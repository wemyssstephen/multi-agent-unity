using Newtonsoft.Json.Linq;

namespace MultiAgentBridge
{
    public class SetPropertyParams
    {
        public string GameObjectId { get; set; }
        public string ComponentType { get; set; }
        public string PropertyPath { get; set; }
        public JToken Value { get; set; }
        public string ReferenceComponentType { get; set; } // Optional
    }
}