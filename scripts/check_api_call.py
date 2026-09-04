
from multi_agent_unity.evaluator import run_evaluator_once

if __name__ == "__main__":
    task = ("Create a simple top-down game. Containing a Player GameObject that moves "
            "using a PlayerMover script containing "
            "a Move(Vector2 input) method that moves the "
            "Player in the direction of input. Write the "
            "script to Assets/MultiAgentBridge/Working/Scripts/. "
            "Use the sprites in Scenes/Sprites/ to build a very basic level.")
    print(run_evaluator_once("topdown_empty", task, "MoveTest", "s"))
