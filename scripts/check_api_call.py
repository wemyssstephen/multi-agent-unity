import tomllib
from pathlib import Path

from multi_agent_unity.evaluator import run_evaluator_once
from multi_agent_unity.logging_setup import setup_logging
from multi_agent_unity.unity_launcher import launch_unity_headless, wait_for_bridge

TASKS_PATH = Path(__file__).parent / "tasks.toml"


def load_tasks():
    with open(TASKS_PATH, "rb") as f:
        return tomllib.load(f)


def get_task(data, genre, scene):
    """Return the prompt for a cell, collapsed to single spaces so line
    breaks in the TOML don't reach the model."""
    raw = data[genre][scene]["task"]
    return " ".join(raw.split())


if __name__ == "__main__":
    setup_logging(logfile="run.log")
    headless = launch_unity_headless()
    if not wait_for_bridge():
        raise RuntimeError("Unity bridge did not start.")


    data = load_tasks()

    genre = "topdown"           # "sidescroller" | "topdown"
    scene = "populated"         # "empty" | "populated"
    system = "s"                # "s" | "m"

    task = get_task(data, genre, scene)
    tests = data["tests"][genre]
    scene_name = f"{genre}_{scene}"

    try:
        print(run_evaluator_once(scene_name, task, tests, system))
    finally:
        headless.terminate()
