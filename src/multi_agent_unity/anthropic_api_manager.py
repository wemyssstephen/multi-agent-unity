import logging
import time

import requests

from multi_agent_unity.config import AnthropicAPI

log = logging.getLogger("anthropic_api_manager")

def build_request_payload(messages: list[dict],
                          *,
                          tools: list[dict] | None = None,
                          model: str,
                          system=None) -> tuple[dict, dict]:
    """Builds the request payload for the Anthropic API."""

    request_headers =   {
                        "x-api-key": AnthropicAPI.api_key(),
                        "anthropic-version": AnthropicAPI.version,
                        "Content-Type": "application/json"
                        }

    request_body =      {
                        "model": model,
                        "max_tokens": AnthropicAPI.max_tokens,
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
    for attempt in range (AnthropicAPI.retries):
        try:
            response = requests.post(AnthropicAPI.endpoint, headers=request_headers,
                                     json=request_body, timeout=AnthropicAPI.timeout)
        except (requests.exceptions.Timeout, requests.exceptions.ConnectionError):
            time.sleep(2 ** attempt)
            continue
        if response.status_code in AnthropicAPI.RETRYABLE:
            time.sleep(2 ** attempt)
            continue
        if not response.ok:
            log.error("API %s: %s", response.status_code, response.text)
        response.raise_for_status()
        return response.json()
    raise RuntimeError(f"Anthropic API failed after {AnthropicAPI.retries} attempts")

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

def build_tool_result(tool_use_id: str, content: str, is_error: bool = False) -> dict:
    """Builds a tool result dictionary."""
    return {
        "type": "tool_result",
        "tool_use_id": tool_use_id,
        "content": content,
        "is_error": is_error
    }
