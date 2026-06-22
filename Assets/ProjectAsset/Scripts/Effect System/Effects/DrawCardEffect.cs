using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible card drawing effect. Adds bonus draws to the player for their next planning phase.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDrawCardEffect", menuName = "Project Timeline/Effects/Draw Card Effect")]
    public class DrawCardEffect : CombatEffect
    {
        public override EffectPhase Phase => EffectPhase.Utility;

        public override void Execute(
            CharacterID sourceId, 
            CharacterID targetId, 
            int value, 
            TimelineModel model, 
            int slot, 
            ActionNodeData node, 
            List<string> logs
        )
        {
            // Card drawing is only valid for the Player character
            if (targetId == CharacterID.Player)
            {
                model.bonusDrawNextTurn += value;
                logs.Add($"  -> [DRAW EFFECT] Player schedules drawing {value} cards at start of next turn.");
            }
        }
    }
}
