using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Extensible damage effect. Applies Weak/Vulnerable multipliers during calculation.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDamageEffect", menuName = "Project Timeline/Effects/Damage Effect")]
    public class DamageEffect : CombatEffect
    {
        public override EffectPhase Phase => EffectPhase.Attack;

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
                int dmg = value;

                // Check if source is Weak (reduces outgoing damage by 25%)
                if (src.statusEffects != null)
                {
                    var weak = src.statusEffects.Find(s => s.effectType == StatusEffectType.Weak);
                    if (weak != null && weak.duration > 0)
                    {
                        int oldDmg = dmg;
                        dmg = Mathf.Max(0, Mathf.RoundToInt(dmg * 0.75f));
                        logs.Add($"     * Weak reduces {src.name}'s damage: {oldDmg} -> {dmg}");
                    }
                }

                int oldHp = tgt.currentHp;
                int oldShield = tgt.shield;
                tgt.TakeDamage(dmg, true); // Direct hit: will apply Vulnerable check in CharacterData.TakeDamage
                logs.Add($"  -> [DAMAGE EFFECT] {src.name} attacks {tgt.name} for {dmg} dmg. (Shield: {oldShield}->{tgt.shield}, HP: {oldHp}->{tgt.currentHp})");
            }
        }
    }
}
