using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible status application effect. Adds buffs/debuffs to the target character.
    /// </summary>
    [CreateAssetMenu(fileName = "NewApplyStatusEffect", menuName = "Project Timeline/Effects/Apply Status Effect")]
    public class ApplyStatusEffect : CombatEffect
    {
        public override EffectPhase Phase => EffectPhase.Utility;

        [Header("Status Configuration")]
        [Tooltip("The Status Effect asset definition.")]
        public StatusEffectSO status;

        public int duration = 2;
        public int intensity = 1;

        [Tooltip("If true, uses the Action Node's value for the intensity instead of the field above.")]
        public bool useNodeValueForIntensity = false;

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
            if (status == null) return;

            if (model.simulatedCharacters.TryGetValue(targetId, out CharacterData tgt))
            {
                int finalIntensity = useNodeValueForIntensity ? value : intensity;
                tgt.ApplyStatus(status, duration, finalIntensity);
                logs.Add($"  -> [STATUS EFFECT] Applied {status.displayName} (Dur: {duration}, Value: {finalIntensity}) to {tgt.name}");
            }
        }
    }
}
