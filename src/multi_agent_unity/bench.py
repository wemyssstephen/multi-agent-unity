import asyncio
import json
import logging
import time
from datetime import datetime
from pathlib import Path

from multi_agent_unity.agent import agent_loop_handler
from multi_agent_unity.exceptions import BridgeTimeout
from multi_agent_unity.logging_setup import setup_logging
from multi_agent_unity.scene_manager import prepare_scene
from multi_agent_unity.tasks import get_task, get_tests
from multi_agent_unity.unity_bridge_client import (
    poll_test_result,
    quit_unity,
    request_compile,
    run_tests,
    save_scene,
    wait_for_compile,
)
from multi_agent_unity.unity_launcher import launch_unity_headless, wait_for_bridge

log = logging.getLogger("bench")

class Bench:
    model = "claude-haiku-4-5-20251001"
    max_iterations = 30
    reps = 5
    systems = ["s", "m"]
    results_path = Path("results/runs.jsonl")

    runs = [
        ("sidescroller", "empty",     "sidescroller_empty"),
        ("sidescroller", "populated", "sidescroller_populated"),
        ("topdown",      "empty",     "topdown_empty"),
        ("topdown",      "populated", "topdown_populated"),
    ]

    def _start_unity(self):
        proc = launch_unity_headless()
        wait_for_bridge()
        return proc

    def _restart_unity(self, proc):
        proc.terminate()
        proc.wait(timeout=30)
        return self._start_unity()

    def run(self):
        self.results_path.parent.mkdir(parents=True, exist_ok=True)
        proc = self._start_unity()
        try:
            with self.results_path.open("a", encoding="utf-8") as out:
                for genre, condition, scene in self.runs:
                    for system in self.systems:
                        for rep in range(self.reps):
                            try:
                                record = self.score(genre, condition, scene, system)
                            except BridgeTimeout:
                                log.exception(
                                    "Bridge timed out: scene=%s, system=%s, rep=%s",
                                    scene, system, rep)
                                record = {"scene": scene, "system": system, "passed": False,
                                      "compiled": False, "error": True, "reason": "timeout"}
                                proc = self._restart_unity(proc)
                            record["rep"] = rep
                            out.write(json.dumps(record) + "\n")
                            out.flush()
        finally:
            quit_unity()
            proc.terminate() # TODO: actually implement Unity quitting

    def score(self, genre, condition, scene, system):
        prepare_scene(scene)
        task = get_task(genre, condition)
        agent_result = asyncio.run(agent_loop_handler(
            task, system, model=self.model, max_iterations=self.max_iterations))
        save_scene()

        request_compile()
        if wait_for_compile() != "ready":
            return {"scene": scene, "system": system, "passed": False, "compiled": False}

        results = {}
        for test in get_tests(genre):
            run_tests(test)
            while (state := poll_test_result()) == "running":
                time.sleep(1)
            results[test] = state

        passed = all(s == "Passed" for s in results.values())
        log.info("scene=%s system=%s passed=%s", scene, system, passed)
        return {"scene": scene, "system": system, "passed": passed,
                "compiled": True, "results": results, **agent_result}

if __name__ == "__main__":
    b = Bench()
    logfile = f"results/batch_{datetime.now():%Y%m%d_%H%M%S}.log"
    setup_logging(logfile=logfile)
    b.runs = [("topdown", "empty", "topdown_empty")]
    b.systems = ["s"]
    b.reps = 1
    b.run()
