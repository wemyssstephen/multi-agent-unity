"""Smoke test: proves the package imports and the harness can run at all."""

from multi_agent_unity import project_name

def test_project_name() -> None:
    assert project_name() == "multi-agent-unity"