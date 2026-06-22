using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Model representation of a combatant's attributes.
    /// Marked serializable so it can be exposed and configured directly inside the Unity Inspector.
    /// </summary>
    [Serializable]
    public class CharacterData
    {
        public string id;
        public string name;
        public int maxHp;
        public int currentHp;
        public int shield;
        public List<StatusEffectInstance> statusEffects = new List<StatusEffectInstance>();

        public CharacterData() { }

        public CharacterData(string id, string name, int maxHp, int currentHp, int shield)
        {
            this.id = id;
            this.name = name;
            this.maxHp = maxHp;
            this.currentHp = currentHp;
            this.shield = shield;
        }

        /// <summary>
        /// Creates a deep copy of the CharacterData for simulation previews.
        /// </summary>
        public CharacterData Clone()
        {
            var cloned = new CharacterData(id, name, maxHp, currentHp, shield);
            if (statusEffects != null)
            {
                foreach (var status in statusEffects)
                {
                    cloned.statusEffects.Add(status.Clone());
                }
            }
            return cloned;
        }

        /// <summary>
        /// Applies damage, taking active shields into account first.
        /// </summary>
        public void TakeDamage(int amount, bool isDirectHit = true)
        {
            if (amount <= 0) return;

            // Vulnerable status check (applies +50% damage multiplier to direct hits)
            if (isDirectHit && statusEffects != null)
            {
                var vul = statusEffects.Find(s => s.effectType == StatusEffectType.Vulnerable);
                if (vul != null && vul.duration > 0)
                {
                    amount = (int)Math.Round(amount * 1.5);
                }
            }

            if (shield > 0)
            {
                if (shield >= amount)
                {
                    shield -= amount;
                    amount = 0;
                }
                else
                {
                    amount -= shield;
                    shield = 0;
                }
            }

            currentHp = Math.Max(0, currentHp - amount);
        }

        /// <summary>
        /// Adds shield to protect the character.
        /// </summary>
        public void AddShield(int amount)
        {
            if (amount <= 0) return;
            shield += amount;
        }

        /// <summary>
        /// Adds a status effect using a scriptable object asset configuration.
        /// </summary>
        public void ApplyStatus(StatusEffectSO statusSO, int duration, int intensity)
        {
            if (statusSO == null) return;
            ApplyStatus(statusSO.effectType, duration, intensity, statusSO);
        }

        /// <summary>
        /// Adds a status effect (e.g. Poison, Weak, Vulnerable).
        /// </summary>
        public void ApplyStatus(StatusEffectType effectType, int duration, int intensity, StatusEffectSO statusSO = null)
        {
            if (effectType == StatusEffectType.None) return;
            if (statusEffects == null) statusEffects = new List<StatusEffectInstance>();

            var existing = statusEffects.Find(s => s.effectType == effectType);
            if (existing != null)
            {
                existing.duration = Math.Max(existing.duration, duration);
                existing.intensity = Math.Max(existing.intensity, intensity);
                if (statusSO != null) existing.statusSO = statusSO;
            }
            else
            {
                statusEffects.Add(new StatusEffectInstance(effectType, duration, intensity, statusSO));
            }
        }

        /// <summary>
        /// Resolves ticking status effects at the start of a slot.
        /// </summary>
        public void TickStatusEffects(List<string> logs)
        {
            if (statusEffects == null || statusEffects.Count == 0) return;

            List<StatusEffectInstance> expired = new List<StatusEffectInstance>();

            for (int i = 0; i < statusEffects.Count; i++)
            {
                var status = statusEffects[i];
                if (status.duration <= 0)
                {
                    expired.Add(status);
                    continue;
                }

                if (status.effectType == StatusEffectType.Poison)
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity, isDirectHit: false);
                    logs.Add($"     * [Poison Tick] {name} takes {status.intensity} poison dmg. (HP: {oldHp}->{currentHp})");
                }
                else if (status.effectType == StatusEffectType.Burn)
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity, isDirectHit: false);
                    logs.Add($"     * [Burn Tick] {name} takes {status.intensity} burn dmg. (HP: {oldHp}->{currentHp})");
                }

                status.duration--;
                if (status.duration <= 0)
                {
                    expired.Add(status);
                }
            }

            foreach (var exp in expired)
            {
                statusEffects.Remove(exp);
                string dispName = exp.statusSO != null ? exp.statusSO.displayName : exp.effectType.ToString();
                logs.Add($"     * Status '{dispName}' on {name} has expired.");
            }
        }
    }
}
