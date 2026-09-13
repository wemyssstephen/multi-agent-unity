import json
import logging
import shutil
import time
from contextlib import contextmanager
from dataclasses import dataclass, field
from pathlib import Path

from multi_agent_unity.evaluator import run_evaluator_once
from multi_agent_unity.exceptions import TestPollTimeout
from multi_agent_unity.logging_setup import setup_logging
from multi_agent_unity.tasks import get_task, get_tests
from multi_agent_unity.unity_bridge_client import quit_unity
from multi_agent_unity.unity_launcher import launch_unity_headless, wait_for_bridge

log = logging.getLogger("batch")

@dataclass(frozen=True)
class BenchRun:
    """One run of the benchmark, includes the task, the scene, and its tests"""
    genre: str      # sidescroller | topdown
    condition: str  # empty | populated
    scene: str
    test_names: tuple[str, ...]
    save_artifact: bool = False

    @property
    def task_key(self) -> str:
        return f"{self.genre}.{self.condition}"

@dataclass
class BenchConfig:
    runs: list[BenchRun]
    systems: list[str] = field(default_factory=lambda: ["s", "m"])
    reps: int = 5
    model: str = "claude-haiku-4-5-20251001"
    results_path: Path = Path("results/runs.jsonl")
    run_log_directory: Path = Path("results/run_logs")
    artifact_directory: Path = Path("results/artifacts")
    soft_run_ceiling: int = 900

def build_bench_config() -> BenchConfig:
    runs = [
        BenchRun("sidescroller", "empty", "sidescroller_empty",
                 tuple(get_tests("sidescroller"))),
        BenchRun("sidescroller", "populated", "sidescroller_populated",
                 tuple(get_tests("sidescroller"))),
        BenchRun("topdown", "empty", "topdown_empty",
                 tuple(get_tests("topdown"))),
        BenchRun("topdown", "populated", "topdown_populated",
                 tuple(get_tests("topdown"))),
    ]
    return BenchConfig(runs=runs)

# Filesystem:
UNITY_PROJECT = Path("unity")
WORKING_SCENE_FILE = UNITY_PROJECT / "Assets/MultiAgentBridge/Working/current.unity"
WORKING_SCRIPTS_DIRECTORY = UNITY_PROJECT / "Assets/MultiAgentBridge/Working/Scripts"

@contextmanager
def run_log_file(path: Path, level: int = logging.DEBUG):
    path.parent.mkdir(parents=True, exist_ok=True)
    handler = logging.FileHandler(path, mode="w", encoding="utf-8")
    handler.setLevel(level)
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s"))
    root = logging.getLogger()
    root.addHandler(handler)
    try:
        yield
    finally:
        root.removeHandler(handler)
        handler.close()

def tasks_done(results_path: Path) -> set[tuple]:
    done: set[tuple] = set()
    if not results_path.exists():
        return done
    with results_path.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except Exception:
                log.warning("skipping malformed JSON line")
                continue
            key = (rec.get("task_key"), rec.get("system"), rec.get("rep"))
            if None not in key:
                done.add(key)
    return done

def snapshot_scene(dest_dir: Path) -> bool:
    """Copy a finished scene and its scripts for inspection"""
    if not WORKING_SCENE_FILE.exists():
        log.warning("no working scene to snapshot at %s", WORKING_SCENE_FILE)
        return False
    dest_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(WORKING_SCENE_FILE, dest_dir / WORKING_SCENE_FILE.name)
    meta = WORKING_SCENE_FILE.parent / (WORKING_SCENE_FILE.name + ".meta")
    if meta.exists():
        shutil.copy2(meta, dest_dir / meta.name)
    if WORKING_SCRIPTS_DIRECTORY.exists():
        shutil.copytree(WORKING_SCRIPTS_DIRECTORY, dest_dir / "Scripts", dirs_exist_ok=True)
    log.info("Scene snapshot -> %s", dest_dir)
    return True

def _stub_record(run: BenchRun, system: str, model: str) -> dict:
    """Full record for a bench run that threw before completion"""
    return {
        "scene": run.scene, "system": system, "model": model,
        "agent_text": None, "iterations": None, "tool_call_count": None,
        "tool_calls_by_name": None, "hit_cap": None,
        "input_tokens": None, "output_tokens": None,
        "cache_read_input_tokens": None, "cache_creation_input_tokens": None,
        "compile_state": None, "results": {}, "passed": False, "compiled": False,
    }

def run_bench(config: BenchConfig) -> None:
    config.results_path.parent.mkdir(parents=True, exist_ok=True)
    done = tasks_done(config.results_path)
    total = len(config.runs) * len(config.systems) * config.reps
    log.info("bench: %d total, %d already done, %d still to run", total, len(done), total - len(done))

    proc = launch_unity_headless()
    try:
        if not wait_for_bridge():
            log.error("Bridge never started. Aborting bench.")
            return

        with config.results_path.open("a", encoding="utf-8") as out:
            for run in config.runs:
                task = get_task(run.genre, run.condition)
                for system in config.systems:
                    for rep in range(config.reps):
                        key = (run.task_key, system, rep)
                        if key in done:
                            log.info("skip (done): %s", key)
                            continue

                        log.info("RUN start: %s", key)
                        run_log = config.run_log_directory / f"{run.task_key}__{system}__rep{rep:02d}.log"
                        started = time.time()
                        error = None

                        with run_log_file(run_log):
                            try:
                                record = run_evaluator_once(
                                    run.scene, task, list(run.test_names),
                                    system, model=config.model
                                )
                            except TestPollTimeout as e:
                                log.warning("run hung: %s (%s)", key, e)
                                record = _stub_record(run, system, config.model)
                                error = f"TestPollTimeout: {e}"
                            except Exception as e:
                                log.exception("run errored: %s", key)
                                record = _stub_record(run, system, config.model)
                                error = f"{type(e).__name__}: {e}"

                        elapsed = time.time() - started
                        if elapsed > config.soft_run_ceiling:
                            log.warning("run %s over soft ceiling: %.0fs", key, elapsed)

                        artifact = None
                        if run.save_artifact and error is None:
                            dest = config.artifact_directory / f"{run.task_key}__{system}__rep{rep:02d}"
                            if snapshot_scene(dest):
                                artifact = str(dest)

                        record.update({
                            "task_key": run.task_key,
                            "rep": rep,
                            "artifact": artifact,
                            "wall_clock": round(elapsed, 1),
                            "error": error,
                        })
                        out.write(json.dumps(record) + "\n")
                        out.flush()
                        done.add(key)
                        log.info("RUN done: %s passed=%s %.0fs",
                                 key, record.get("passed"), elapsed)
    finally:
        _shutdown(proc)

def _shutdown(proc) -> None:
    log.info("Shutting down Unity...")
    try:
        quit_unity()
        proc.wait(timeout=30)
        log.info("Unity exited.")
    except Exception:
        log.warning("Clean quit failed. Terminating process...")
        proc.terminate()

if __name__ == "__main__":
    setup_logging(console_level=logging.INFO, file_level=logging.DEBUG,
                  logfile="results/batch.log")
    run_bench(build_bench_config())
