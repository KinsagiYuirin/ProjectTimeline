using System;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Pure C# class representing a single card instance during gameplay.
    /// Manages unique runtime instance IDs linked to static CardDataBlueprints.
    /// </summary>
    public class RuntimeCardInstance
    {
        // Unique ID representing this specific card instance (e.g. player_slash_9abf83c1)
        public string instanceId { get; private set; }

        // Reference to the original CardDataBlueprint definition
        public CardDataBlueprint blueprint { get; private set; }

        public RuntimeCardInstance(CardDataBlueprint blueprint)
        {
            this.blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            
            // Generate a unique instance ID to isolate this action from identical cards on the timeline
            this.instanceId = $"{blueprint.cardId}_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }
}
