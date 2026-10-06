import asyncio
import json
import logging
import shutil
import time
from datetime import datetime
from itertools import product

from multi_agent_unity.agent import agent_loop_handler
from multi_agent_unity.config import BenchPaths, Bridge
from multi_agent_unity.exceptions import BridgeTimeout, TestPollTimeout, UnityStartupError
from multi_agent_unity.logging_setup import setup_logging
from multi_agent_unity.scene_manager import prepare_scene, save_scene
from multi_agent_unity.tasks import get_task, get_tests
from multi_agent_unity.test_manager import await_tests, run_tests
from multi_agent_unity.unity_manager import launch_unity_headless, quit_unity, request_compile, wait_for_compile, wait_for_unity
from multi_agent_unity import repl_messages

log = logging.getLogger("bench")

class Bench:
    """Runs every cell of the benchmark in a headless Unity instance and writes one JSONL row per cell."""

    # Config
    model = "claude-opus-5-5"
    systems = ["s", "m"]
    iteration_budget = 120
    reps = 5
    console_level = logging.INFO

    # Run parameters (genre, condition, scene template)
    runs = [
        ("sidescroller", "empty",     "sidescroller_empty"),
        ("sidescroller", "populated", "sidescroller_populated"),
        ("topdown",      "empty",     "topdown_empty"),
        ("topdown",      "populated", "topdown_populated"),
    ]

    def __init__(self):
        # Each batch gets timestamped results, artefacts and logfile
        timestamp = f"{datetime.now():%Y%m%d_%H%M%S}"
        self.results_path = BenchPaths.results / f"runs_{timestamp}.jsonl"
        self.artefacts_path = BenchPaths.results / f"artefacts_{timestamp}"
        self.log_path = BenchPaths.results / f"batch_{timestamp}.log"

    def _start_unity(self):
        """Launches Unity and waits for the bridge. Returns the running process."""
        unity_process = launch_unity_headless()
        if not wait_for_unity():
            unity_process.terminate()
            raise UnityStartupError(
                f"Unity did not answer within {Bridge.startup_wait}s of launching")
        return unity_process

    def _restart_unity(self):
        """Replaces a stuck Unity with a new one."""
        self.unity_process.terminate()
        self.unity_process.wait(timeout=30)
        self.unity_process = self._start_unity()

    def _snapshot(self, scene, system, rep):
        """Copies the agent produced scene and scripts for hand-scoring and debugging"""
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
        """Run the whole benchmark batch."""
        setup_logging(console_level=self.console_level, logfile=self.log_path)
        self.results_path.parent.mkdir(parents=True, exist_ok=True)
        cells = list(product(self.runs, self.systems, range(self.reps)))
        
        repl_messages.message(f"Beginning benchmark... {len(cells)} runs with {self.model}")
        self.unity_process = self._start_unity()
        repl_messages.message("Unity running...")
        try:
            with self.results_path.open("a", encoding="utf-8") as out:
                for number, ((genre, condition, scene), system, rep) in enumerate(cells, start=1):
                    repl_messages.cell_started(number, len(cells), genre, condition, system, rep)
                    started = time.monotonic()
                    record = self._run_cell(genre, condition, scene, system, rep)
                    repl_messages.cell_finished(number, len(cells), genre, condition, system, rep, record, time.monotonic() - started)
                    out.write(json.dumps(record) + "\n")
                    out.flush()
        finally:
            repl_messages.message("Shutting down Unity.")
            quit_unity()
            self.unity_process.terminate()

    def score(self, genre, condition, scene, system, rep):
        """Scores one cell of the benchmark batch. Resets the scene, runs the agent, and then run the tests."""
        prepare_scene(scene)
        task = get_task(genre, condition)
        agent_result = asyncio.run(agent_loop_handler(
            task, system, model=self.model, iteration_budget=self.iteration_budget))
        save_scene()

        # Take a snapshot before running tests.
        self._snapshot(scene, system, rep)

        # Code that does not compile cannot be tested, so we check.
        request_compile()
        if wait_for_compile() != "ready":
            return self._record(scene, system, rep, **agent_result)

        # Run tests
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

    def _run_cell(self, genre, condition, scene, system, rep):
        """Runs one cell of the benchmark"""
        try:
            return self.score(genre, condition, scene, system, rep)
        except BridgeTimeout:
            # Unity has crashed or stopped responding, restart it.
            log.exception("Bridge timed out: scene=%s, system=%s, rep=%s", scene, system, rep)
            self._restart_unity()
            return self._record(scene, system, rep, reason="timeout")
        except TestPollTimeout:
            # Unity Test Framework got stuck, restart Unity.
            log.exception("Test poll timed out: scene=%s, system=%s, rep=%s", scene, system, rep)
            self._restart_unity()
            return self._record(scene, system, rep, compiled=True, reason="test_timeout")
        except Exception as e:
            log.exception("Error - moving to next rep: scene=%s, system=%s, rep=%s", scene, system, rep)
            # Agent errors arrive wrapped by the MCP client. Extract and record the real error.
            while isinstance(e, BaseExceptionGroup):
                e = e.exceptions[0]
            return self._record(scene, system, rep, reason=type(e).__name__)

if __name__ == "__main__":
    Bench().run()
