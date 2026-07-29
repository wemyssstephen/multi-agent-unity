
def add(a: int, b: int) -> int:
    """Add two integers."""
    return a + b

add_tool = {
    "name": "add",
    "description": "Add two integers.",
    "input_schema": {
        "type": "object",
        "properties": {
            "a": {"type": "integer"},
            "b": {"type": "integer"}
        },
        "required": ["a", "b"]
    }
}

TOOL_REGISTRY = {"add": add}
TOOL_DEFINITIONS = [add_tool]
