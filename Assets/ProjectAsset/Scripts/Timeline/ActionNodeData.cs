using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Model representation of a timeline action segment.
    /// Marked serializable so it can be exposed and configured directly inside the Unity Inspector.
    /// </summary>
    [Serializable]
    public class ActionNodeData
    {
        public string id;
        public CharacterID sourceId;
        public CharacterID targetId;
        public int startSlot; // Index of slot (0 to 4)
        public int effectiveSlot; // Where the action actually executes after delays (dynamic)
        public bool isExclusive; // True: Main Action (cannot overlap), False: Free Action (can overlap)
        public int value; // Damage amount, Shield amount, Delay slots

        [Header("Modular Action Effects")]
        public List<CombatEffect> effects = new List<CombatEffect>();

        [Header("Combo Card Settings")]
        public CardType cardType = CardType.Normal;
        public List<CombatEffect> comboEffects = new List<CombatEffect>();
        public float comboValueMultiplier = 1f;
        public int comboValueBonus = 0;

        [Header("Speed/Priority Settings")]
        public CardSpeed cardSpeed = CardSpeed.Normal;

        /// <summary>
        /// Derived ActionType based on custom effects for legacy compatibility.
        /// </summary>
        public ActionType actionType
        {
            get
            {
                if (effects != null && effects.Count > 0)
                {
                    foreach (var effect in effects)
                    {
                        if (effect is DamageEffect) return ActionType.Attack;
                        if (effect is ShieldEffect) return ActionType.Defend;
                        if (effect is DelayEffect) return ActionType.Delay;
                    }
                }
                return ActionType.Attack; // Default fallback
            }
        }

        public ActionNodeData() { }

        public ActionNodeData(string id, CharacterID sourceId, CharacterID targetId, int startSlot, ActionType legacyType, int value, bool isExclusive = true)
        {
            this.id = id;
            this.sourceId = sourceId;
            this.targetId = targetId;
            this.startSlot = startSlot;
            this.effectiveSlot = startSlot; // Default to start slot
            this.isExclusive = isExclusive;
            this.value = value;
            this.cardSpeed = CardSpeed.Normal;

            // Automatically populate modular effects for programmatically created actions
            switch (legacyType)
            {
                case ActionType.Attack:
                    effects.Add(ScriptableObject.CreateInstance<DamageEffect>());
                    break;
                case ActionType.Defend:
                    effects.Add(ScriptableObject.CreateInstance<ShieldEffect>());
                    break;
                case ActionType.Delay:
                    var delayEffect = ScriptableObject.CreateInstance<DelayEffect>();
                    delayEffect.isTargetedDelay = false;
                    effects.Add(delayEffect);
                    break;
            }
        }

        /// <summary>
        /// Deep copies the action node configuration.
        /// </summary>
        public ActionNodeData Clone()
        {
            var cloned = new ActionNodeData
            {
                id = this.id,
                sourceId = this.sourceId,
                targetId = this.targetId,
                startSlot = this.startSlot,
                effectiveSlot = this.effectiveSlot,
                isExclusive = this.isExclusive,
                value = this.value,
                cardType = this.cardType,
                comboValueMultiplier = this.comboValueMultiplier,
                comboValueBonus = this.comboValueBonus,
                cardSpeed = this.cardSpeed
            };
            if (this.effects != null)
            {
                cloned.effects = new List<CombatEffect>(this.effects);
            }
            if (this.comboEffects != null)
            {
                cloned.comboEffects = new List<CombatEffect>(this.comboEffects);
            }
            return cloned;
        }
    }
}
