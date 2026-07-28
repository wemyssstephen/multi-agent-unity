
from multi_agent_unity.client import build_request_payload, get_stop_reason, get_text, get_usage, send_request


def agent_loop(task: str) -> dict:
    """Main loop for an agent"""
    messages = [{"role": "user", "content": task}]

    total_input_tokens = 0
    total_output_tokens = 0

    for _ in range(10): # temporary testing cap
        headers, body = build_request_payload(messages)
        response = send_request(headers, body)
        stop = get_stop_reason(response)

        usage = get_usage(response)
        total_input_tokens += usage["input_tokens"]
        total_output_tokens += usage["output_tokens"]

        if stop == "tool_use":
            messages.append({"role": "assistant", "content": response["content"]})
            # TODO: Run tool and append the result to the user turn
            continue

        elif stop == "max_tokens":
            # TODO: Handle truncation and flag. For now just return what we've got.
            return {
                    "text": get_text(response),
                    "input_tokens": total_input_tokens,
                    "output_tokens": total_output_tokens
                    }
        else:
            return {
                    "text": get_text(response),
                    "input_tokens": total_input_tokens,
                    "output_tokens": total_output_tokens
                    }

    return  {
            "text": "Agent loop reached maximum iterations without completing the task.",
            "input_tokens": total_input_tokens,
            "output_tokens": total_output_tokens
            }
            # TODO: this could potentially be a result class
