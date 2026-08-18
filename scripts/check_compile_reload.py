import json
import time
import urllib.request
import urllib.error

BRIDGE_URL = "http://localhost:8080/"

SCRIPT_CONTENT = """using UnityEngine;
using System.Collections.Generic;

public class ProbeController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 100f;
    private List<Vector3> history = new List<Vector3>();

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0f, v).normalized * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        history.Add(transform.position);
        if (history.Count > 1000) history.RemoveAt(0);
    }

    public void ResetPosition()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        history.Clear();
    }
}
"""

request = {
    "Name": "create_script",
    "Args": {
        "TargetPath": "Assets",
        "ScriptName": "ProbeController",
        "ScriptContent": SCRIPT_CONTENT,
    },
}

body = json.dumps(request).encode("utf-8")

print("Sending create_script ...")
start = time.monotonic()

try:
    with urllib.request.urlopen(BRIDGE_URL, data=body, timeout=60) as resp:
        text = resp.read().decode("utf-8")
        elapsed = time.monotonic() - start
        print(f"RESPONSE after {elapsed:.2f}s (status {resp.status}):")
        print(text)
except urllib.error.URLError as e:
    elapsed = time.monotonic() - start
    print(f"URLError after {elapsed:.2f}s: {e}")
except Exception as e:
    elapsed = time.monotonic() - start
    print(f"{type(e).__name__} after {elapsed:.2f}s: {e}")