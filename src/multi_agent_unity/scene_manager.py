import logging
import shutil

from multi_agent_unity.bridge_manager import post
from multi_agent_unity.unity_manager import refresh_database, request_compile, wait_for_compile
from multi_agent_unity.config import BenchPaths, UnityAssets

log = logging.getLogger("scene_manager")

def prepare_scene(scene: str) -> None:
    # Reset the scene and prepare scripts
    reset_scene(scene)
    wipe_scripts()
    copy_scene_scripts(scene)
    refresh_database()
    request_compile()
    wait_for_compile()

def wipe_scripts() -> None:
    if BenchPaths.working_scripts.exists():
        shutil.rmtree(BenchPaths.working_scripts)
    BenchPaths.working_scripts.mkdir(parents=True)
    log.info("wipe_scripts -> %s", BenchPaths.working_scripts)

def copy_scene_scripts(scene: str) -> None:
    scene_dir = BenchPaths.seed_scripts / scene
    if not scene_dir.exists():
        log.info("copy_scene_scripts: no scene dir for %s", scene)
        return
    for item in scene_dir.iterdir():
        shutil.copy(item, BenchPaths.working_scripts / item.name)
    log.info("copy_scene_scripts -> copied seed for %s", scene)
    
def open_scene(scene_path: str) -> str:
    result = post({"Name": "open_scene", "Args": {"ScenePath": scene_path}})
    log.info("open_scene -> %s", result)
    return result

def save_scene() -> str:
    result = post({"Name": "save_scene", "Args": {}})
    log.info("save_scene -> %s", result)
    return result

def reset_scene(scene: str) -> str:
    """Copy scene template to the working path and open it"""
    template = BenchPaths.unity_project / UnityAssets.scene_templates / f"{scene}.unity"
    shutil.copy(template, BenchPaths.unity_project / UnityAssets.working_scene)
    result = open_scene(UnityAssets.working_scene)
    log.info("reset_scene -> %s", result)
    return result
