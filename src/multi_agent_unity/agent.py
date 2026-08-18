from mcp import Client
from mcp.client.stdio import StdioServerParameters, stdio_client

import multi_agent_unity.client as client
from multi_agent_unity.agent_classes import Agent, OrchestratorAgent, SingleAgent

server_params = StdioServerParameters(
    command=r"C:\dev\multi-agent-unity\.venv\Scripts\python.exe",
    args=[r"C:\dev\multi-agent-unity\src\multi_agent_unity\server.py"],
)

async def agent_loop_handler(task: str, system_flag, model="claude-haiku-4-5-20251001", sender=client.send_request) -> dict:
    """Handles agent loops"""
    async with Client(stdio_client(server_params)) as mcp:
    # Find the tools available on the server.
        tools = await mcp.list_tools()
        # Reshape the format to what Anthropic expects.
        anthropic_tools =   [
                        {"name": t.name, "description": t.description, "input_schema": t.input_schema} for t in tools.tools
                        ]
        if system_flag == "m":
            # Run multi-agent loop
            agent = OrchestratorAgent.make(model, anthropic_tools)
            return await run_agent(task, agent, mcp, sender)

        elif system_flag == "s":
            # Run single-agent loop
            agent = SingleAgent.make(model, anthropic_tools)
            return await run_agent(task, agent, mcp, sender)

        else:
            # Report that system flag is wrong
            return ""


async def run_agent(task: str, agent: Agent, mcp, sender) -> dict:
    """Main loop for an agent. Task is plain English 'Make a Cube'. """
    # The stored conversation with the agent
    messages = [{"role": "user", "content": task}]

    # A log of tokens used
    total_input_tokens = 0
    total_output_tokens = 0
    total_cache_read_tokens = 0
    total_cache_creation_tokens = 0

    for _ in range(10): # TODO: temporary testing cap
        # Builds the request to send to Anthropic. Packs the API key + headers + conversation + tools.
        headers, body = client.build_request_payload(messages, tools=agent.tools, model=agent.model, system=agent.system_prompt)
        
        # Sends the request to the Anthropic API
        response = sender(headers, body)
        
        # Pulls current token count
        usage = client.get_usage(response)
        total_input_tokens += usage["input_tokens"]
        total_output_tokens += usage["output_tokens"]
        total_cache_read_tokens += usage["cache_read_input_tokens"]
        total_cache_creation_tokens += usage["cache_creation_input_tokens"]
        
        # Extracts the reason the conversation stopped.
        stop = client.get_stop_reason(response)

        # Checks if the model wants to use a tool
        if stop == "tool_use":
            # Adds models turn to the conversation, including its request to use a tool.
            messages.append({"role": "assistant", "content": response["content"]})
            # Grabs the tool request from the response
            tool_calls = client.get_tool_calls(response)
            # Build an empty list for the results of tool use
            tool_results = []
            # Check if we're calling a sub-agent or a Unity tool
            for call in tool_calls:
                if call["name"] in agent.worker_names:
                    # Build the required worker agent
                    worker = agent.build_worker(call["name"])
                    print(f"[{agent.name}] delegating -> {call['name']}")
                    # Run it
                    result = await run_agent(call["input"]["task"], worker, mcp, sender)
                    # Extract the result
                    text = result["text"]
                    print(f"[{call['name']}] -> {text}")
                else:
                    # Call the Unity tool
                    result = await mcp.call_tool(call["name"], call["input"])
                    # Extract the result
                    text = result.content[0].text
                    print(f"[{agent.name}] tool {call['name']} -> {text[:120]}")
                tool_results.append(client.build_tool_result(call["tool_use_id"], text))
            # Append the result of the tool call to the conversation
            messages.append({"role": "user", "content": tool_results})
            # Go again.
            continue

        elif stop == "max_tokens":
            # TODO: Handle truncation and flag. For now just return what we've got.
            return {
                    "text": client.get_text(response),
                    "input_tokens": total_input_tokens,
                    "output_tokens": total_output_tokens,
                    "cache_read_input_tokens": total_cache_read_tokens,
                    "cache_creation_input_tokens": total_cache_creation_tokens
                    }
        else:
            return {
                    "text": client.get_text(response),
                    "input_tokens": total_input_tokens,
                    "output_tokens": total_output_tokens,
                    "cache_read_input_tokens": total_cache_read_tokens,
                    "cache_creation_input_tokens": total_cache_creation_tokens
                    }

    return  {
            "text": "Agent loop reached maximum iterations without completing the task.",
            "input_tokens": total_input_tokens,
            "output_tokens": total_output_tokens,
            "cache_read_input_tokens": total_cache_read_tokens,
            "cache_creation_input_tokens": total_cache_creation_tokens
            }
            # TODO: this could potentially be a result class
