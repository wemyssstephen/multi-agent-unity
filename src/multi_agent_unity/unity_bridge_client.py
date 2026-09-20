import logging
import shutil
import time

import requests

from multi_agent_unity.exceptions import BridgeTimeout

BRIDGE_URL = "http://localhost:8080"

SCENE_TEMPLATES = {
    "sidescroller_empty":     "Assets/MultiAgentBridge/Scenes/sidescroller_empty.unity",
    "sidescroller_populated": "Assets/MultiAgentBridge/Scenes/sidescroller_populated.unity",
    "topdown_empty":          "Assets/MultiAgentBridge/Scenes/topdown_empty.unity",
    "topdown_populated":      "Assets/MultiAgentBridge/Scenes/topdown_populated.unity",
}

PROJECT_ROOT = "unity"
WORKING_SCENE = "Assets/MultiAgentBridge/Working/current.unity"

log = logging.getLogger("bridge")
_session = requests.Session()

def post(payload: dict, timeout: int = 30) -> str:
    for _ in range(30):
        try:
            response = _session.post(BRIDGE_URL, json=payload, timeout=timeout)
            return response.text
        except (requests.exceptions.Timeout, TimeoutError) as e:
            raise BridgeTimeout("post timed out") from e
        except requests.exceptions.ConnectionError:
            time.sleep(1)
    return "Unity bridge unreachable after retrying."

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
    log.info("reset_scene -> %s", result)
    return result

def save_scene() -> str:
    result = post({"Name": "save_scene", "Args": {}})
    log.info("save_scene -> %s", result)
    return result

def run_tests(test_name: str) -> str:
    result = post({"Name": "run_tests", "Args": {"TestName": test_name}})
    log.info("run_tests -> %s", result)
    return result

def poll_test_result() -> str:
    return post({"Name": "poll_test_result", "Args": {}})

def request_compile() -> str:
    result = post({"Name": "request_compile", "Args": {}})
    log.info("request_compile -> %s", result)
    return result

def check_compile() -> str:
    return post({"Name": "check_compile", "Args": {}})

def refresh_database() -> str:
    result = post({"Name": "refresh_database", "Args": {}})
    log.info("refresh_database -> %s", result)
    return result

def wait_for_compile(max_wait_time: int = 30) -> str:
    """Waits for Unity to finish compiling scripts."""
    log.info("Waiting for Unity to start compiling...")
    time.sleep(1)  # Initial wait to allow Unity to start compiling
    for _ in range(max_wait_time):
        try:
            result = check_compile()
        except requests.exceptions.ConnectionError:
            log.info("Connection error... waiting...")
            time.sleep(1)
            continue

        if result == "ready":
            log.info("Compile successful...")
            return "ready"
        elif result == "errors":
            log.warning("wait_for_compile -> errors")
            return "errors"
        time.sleep(1)
    log.warning("wait_for_compile -> timeout")
    return "timeout"

def quit_unity() -> str:
    """Ask Unity to exit"""
    # TODO: I think this and start/restart unity being in separate files is pretty weird and should be fixed
    try:
        _session.post(BRIDGE_URL, json={"Name": "quit", "Args": {}}, timeout=5)
    except (requests.exceptions.ConnectionError, requests.exceptions.Timeout):
        return "Unity exiting (connection dropped)"

