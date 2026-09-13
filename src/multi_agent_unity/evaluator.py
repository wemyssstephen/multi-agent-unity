import asyncio
import logging
import shutil
import time
from pathlib import Path

from multi_agent_unity.agent import agent_loop_handler
from multi_agent_unity.unity_bridge_client import poll_test_result, refresh_database, request_compile, reset_scene, run_tests, save_scene, wait_for_compile

log = logging.getLogger("evaluator")

WORKING_SCRIPTS = Path("unity/Assets/MultiAgentBridge/Working/Scripts")
SCENE_SCRIPTS = Path("unity/Scenes/SceneScripts")

def prepare_scene(scene: str) -> None:
    # Reset the scene and prepare scripts
    reset_scene(scene)
    wipe_scripts()
    refresh_database()
    wait_for_compile()
    copy_scene_scripts(scene)
    refresh_database()
    request_compile()
    wait_for_compile()

def wipe_scripts() -> None:
    if WORKING_SCRIPTS.exists():
        shutil.rmtree(WORKING_SCRIPTS)
    WORKING_SCRIPTS.mkdir(parents=True)
    log.info("wipe_scripts -> %s", WORKING_SCRIPTS)

def copy_scene_scripts(scene: str) -> None:
    scene_dir = SCENE_SCRIPTS / scene
    if not scene_dir.exists():
        log.info("copy_scene_scripts: no scene dir for %s", scene)
        return
    for item in scene_dir.iterdir():
        shutil.copy(item, WORKING_SCRIPTS / item.name)
    log.info("copy_scene_scripts -> copied seed for %s", scene)

def run_evaluator_once(scene: str, task: str, test_names: list, system_flag: str) -> dict:
    prepare_scene(scene)

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
        run_tests(test)
        while(test_state:= poll_test_result()) == "running":
            time.sleep(1)
        test_results[test] = test_state

    passed = all(state == "Passed" for state in test_results.values())
    log.info("RESULT scene=%s system=%s passed=%s results=%s",
         scene, system_flag, passed, test_results)

    # Return the results
    return {
        "scene": scene,
        "system": system_flag,
        "results": test_results,
        "passed": passed,
        "compiled": True,
        "agent_text": agent_result["text"],
        "input_tokens": agent_result["input_tokens"],
        "output_tokens": agent_result["output_tokens"],
    }
