
from multi_agent_unity.evaluator import run_evaluator_once

if __name__ == "__main__":
    task = ("Create a simple side-scroller game. Containing a Player GameObject that moves "
            "and jumps, using a PlayerMover script containing "
            "a Move(Vector2 input) method that moves the "
            "Player in the direction of input and a Jump(Vector2 input) method that handles jumping. "
            "Write the script to Assets/MultiAgentBridge/Working/Scripts/. "
            "Use Sprites for the game by using the list_sprites tool. ")
    tests = ["MoveTest", "JumpTest", "WallCollideTest"]

    print(run_evaluator_once("sidescroller_empty", task, tests, "s"))
