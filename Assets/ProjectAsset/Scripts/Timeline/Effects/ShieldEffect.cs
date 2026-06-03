using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible shield effect. Adds defensive shields to target characters.
    /// </summary>
    [CreateAssetMenu(fileName = "NewShieldEffect", menuName = "Project Timeline/Effects/Shield Effect")]
    public class ShieldEffect : CombatEffect
    {
        public override EffectPhase Phase => EffectPhase.Defense;

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
            if (model.simulatedCharacters.TryGetValue(sourceId, out CharacterData src) &&
                model.simulatedCharacters.TryGetValue(targetId, out CharacterData tgt))
            {
                tgt.AddShield(value);
                logs.Add($"  -> [SHIELD EFFECT] {src.name} adds {value} shield to {tgt.name} (Shield: {tgt.shield})");
            }
        }
    }
}
