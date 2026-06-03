using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    public enum EffectPhase
    {
        Delay,      // First phase: delays, speed changes, timeline manipulation
        Defense,    // Second phase: shields, blocks, defensive buffs
        Attack,     // Third phase: damage, offensive hits
        Utility     // Fourth phase: healing, card draws, custom effects
    }

    /// <summary>
    /// Base class for modular ScriptableObject combat effects.
    /// Enables extensible action configurations on the timeline.
    /// </summary>
    public abstract class CombatEffect : ScriptableObject
    {
        /// <summary>
        /// The phase of execution when this effect should run during the chronological slot simulation.
        /// </summary>
        public abstract EffectPhase Phase { get; }

        /// <summary>
        /// Executes the effect on the characters/timeline.
        /// </summary>
        public abstract void Execute(
            CharacterID sourceId, 
            CharacterID targetId, 
            int value, 
            TimelineModel model, 
            int slot, 
            ActionNodeData node, 
            List<string> logs
        );
    }
}
