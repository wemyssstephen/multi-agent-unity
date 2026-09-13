import subprocess
import time
from pathlib import Path

import requests

from multi_agent_unity.unity_bridge_client import check_compile

UNITY_EXE = Path(r"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe")
PROJECT_PATH = Path(r"C:/dev/multi-agent-unity/unity")
UNITY_LOG = Path(r"C:/dev/multi-agent-unity/unity_headless.log")

def wait_for_bridge(timeout=120) -> bool:
    """Block until the headless bridge answers, or give up."""
    for _ in range(timeout):
        try:
            if check_compile() in ("ready", "compiling", "errors"):
                return True
        except requests.exceptions.ConnectionError:
            pass
        time.sleep(1)
    return False

def launch_unity_headless() -> subprocess.Popen:
    """Launch Unity headless with the bridge, logging to a known path."""
    args = [
        str(UNITY_EXE),
        "-batchmode",
        "-projectPath", str(PROJECT_PATH),
        "-logFile", str(UNITY_LOG),
        "-accept-apiupdate",
    ]
    proc = subprocess.Popen(args)
    return proc
