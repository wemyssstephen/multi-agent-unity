from typing import Any

from mcp.server import MCPServer

from multi_agent_unity.unity_bridge_client import post, post_with_compile_retry

mcp = MCPServer("Multi-Agent Unity Server", "1.0.0")
BRIDGE_URL = "http://localhost:8080"

@mcp.tool()
def create_gameobject(object_name: str) -> str:
    """Creates an object with the given name."""
    payload =   {
                    "Name": "create_gameobject",
                    "Args": {
                    "ObjectName": object_name,
                    }
                }
    return post(payload=payload)

@mcp.tool()
def create_script(script_name: str, target_path: str, script_content: str) -> str:
    """Creates a C# script with the given name, target path, and content."""
    payload =   {
                    "Name": "create_script",
                    "Args": {
                        "ScriptName": script_name,
                        "TargetPath": target_path,
                        "ScriptContent": script_content
                    }
                }
    return post(payload=payload)

@mcp.tool()
def add_component(game_object_id: str, component_type: str) -> str:
    """Adds a component to the specified game object."""
    payload =   {
                    "Name": "add_component",
                    "Args": {
                        "GameObjectId": game_object_id,
                        "ComponentType": component_type
                    }
                }
    return post_with_compile_retry(payload=payload)

@mcp.tool()
def set_property(game_object_id: str,
                 component_type: str,
                 property_path: str,
                 value: Any,
                 reference_component_type: str | None = None) -> str:
    """Sets a property on a component of the specified game object."""
    payload =   {
                    "Name": "set_property",
                    "Args": {
                        "GameObjectId": game_object_id,
                        "ComponentType": component_type,
                        "PropertyPath": property_path,
                        "Value": value,
                    }
                }
    if reference_component_type:
        payload["Args"]["ReferenceComponentType"] = reference_component_type

    return post_with_compile_retry(payload=payload)

@mcp.tool()
def create_primitive(object_name: str, primitive_name: str) -> str:
    """Creates a primitive shape (Cube, Sphere, Cylinder, Capsule, Plane, Quad) with the given name"""
    payload =   {
                    "Name": "create_primitive",
                    "Args": {
                        "ObjectName": object_name,
                        "PrimitiveName": primitive_name}
    }
    return post(payload=payload)

@mcp.tool()
def read_scene() -> str:
    """Returns the scene hierarchy with nested tree"""
    payload =   {
                    "Name": "read_scene",
                    "Args": {}
                }
    return post(payload=payload)

@mcp.tool()
def read_script(script_path: str) -> str:
    """Returns the full contents of a C# script at a given path"""
    payload =   {
                    "Name": "read_script",
                    "Args": {
                        "ScriptPath": script_path
                    }
                }
    return post(payload=payload)

@mcp.tool()
def read_console() -> str:
    """Returns the last 500 lines of the console"""
    payload = {
                "Name": "read_console",
                "Args": {}
    }
    return post(payload=payload)

if __name__ == "__main__":
    mcp.run(transport="stdio")
