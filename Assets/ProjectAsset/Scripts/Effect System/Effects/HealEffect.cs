using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible healing effect. Restores HP to the target character up to their maxHp.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHealEffect", menuName = "Project Timeline/Effects/Heal Effect")]
    public class HealEffect : CombatEffect
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
            if (model.simulatedCharacters.TryGetValue(targetId, out CharacterData tgt))
            {
                int oldHp = tgt.currentHp;
                tgt.currentHp = Mathf.Min(tgt.maxHp, tgt.currentHp + value);
                logs.Add($"  -> [HEAL EFFECT] {tgt.name} is healed for {value} HP. (HP: {oldHp}->{tgt.currentHp})");
            }
        }
    }
}
