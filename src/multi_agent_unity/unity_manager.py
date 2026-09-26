import subprocess
import logging
import time

import requests

from multi_agent_unity.bridge_manager import post, post_quit
from multi_agent_unity.config import Bridge, BenchPaths

log = logging.getLogger("unity_manager")

def launch_unity_headless() -> subprocess.Popen:
    """Launch Unity headless with the bridge, logging to a known path."""
    BenchPaths.unity_log.write_text("")
    args = [
        str(BenchPaths.unity_exe),
        "-batchmode",
        "-projectPath", str(BenchPaths.unity_project),
        "-logFile", str(BenchPaths.unity_log),
        "-accept-apiupdate",
    ]
    proc = subprocess.Popen(args)
    return proc

def quit_unity() -> None:
    """Ask Unity to exit"""
    post_quit({"Name": "quit", "Args": {}})

def wait_for_unity() -> bool:
    """Block until the bridge answers, or give up."""
    deadline = time.monotonic() + Bridge.startup_wait
    while time.monotonic() < deadline:
        if check_compile() in ("ready", "compiling", "errors"):
            return True
    return False

def request_compile() -> str:
    result = post({"Name": "request_compile", "Args": {}})
    log.info("request_compile -> %s", result)
    return result

def check_compile() -> str:
    return post({"Name": "check_compile", "Args": {}})

def refresh_database() -> str:
    result = post({"Name": "refresh_database", "Args": {}})
    log.info("refresh_database -> %s", result)
    return result

def wait_for_compile() -> str:
    """Waits for Unity to finish compiling scripts."""
    log.info("Waiting for Unity to compile...")
    time.sleep(0.25)  # Initial wait to allow Unity to compile
    for _ in range(Bridge.compile_wait):
        try:
            result = check_compile()
        except requests.exceptions.ConnectionError:
            log.info("Connection error... waiting...")
            time.sleep(1)
            continue

        if result == "ready":
            log.info("Compile successful...")
            return "ready"
        elif result == "errors":
            log.warning("wait_for_compile -> errors")
            return "errors"
        time.sleep(1)
    log.warning("wait_for_compile -> timeout")
    return "timeout"

