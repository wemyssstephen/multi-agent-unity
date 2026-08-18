import time
from typing import Any

import requests
from mcp.server import MCPServer

mcp = MCPServer("Multi-Agent Unity Server", "1.0.0")
BRIDGE_URL = "http://localhost:8080"

def post_to_unity_bridge(payload: dict, timeout: int = 30) -> str:
    try:
        response = requests.post(BRIDGE_URL, json=payload, timeout=timeout)
    except requests.exceptions.Timeout:
        return "Unity bridge timed out. Unity might be busy or has crashed."
    return response.text

def post_with_compile_retry(payload: dict) -> str:
    # "Failed to find component type" means the script hasn't compiled yet.
    # Coupled to ToolRouter.FindComponentType's return string — rename one, rename both.
    for _ in range(10):
        try:
            result = post_to_unity_bridge(payload)
        except requests.exceptions.ConnectionError:
            time.sleep(1)
            continue
        if "Failed to find component type" not in result:
            return result
        time.sleep(1)
    return result
        
@mcp.tool()
def create_gameobject(object_name: str) -> str:
    """Creates an object with the given name."""
    payload =   {
                    "Name": "create_gameobject",
                    "Args": {
                    "ObjectName": object_name,
                    }
                }
    return post_to_unity_bridge(payload=payload)

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
    return post_to_unity_bridge(payload=payload)

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
    return post_to_unity_bridge(payload=payload)

@mcp.tool()
def read_scene() -> str:
    """Returns the scene hierarchy with nested tree"""
    payload =   {
                    "Name": "read_scene",
                    "Args": {}
                }
    return post_to_unity_bridge(payload=payload)

@mcp.tool()
def read_script(script_path: str) -> str:
    """Returns the full contents of a C# script at a given path"""
    payload =   {
                    "Name": "read_script",
                    "Args": {
                        "ScriptPath": script_path
                    }
                }
    return post_to_unity_bridge(payload=payload)

@mcp.tool()
def read_console() -> str:
    """Returns the last 500 lines of the console"""
    payload = {
                "Name": "read_console",
                "Args": {}
    }
    return post_to_unity_bridge(payload=payload)

if __name__ == "__main__":
    mcp.run(transport="stdio")
