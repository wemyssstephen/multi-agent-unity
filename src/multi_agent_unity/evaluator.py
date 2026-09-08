import asyncio
import shutil
import time
from pathlib import Path

from multi_agent_unity.agent import agent_loop_handler
from multi_agent_unity.unity_bridge_client import poll_test_result, reset_scene, run_tests, save_scene, request_compile, refresh_database, wait_for_compile

WORKING_SCRIPTS = Path("unity/Assets/MultiAgentBridge/Working/Scripts")
SCENE_SCRIPTS = Path("unity/Scenes/SceneScripts")

def wipe_scripts() -> None:
    if WORKING_SCRIPTS.exists():
        shutil.rmtree(WORKING_SCRIPTS)
    WORKING_SCRIPTS.mkdir(parents=True)
    
def copy_scene_scripts(scene: str) -> None:
    scene_dir = SCENE_SCRIPTS / scene
    if not scene_dir.exists():
        return
    for item in scene_dir.iterdir():
        shutil.copy(item, WORKING_SCRIPTS / item.name)

def run_evaluator_once(scene: str, task: str, test_names: list, system_flag: str) -> dict:
    # Reset the scene and prepare scripts
    open_result = reset_scene(scene)
    print(f"reset_scene -> {open_result}")
    wipe_scripts()
    print(f"Scripts wiped. Working scripts directory: {WORKING_SCRIPTS}")
    refresh_database()
    print("Database refreshed.")
    wait_for_compile()
    print("Initial compile check complete.")
    copy_scene_scripts(scene)
    print(f"Scene scripts copied from {SCENE_SCRIPTS / scene} to {WORKING_SCRIPTS}")
    refresh_database()
    print("Database refreshed.")
    request_compile()
    print("Compile requested.")
    wait_for_compile()
    print("Compile check complete.")

    # Run the agent loop and save
    agent_result = asyncio.run(agent_loop_handler(task, system_flag))
    save_scene()

    # Fail tests if Unity does not compile.
    request_compile()
    compile_state = wait_for_compile()
    if compile_state != "ready":
        return {
            "scene": scene,
            "system": system_flag,
            "results": {},
            "passed": False,
            "compiled": False,
            "agent_text": agent_result["text"],
            "input_tokens": agent_result["input_tokens"],
            "output_tokens": agent_result["output_tokens"],
            "compile_state": compile_state
        }

    # Otherwise, run the tests and collect results.
    test_results = {}
    for test in test_names:
        print(f"Running test: {test}")
        run_tests(test)
        while(test_state:= poll_test_result()) == "running":
            time.sleep(1)
        test_results[test] = test_state

    # Return the results
    return {
        "scene": scene,
        "system": system_flag,
        "results": test_results,
        "passed": all(state == "Passed" for state in test_results.values()),
        "compiled": True,
        "agent_text": agent_result["text"],
        "input_tokens": agent_result["input_tokens"],
        "output_tokens": agent_result["output_tokens"],
    }
