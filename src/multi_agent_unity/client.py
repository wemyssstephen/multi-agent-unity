import requests

from multi_agent_unity.config import get_anthropic_api_key

END_POINT = "https://api.anthropic.com/v1/messages"

def build_request_payload(messages: list[dict], model: str = "claude-haiku-4-5-20251001") -> tuple[dict, dict]:
    """Builds the request payload for the Anthropic API."""

    api_key = get_anthropic_api_key() # TODO: Consider fetching the API key as this is a dependency

    request_headers =   {
                        "x-api-key": api_key,
                        "anthropic-version": "2023-06-01",
                        "Content-Type": "application/json"
                        }

    request_body =      {
                        "model": model,
                        "max_tokens": 1024,
                        "messages": messages
                        }

    return request_headers, request_body

def send_request(request_headers: dict, request_body: dict) -> dict:
    """Sends a request to the Anthropic API and returns the response in a dictionary."""
    response = requests.post(END_POINT, headers=request_headers, json=request_body)
    response.raise_for_status()  # Raise an error for bad responses
    return response.json()

def get_text(response: dict) -> str:
    """Extracts the text from the Anthropic API response."""
    text_blocks = [
                    block["text"] for block in response["content"] if block["type"] == "text"
                  ]
    return "\n".join(text_blocks)

def get_usage(response: dict) -> dict:
    """Extracts the token usage from the Anthropic API response."""
    usage = response["usage"]
    return {
        "input_tokens": usage["input_tokens"],
        "output_tokens": usage["output_tokens"],
    }

def get_stop_reason(response: dict) -> str:
    """Extracts the stop reason from the Anthropic API response."""
    return response.get("stop_reason") or "end_turn"

def print_response_to_terminal(response: dict) -> None:
    """Prints the response from the Anthropic API to the terminal."""
    text = get_text(response)
    usage = get_usage(response)
    print(f"Response Text: {text}")
    print(f"Token Usage: {usage}")
