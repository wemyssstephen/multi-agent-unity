
import asyncio

from multi_agent_unity.agent import agent_loop_handler

if __name__ == "__main__":
    result = asyncio.run(agent_loop_handler("Create a cube and attach a C# script to it", "m"))
    print(result)
