import logging

from mcp import Client
from mcp.client.stdio import StdioServerParameters, stdio_client

import multi_agent_unity.agent_client as agent_client
from multi_agent_unity.agent_classes import Agent, OrchestratorAgent, SingleAgent

server_params = StdioServerParameters(
    command=r"C:\dev\multi-agent-unity\.venv\Scripts\python.exe",
    args=[r"C:\dev\multi-agent-unity\src\multi_agent_unity\mcp_server.py"],
)

log = logging.getLogger("agent")

def accumulate_tokens(usage_totals: dict, usage: dict) -> None:
    usage_totals["input_tokens"] += usage["input_tokens"]
    usage_totals["output_tokens"] += usage["output_tokens"]
    usage_totals["cache_read_input_tokens"] += usage["cache_read_input_tokens"]
    usage_totals["cache_creation_input_tokens"] += usage["cache_creation_input_tokens"]

async def agent_loop_handler(task: str, system_flag, model="claude-haiku-4-5-20251001", max_iterations=30, sender=agent_client.send_request) -> dict:
    """Handles agent loops"""
    async with Client(stdio_client(server_params)) as mcp:
        # Find the tools available on the server.
        log.info("Beginning the agent loop....")
        tools = await mcp.list_tools()
        log.info("Tools acquired...")
        # Reshape the format to what Anthropic expects.
        anthropic_tools =   [
                        {"name": t.name, "description": t.description, "input_schema": t.input_schema} for t in tools.tools
                        ]
        if system_flag == "m":
            # Run multi-agent loop
            log.info("Running multi-agent loop...")
            agent = OrchestratorAgent.make(model, anthropic_tools)
            return await run_agent(task, agent, mcp, sender, max_iterations)

        elif system_flag == "s":
            # Run single-agent loop
            log.info("Running single-agent loop...")
            agent = SingleAgent.make(model, anthropic_tools)
            return await run_agent(task, agent, mcp, sender, max_iterations)

        else:
            # Report that system flag is wrong
            raise ValueError("System flag is wrong. Use 'm' for multi-agent or 's' for single-agent.")

async def run_agent(task: str, agent: Agent, mcp, sender, max_iterations) -> dict:
    """Main loop for an agent. Task is plain English 'Make a Cube'. """
    messages = [{"role": "user", "content": task}]
    usage_totals = {
        "input_tokens": 0,
        "output_tokens": 0,
        "cache_read_input_tokens": 0,
        "cache_creation_input_tokens": 0,
        }

    iterations = 0
    tool_call_count = 0
    tool_calls_by_name = {}
    iteration_cap = False

    for i in range(max_iterations):
        iterations = i + 1

        headers, body = agent_client.build_request_payload(messages, tools=agent.tools, model=agent.model, system=agent.system_prompt)
        response = sender(headers, body)
        stop_reason = agent_client.get_stop_reason(response)
        accumulate_tokens(usage_totals, agent_client.get_usage(response))

        if stop_reason == "tool_use":
            messages.append({"role": "assistant", "content": response["content"]})
            tool_calls = agent_client.get_tool_calls(response)
            tool_results = []
            for call in tool_calls:
                tool_call_count += 1
                tool_calls_by_name[call["name"]] = tool_calls_by_name.get(call["name"], 0) + 1

                if call["name"] in agent.worker_names:
                    worker = agent.build_worker(call["name"])
                    log.info("%s delegating -> %s", agent.name, call["name"])
                    result = await run_agent(call["input"]["task"], worker, mcp, sender, max_iterations)
                    result_text = result["text"]
                    accumulate_tokens(usage_totals, result)

                    # Update logging counts
                    iterations += result["iterations"]
                    tool_call_count += result["tool_call_count"]
                    for name, n in result["tool_calls_by_name"].items():
                        tool_calls_by_name[name] = tool_calls_by_name.get(name, 0) + n
                    iteration_cap = iteration_cap or result["hit_cap"]
                    log.info("%s returned -> %s", call["name"], result_text[:120])

                else:
                    result = await mcp.call_tool(call["name"], call["input"])
                    result_text = result.content[0].text
                    log.info("%s tool %s -> %s", agent.name, call["name"], result_text[:120])
                tool_results.append(agent_client.build_tool_result(call["tool_use_id"], result_text))
            messages.append({"role": "user", "content": tool_results})
            continue

        else:
            # TODO might want to handle "max_tokens" stop reason
            return {
                "text": agent_client.get_text(response),
                "iterations": iterations,
                "tool_call_count": tool_call_count,
                "tool_calls_by_name": tool_calls_by_name,
                "hit_cap": iteration_cap,
                **usage_totals,
            }

    iteration_cap = True
    return {
        "text": "Hit max iterations",
        "iterations": iterations,
        "tool_call_count": tool_call_count,
        "tool_calls_by_name": tool_calls_by_name,
        "hit_cap": iteration_cap,
        **usage_totals,
    }
