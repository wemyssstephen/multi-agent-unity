import asyncio
import json
import logging
import shutil
from datetime import datetime

from multi_agent_unity.agent import agent_loop_handler
from multi_agent_unity.config import BenchPaths, Bridge
from multi_agent_unity.exceptions import BridgeTimeout, TestPollTimeout, UnityStartupError
from multi_agent_unity.logging_setup import setup_logging
from multi_agent_unity.scene_manager import prepare_scene, save_scene
from multi_agent_unity.tasks import get_task, get_tests
from multi_agent_unity.test_manager import await_tests, run_tests
from multi_agent_unity.unity_manager import launch_unity_headless, quit_unity, request_compile, wait_for_compile, wait_for_unity

log = logging.getLogger("bench")

class Bench:
    model = "claude-haiku-4-5-20251001"
    systems = ["s", "m"]
    results_path = BenchPaths.results / "runs.jsonl"
    artefacts_path = BenchPaths.results / "artefacts"

    iteration_budget = 120
    reps = 5

    runs = [
        ("sidescroller", "empty",     "sidescroller_empty"),
        ("sidescroller", "populated", "sidescroller_populated"),
        ("topdown",      "empty",     "topdown_empty"),
        ("topdown",      "populated", "topdown_populated"),
    ]

    def _start_unity(self):
        proc = launch_unity_headless()
        if not wait_for_unity():
            proc.terminate()
            raise UnityStartupError(
                f"Unity did not answer within {Bridge.startup_wait}s of launching")
        return proc

    def _restart_unity(self, proc):
        proc.terminate()
        proc.wait(timeout=30)
        return self._start_unity()

    def _snapshot(self, scene, system, rep):
        dest = self.artefacts_path / scene / system / f"rep{rep}"
        if dest.exists():
            shutil.rmtree(dest)
        dest.mkdir(parents=True)

        for name in ("current.unity", "current.unity.meta"):
            source = BenchPaths.working_dir / name
            if source.exists():
                shutil.copy2(source, dest / name)

        if BenchPaths.working_scripts.exists():
            shutil.copytree(BenchPaths.working_scripts, dest / "Scripts", dirs_exist_ok=True)

    def run(self):
        self.results_path.parent.mkdir(parents=True, exist_ok=True)
        proc = self._start_unity()
        try:
            with self.results_path.open("a", encoding="utf-8") as out:
                for genre, condition, scene in self.runs:
                    for system in self.systems:
                        for rep in range(self.reps):
                            try:
                                record = self.score(genre, condition, scene, system, rep)
                            except BridgeTimeout:
                                log.exception(
                                    "Bridge timed out: scene=%s, system=%s, rep=%s",
                                    scene, system, rep)
                                record = self._record(scene, system, rep, reason="timeout")
                                proc = self._restart_unity(proc)
                            except TestPollTimeout:
                                log.exception(
                                    "Test poll timed out: scene=%s, system=%s, rep=%s",
                                    scene, system, rep)
                                record = self._record(scene, system, rep, compiled=True,
                                                      reason="test_timeout")
                                proc = self._restart_unity(proc)
                            except Exception as e:
                                log.exception(
                                    "Error - moving to next rep: scene=%s, system=%s, rep=%s",
                                    scene, system, rep)
                                while isinstance(e, BaseExceptionGroup):
                                    e = e.exceptions[0]
                                record = self._record(scene, system, rep, reason=type(e).__name__)
                            out.write(json.dumps(record) + "\n")
                            out.flush()
        finally:
            quit_unity()
            proc.terminate()

    def score(self, genre, condition, scene, system, rep):
        prepare_scene(scene)
        task = get_task(genre, condition)
        agent_result = asyncio.run(agent_loop_handler(
            task, system, model=self.model, iteration_budget=self.iteration_budget))
        save_scene()
        self._snapshot(scene, system, rep)

        request_compile()
        if wait_for_compile() != "ready":
            return self._record(scene, system, rep, **agent_result)

        started = run_tests(get_tests(genre))
        if started.startswith("Busy"):
            raise TestPollTimeout("previous test run still active")
        suite = await_tests()
        results = suite["results"]

        passed = (suite["status"] == "finished" and all(s== "Passed" for s in results.values()))
        log.info("scene=%s system=%s passed=%s", scene, system, passed)
        record = self._record(scene, system, rep, passed=passed, compiled=True,
                              results=results, messages=suite.get("messages", {}), **agent_result)
        if suite["status"] == "error":
            record["test_error"] = suite.get("error")
        return record

    def _record(self, scene, system, rep, *, passed=False, compiled=False,
                reason=None, **extra):
        """Builds one results row."""
        record = {"scene": scene, "system": system, "rep": rep, "model": self.model,
                  "passed": passed, "compiled": compiled}
        if reason is not None:
            record["error"] = True
            record["reason"] = reason
        record.update(extra)
        return record

if __name__ == "__main__":
    b = Bench()
    timestamp = f"{datetime.now():%Y%m%d_%H%M%S}"
    b.results_path = BenchPaths.results / f"runs_{timestamp}.jsonl"
    b.artefacts_path = BenchPaths.results / f"artefacts_{timestamp}"
    setup_logging(logfile=BenchPaths.results / f"batch_{timestamp}.log")
    b.reps = 1
    b.model = "claude-opus-5-5"
    b.run()
