from multi_agent_unity.client import build_request_payload, get_text, get_usage

RESPONSE =  {
                "content": [{"type": "text", "text": "Hello!"}, {"type": "text", "text": "How are you?"}],
                "usage": {"input_tokens": 11, "output_tokens": 19},
            } # TODO: Consider making the response a fixture to be used in multiple tests.
                # Also, means that a test mutating it doesn't matter.

def test_build_request_payload(monkeypatch) -> None:
    monkeypatch.setenv("ANTHROPIC_API_KEY", "test_api_key")
    messages = [{"role": "user", "content": "Hello, world!"}]
    headers, body = build_request_payload(messages)

    assert "x-api-key" in headers
    assert headers["Content-Type"] == "application/json"
    assert body["model"] == "claude-haiku-4-5-20251001"
    assert body["messages"] == messages

def test_get_text() -> None:
    text = get_text(RESPONSE)
    assert text == "Hello!\nHow are you?"

def test_get_usage() -> None:
    usage = get_usage(RESPONSE)
    assert usage == {"input_tokens": 11, "output_tokens": 19}
