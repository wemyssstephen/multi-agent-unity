class Agent:
    name = None
    worker_names: list[str] = []
    tool_names: list[str] = []
    system_prompt: str | None = None

    def __init__(self, model, tools, all_tools, system_prompt=None):
        self.model = model
        self.tools = tools
        self.all_tools = all_tools
        self.system_prompt = system_prompt

    @classmethod
    def make(cls, model, all_tools):
        tools = [t for t in all_tools if t["name"] in cls.tool_names]
        return cls(model=model, tools=tools, all_tools=all_tools, system_prompt=cls.system_prompt)

    def build_worker(self, worker_name):
        return WORKER_CLASSES[worker_name].make(self.model, self.all_tools)

class SingleAgent(Agent):
    name = "single_agent"
    system_prompt = "You receive a plain-language Unity task that you must complete."
    tool_names = ["create_gameobject", "create_script", "add_component",
                  "assign_sprite", "set_property", #"create_primitive",
                  "read_scene", "read_script", "read_console"]

class OrchestratorAgent(Agent):
    name = "orchestrator"
    system_prompt = ("You receive a plain-language Unity task, break it into steps, "
                     "and delegate each to a specialist. The Scene Agent for GameObjects "
                     "and hierarchy, the Script Agent for C# scripts, and the Console "
                     "Agent for compilation errors. When a step fails, delegate to the "
                     "Console Agent to read errors before trying again.")
    worker_names = ["scene_agent", "script_agent", "console_agent"]
    worker_schemas = [
        {"name": "scene_agent",
         "description": "Delegate a scene / GameObject task. Give it a clear instruction and any relevant GlobalObjectIds.",
         "input_schema": {"type": "object",
                          "properties": {"task": {"type": "string"}},
                          "required": ["task"]}},
        {"name": "script_agent",
         "description": "Delegate writing or revising a C# script.",
         "input_schema": {"type": "object",
                          "properties": {"task": {"type": "string"}},
                          "required": ["task"]}},
        {"name": "console_agent",
         "description": "Delegate reading compilation output and reporting errors.",
         "input_schema": {"type": "object",
                          "properties": {"task": {"type": "string"}},
                          "required": ["task"]}},
    ]

    @classmethod
    def make(cls, model, all_tools):
        return cls(model=model, tools=cls.worker_schemas, all_tools=all_tools, system_prompt=cls.system_prompt)

class SceneAgent(Agent):
    name = "scene_agent"
    tool_names = ["create_gameobject", #"create_primitive",
                  "assign_sprite",
                  "set_property", "add_component", "read_scene"]

class ScriptAgent(Agent):
    name = "script_agent"
    tool_names = ["create_script", "read_script"]

class ConsoleAgent(Agent):
    name = "console_agent"
    tool_names = ["read_console"]

WORKER_CLASSES = {  "single_agent":   SingleAgent,
                    "orchestrator":   OrchestratorAgent,
                    "scene_agent":    SceneAgent,
                    "script_agent":   ScriptAgent,
                    "console_agent":  ConsoleAgent
                    }
