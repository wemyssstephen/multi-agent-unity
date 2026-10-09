# Evaluating Multi-Agent Systems in Video Game Development

A benchmark that compares a multi-agent LLM system with a single-agent baseline on game development tasks inside a live Unity Editor. Built for my MSc Computer Science final project at Birkbeck, University of London.

Both systems use the same model, task prompt and tools, so agent architecture is the only variable. Each run starts from a clean scene, the agents build a small 2D game through a set of Unity tools, and hidden PlayMode tests decide whether the game works.

## How it works

The project has three parts.

- **Agent harness** (`src/multi_agent_unity/agent.py`, `agent_classes.py`). A Python reason–act–observe loop that calls the Anthropic Messages API directly. It runs either a single agent or an orchestrator that delegates to Scene, Script and Console workers.
- **Unity bridge** (`mcp_server.py`, `unity/Assets/MultiAgentBridge/Editor/`). An MCP server offers the agent tools and forwards each call over HTTP to a C# plugin inside the Unity Editor, which runs it on Unity's main thread.
- **Evaluation benchmark** (`bench.py`, `repl.py`, `analyse.py`). Bench resets Unity, runs each cell, snapshots the result, compiles it and runs the hidden tests. The REPL configures and runs batches, and `analyse.py` reports Pass@k, Pass^k, cost and per-test results.

The task prompts and test lists are in `src/multi_agent_unity/tasks.toml`. The tests are in `unity/Assets/MultiAgentBridge/Tests/`.

## Requirements

- Windows
- Unity 6 (6000.5.6f1)
- Python 3.12 or later
- An Anthropic API key

## Setup

1. Clone the repository and open the `unity` folder once in Unity Hub, so Unity imports the project.
2. From the repository root, create a virtual environment and install the package:

   ```
   python -m venv .venv
   .venv\Scripts\activate
   pip install -e ".[dev]"
   ```

3. Create a `.env` file in the repository root containing `ANTHROPIC_API_KEY=...`. If Unity is not installed at the default Hub path, also set `UNITY_EXE` to the location of `Unity.exe`.

## Running a batch

Close the Unity Editor first. Bench launches its own headless Editor.

```
python -m multi_agent_unity.repl
```

The REPL asks for the model, the systems, the scenes and the number of repetitions, shows the batch and its estimated cost, and starts on confirmation. Results, artefact snapshots and a log are written to `results/`, each named with the batch's timestamp. Choose "Analyse a results file" to produce the analysis for any results file.

## Tests

```
ruff check .
pytest
```
