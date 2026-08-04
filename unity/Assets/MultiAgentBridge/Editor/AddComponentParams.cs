namespace MultiAgentBridge
{
    /// <summary>Args for the add_component tool. GameObjectId identifies the target; ComponentType names the component to add.</summary>
    public class AddComponentParams
    {
        public string GameObjectId { get; set; }
        public string ComponentType { get; set; }
    }
}