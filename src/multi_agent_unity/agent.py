import logging
import sys
from pathlib import Path

from mcp import Client
from mcp.client.stdio import StdioServerParameters, stdio_client

import multi_agent_unity.anthropic_api_manager as anthropic_api_manager
from multi_agent_unity.agent_classes import Agent, OrchestratorAgent, SingleAgent

server_params = StdioServerParameters(
    command=sys.executable,
    args=[str(Path(__file__).parent / "mcp_server.py")],
)

log = logging.getLogger("agent")

class Budget:
    """The iteration budget for each Bench run. Each API call spends one."""
    def __init__(self, limit: int):
        self.limit = limit
        self.iterations = 0
        self.hit_cap = False
        self.tool_call_count = 0
        self.tool_calls_by_name = {}
        self.usage = {
            "input_tokens": 0,
            "output_tokens": 0,
            "cache_read_input_tokens": 0,
            "cache_creation_input_tokens": 0,
        }

    def take(self) -> bool:
        if self.iterations >= self.limit:
            self.hit_cap = True
            return False
        self.iterations += 1
        return True

    def record_usage(self, usage: dict) -> None:
        for key in self.usage:
            self.usage[key] += usage[key]

    def record_tool_call(self, name: str) -> None:
        self.tool_call_count += 1
        self.tool_calls_by_name[name] = self.tool_calls_by_name.get(name, 0) + 1

    def summary(self) -> dict:
        return {
            "iterations": self.iterations,
            "tool_call_count": self.tool_call_count,
            "tool_calls_by_name": self.tool_calls_by_name,
            "hit_cap": self.hit_cap,
            **self.usage,
        }

async def agent_loop_handler(task: str, system_flag, model, iteration_budget, sender=anthropic_api_manager.send_request) -> dict:
    """Handles agent loops"""
    agent_types = {"m": OrchestratorAgent, "s": SingleAgent}
    if system_flag not in agent_types:
        raise ValueError("System flag is wrong. Use 'm' for multi-agent or 's' for single-agent.")
    async with Client(stdio_client(server_params)) as mcp:
        log.info("Retrieving tools...")
        tools = await mcp.list_tools()
        anthropic_tools =   [
                        {"name": t.name, "description": t.description, "input_schema": t.input_schema} for t in tools.tools
                        ]
        agent = agent_types[system_flag].make(model, anthropic_tools)
        log.info("Running %s agent loop...", agent.name)
        budget = Budget(iteration_budget)
        result = await run_agent(task, agent, mcp, sender, budget)
        return {"text": result, **budget.summary()}


async def run_agent(task: str, agent: Agent, mcp, sender, budget: Budget) -> str:
    """Main loop for an agent. Task is plain English 'Make a Cube'. """
    messages = [{"role": "user", "content": task}]

    while budget.take():
        headers, body = anthropic_api_manager.build_request_payload(messages, tools=agent.tools, model=agent.model, system=agent.system_prompt)
        response = sender(headers, body)
        budget.record_usage(anthropic_api_manager.get_usage(response))

        if anthropic_api_manager.get_stop_reason(response) != "tool_use":
            return anthropic_api_manager.get_text(response)

        messages.append({"role": "assistant", "content": response["content"]})
        tool_results = []
        for call in anthropic_api_manager.get_tool_calls(response):
            budget.record_tool_call(call["name"])

            if call["name"] in agent.worker_names:
                worker = agent.build_worker(call["name"])
                log.info("%s delegating -> %s", agent.name, call["name"])
                result_text = await run_agent(call["input"]["task"], worker, mcp, sender, budget)
                log.info("%s returned -> %s", call["name"], result_text[:120])

            else:
                result = await mcp.call_tool(call["name"], call["input"])
                result_text = result.content[0].text
                log.info("%s tool %s -> %s", agent.name, call["name"], result_text[:120])
            tool_results.append(anthropic_api_manager.build_tool_result(call["tool_use_id"], result_text))
        messages.append({"role": "user", "content": tool_results})

    return "Iteration budget exhausted"
