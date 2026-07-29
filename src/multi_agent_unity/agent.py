
import multi_agent_unity.client as client


def agent_loop(task: str, tool_registry: dict, tool_definitions: list, sender=client.send_request) -> dict:
    """Main loop for an agent""" # TODO: Tool registry and definitions are separate which is not good.
    messages = [{"role": "user", "content": task}]

    total_input_tokens = 0
    total_output_tokens = 0

    for _ in range(10): # temporary testing cap
        headers, body = client.build_request_payload(messages, tools=tool_definitions)
        response = sender(headers, body)
        stop = client.get_stop_reason(response)

        usage = client.get_usage(response)
        total_input_tokens += usage["input_tokens"]
        total_output_tokens += usage["output_tokens"]

        if stop == "tool_use":
            messages.append({"role": "assistant", "content": response["content"]})
            tool_calls = client.get_tool_calls(response)
            tool_results = client.get_tool_results(tool_calls, tool_registry)
            messages.append({"role": "user", "content": tool_results})
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
