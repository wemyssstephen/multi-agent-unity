import logging
import shutil
from pathlib import Path

from multi_agent_unity.unity_bridge_client import refresh_database, request_compile, reset_scene, wait_for_compile

log = logging.getLogger("scene_manager")

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
