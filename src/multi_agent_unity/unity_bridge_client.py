import shutil
import time

import requests

BRIDGE_URL = "http://localhost:8080"

SCENE_TEMPLATES = {
    "sidescroller_empty":     "Assets/MultiAgentBridge/Scenes/sidescroller_empty.unity",
    "sidescroller_populated": "Assets/MultiAgentBridge/Scenes/sidescroller_populated.unity",
    "topdown_empty":          "Assets/MultiAgentBridge/Scenes/topdown_empty.unity",
    "topdown_populated":      "Assets/MultiAgentBridge/Scenes/topdown_populated.unity",
}

PROJECT_ROOT = "unity"
WORKING_SCENE = "Assets/MultiAgentBridge/Working/current.unity"

def post(payload: dict, timeout: int = 30) -> str:
    try:
        response = requests.post(BRIDGE_URL, json=payload, timeout=timeout)
    except requests.exceptions.Timeout:
        return "Unity bridge timed out. Unity might be busy or has crashed."
    return response.text

def post_with_compile_retry(payload: dict) -> str:
    # "Failed to find component type" means the script hasn't compiled yet.
    for _ in range(10):
        try:
            result = post(payload)
        except requests.exceptions.ConnectionError:
            time.sleep(1)
            continue
        if "Failed to find component type" not in result:
            return result
        time.sleep(1)
    return result

def reset_scene(scene: str) -> str:
    """Copy scene template to the working path and open it"""
    src = f"{PROJECT_ROOT}/{SCENE_TEMPLATES[scene]}"
    shutil.copy(src, f"unity/{WORKING_SCENE}")
    result = post({"Name": "open_scene", "Args": {"ScenePath": WORKING_SCENE}})
    wait_for_compile()
    return result

def save_scene() -> str:
    return post({"Name": "save_scene", "Args": {}})

def run_tests(test_name: str) -> str:
    return post({"Name": "run_tests", "Args": {"TestName": test_name}})

def poll_test_result() -> str:
    return post({"Name": "poll_test_result", "Args": {}})

def request_compile() -> str:
    return post({"Name": "request_compile", "Args": {}})

def check_compile() -> str:
    return post({"Name": "check_compile", "Args": {}})

def refresh_database() -> str:
    return post({"Name": "refresh_database", "Args": {}})

def wait_for_compile(max_wait_time: int = 30) -> str:
    """Waits for Unity to finish compiling scripts."""
    time.sleep(1)  # Initial wait to allow Unity to start compiling
    for _ in range(max_wait_time):
        result = check_compile()
        if result == "ready":
            return "ready"
        elif result == "errors":
            return "errors"
        time.sleep(1)
    return "timeout"

