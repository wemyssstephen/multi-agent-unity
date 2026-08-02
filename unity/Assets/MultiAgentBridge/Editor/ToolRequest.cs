using Newtonsoft.Json.Linq;

namespace MultiAgentBridge
{
    /// <summary>
    /// A parsed tool request. Name selects the handler. Args is a JSON object that the handler deserializes into its own params class.
    /// </summary>
    public class ToolRequest
    {
        public string Name { get; set; }
        public JObject Args { get; set; }
    }
}