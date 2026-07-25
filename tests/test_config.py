"""Tests for config: proves the api key is loaded correctly from the .env file."""
import pytest
from multi_agent_unity.config import get_anthropic_api_key

monkeypatch = pytest.MonkeyPatch()

def test_get_anthropic_api_key(monkeypatch) -> None:
    monkeypatch.setenv("ANTHROPIC_API_KEY", "test_api_key")
    assert get_anthropic_api_key() == "test_api_key"

def test_get_anthropic_api_key_missing(monkeypatch) -> None:
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    with pytest.raises(RuntimeError):
        get_anthropic_api_key()