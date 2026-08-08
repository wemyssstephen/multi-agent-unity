from mcp import Client
from mcp.client.stdio import StdioServerParameters, stdio_client

import multi_agent_unity.client as client

server_params = StdioServerParameters(
    command=r"C:\dev\multi-agent-unity\.venv\Scripts\python.exe",
    args=[r"C:\dev\multi-agent-unity\src\multi_agent_unity\server.py"],
)


async def agent_loop(task: str, sender=client.send_request) -> dict:
    """Main loop for an agent. Task is plain English 'Make a Cube'. """
    # Start the MCP server and connect to it.
    async with Client(stdio_client(server_params)) as mcp:
        # Find the tools available on the server.
        tools = await mcp.list_tools()
        # Reshape the format to what Anthropic expects.
        anthropic_tools = [
            {"name": t.name, "description": t.description, "input_schema": t.input_schema} for t in tools.tools
        ]

        # The stored conversation with the agent
        messages = [{"role": "user", "content": task}]

        # A log of tokens used
        total_input_tokens = 0
        total_output_tokens = 0

        for _ in range(10): # TODO: temporary testing cap
            # Builds the request to send to Anthropic. Packs the API key + headers + conversation + tools.
            headers, body = client.build_request_payload(messages, tools=anthropic_tools)
            # Sends the request to the Anthropic API
            response = sender(headers, body)
            # Extracts the reason the conversation stopped.
            stop = client.get_stop_reason(response)
            # Pulls current token count
            usage = client.get_usage(response)
            total_input_tokens += usage["input_tokens"]
            total_output_tokens += usage["output_tokens"]

            # Checks if the model wants to use a tool
            if stop == "tool_use":
                # Adds models turn to the conversation, including its request to use a tool.
                messages.append({"role": "assistant", "content": response["content"]})
                # Grabs the tool request from the response
                tool_calls = client.get_tool_calls(response)
                # Build an empty list for the results of tool use
                tool_results = []
                for call in tool_calls:
                    # Send the tool's name and input to the MCP server. MCP POSTs to Unity.
                    # Unity runs the tool and replies
                    result = await mcp.call_tool(call["name"], call["input"])
                    # Grab the readable text from Unity's reply
                    text = result.content[0].text
                    # Puts the reply in a format Anthropic expects, tagged with the correct id.
                    tool_results.append(client.build_tool_result(call["tool_use_id"], text))
                # Add all the tool responses to the conversation.
                messages.append({"role": "user", "content": tool_results})
                # Go again.
                continue

            elif stop == "max_tokens":
                # TODO: Handle truncation and flag. For now just return what we've got.
                return {
                        "text": client.get_text(response),
                        "input_tokens": total_input_tokens,
                        "output_tokens": total_output_tokens
                        }
            else:
                return {
                        "text": client.get_text(response),
                        "input_tokens": total_input_tokens,
                        "output_tokens": total_output_tokens
                        }

        return  {
                "text": "Agent loop reached maximum iterations without completing the task.",
                "input_tokens": total_input_tokens,
                "output_tokens": total_output_tokens
                }
                # TODO: this could potentially be a result class
