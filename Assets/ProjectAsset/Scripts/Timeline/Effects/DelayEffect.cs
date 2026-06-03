using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible delay effect. Triggers timeline shift on target characters' scheduled actions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDelayEffect", menuName = "Project Timeline/Effects/Delay Effect")]
    public class DelayEffect : CombatEffect
    {
        public override EffectPhase Phase => EffectPhase.Delay;

        [Header("Delay Settings")]
        public bool isTargetedDelay;
        public ActionType targetActionType;
        public DelayTargetMode delayTargetMode = DelayTargetMode.ByActionType;

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
            model.ApplyDelay(sourceId, targetId, slot, value, isTargetedDelay, targetActionType, delayTargetMode, logs);
        }
    }
}
