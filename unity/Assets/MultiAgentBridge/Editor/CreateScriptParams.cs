namespace MultiAgentBridge
{
    /// <summary>Args for the create_script tool. ScriptName determines the filename and class. TargetPath determines the directory.</summary>
    public class CreateScriptParams
    {
        public string ScriptName { get; set; }
        public string TargetPath { get; set; }
        public string ScriptContent { get; set; }
    }
}