import requests

from multi_agent_unity.config import get_anthropic_api_key

END_POINT = "https://api.anthropic.com/v1/messages"

def build_request_payload(messages: list[dict],
                          *,
                          tools: list[dict] | None = None,
                          model: str,
                          system=None) -> tuple[dict, dict]:
    """Builds the request payload for the Anthropic API."""

    api_key = get_anthropic_api_key() # TODO: Consider fetching the API key as this is a dependency

    request_headers =   {
                        "x-api-key": api_key,
                        "anthropic-version": "2023-06-01",
                        "Content-Type": "application/json"
                        }

    request_body =      {
                        "model": model,
                        "max_tokens": 8192,
                        "messages": messages,
                        "cache_control": {"type": "ephemeral"}
                        }
    if system:
        request_body["system"] = system

    if tools:
        request_body["tools"] = tools

    return request_headers, request_body

def send_request(request_headers: dict, request_body: dict) -> dict:
    """Sends a request to the Anthropic API and returns the response in a dictionary."""
    response = requests.post(END_POINT, headers=request_headers, json=request_body)
    response.raise_for_status()  # Raise an error for bad responses
    return response.json()

def get_text(response: dict) -> str:
    """Extracts the text from the Anthropic API response."""
    text_blocks = [block["text"] for block in response["content"] if block["type"] == "text"]
    return "\n".join(text_blocks)

def get_usage(response: dict) -> dict:
    """Extracts the token usage from the Anthropic API response."""
    usage = response["usage"]
    return {
        "input_tokens": usage["input_tokens"],
        "output_tokens": usage["output_tokens"],
        "cache_read_input_tokens": usage.get("cache_read_input_tokens", 0),
        "cache_creation_input_tokens": usage.get("cache_creation_input_tokens", 0)
    }

def get_stop_reason(response: dict) -> str:
    """Extracts the stop reason from the Anthropic API response."""
    return response.get("stop_reason") or "end_turn"

# TODO: Build tool call tests ASAP

def get_tool_calls(response: dict) -> list[dict]:
    """Extracts the tool calls from the Anthropic API response."""
    tool_calls = []
    for block in response["content"]:
        if block["type"] == "tool_use":
            tool_calls.append(
                {
                    "name": block["name"],
                    "tool_use_id": block["id"],
                    "input": block["input"]
                }
            )
    return tool_calls

def get_tool_results(tool_calls: list[dict], tool_registry: dict) -> list[dict]:
    """Executes tool calls and returns the results."""
    # TODO: Consider moving this to a separate module for tool execution.
    return [run_tool_call(call, tool_registry) for call in tool_calls]

def run_tool_call(call: dict, tool_registry: dict) -> dict:
    """Executes a single tool call and returns the result."""
    fn = tool_registry.get(call["name"])
    if fn is None:
        error_message = f"Tool '{call['name']}' not found in registry."
        return build_tool_result(call["tool_use_id"], error_message, is_error=True)
    try:
        result = fn(**call["input"])
    except Exception as e:
        error_message = f"Error: {e}"
        return build_tool_result(call["tool_use_id"], error_message, is_error=True)
    return build_tool_result(call["tool_use_id"], str(result))

def build_tool_result(tool_use_id: str, content: str, is_error: bool = False) -> dict:
    """Builds a tool result dictionary."""
    return {
        "type": "tool_result",
        "tool_use_id": tool_use_id,
        "content": content,
        "is_error": is_error
    }

def print_response_to_terminal(response: dict) -> None:
    """Prints the response from the Anthropic API to the terminal."""
    text = get_text(response)
    usage = get_usage(response)
    print(f"Response Text: {text}")
    print(f"Token Usage: {usage}")
