import shutil
import time

import requests

BRIDGE_URL = "http://localhost:8080"

SCENE_TEMPLATES = {
    "sidescroller_empty":     "Assets/Benchmark/Fixtures/sidescroller_empty.unity",
    "sidescroller_populated": "Assets/Benchmark/Fixtures/sidescroller_populated.unity",
    "topdown_empty":          "Assets/Benchmark/Fixtures/topdown_empty.unity",
    "topdown_populated":      "Assets/Benchmark/Fixtures/topdown_populated.unity",
}

WORKING_SCENE = "Assets/Benchmark/Working/current.unity"

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

def reset_scene(scene: str) -> None:
    """Copy scene template to the working path and open it"""
    shutil.copy(SCENE_TEMPLATES[scene], WORKING_SCENE)
    post({"Name": "open_scene", "Args": {"ScenePath": WORKING_SCENE}})

def save_scene() -> None:
    post({"Name": "save_scene", "Args": {}})
