import tomllib
from pathlib import Path

from multi_agent_unity.evaluator import run_evaluator_once

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
    data = load_tasks()

    genre = "topdown"      # "sidescroller" | "topdown"
    scene = "populated"         # "empty" | "populated"
    system = "s"                # "s" | "m"

    task = get_task(data, genre, scene)
    tests = data["tests"][genre]
    scene_name = f"{genre}_{scene}"

    print(run_evaluator_once(scene_name, task, tests, system))
