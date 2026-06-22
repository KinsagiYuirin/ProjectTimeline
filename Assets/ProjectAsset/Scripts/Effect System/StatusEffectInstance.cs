using System;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Model representation of a status effect active on a character.
    /// </summary>
    [Serializable]
    public class StatusEffectInstance
    {
        public StatusEffectType effectType;
        public StatusEffectSO statusSO;
        public int duration;
        public int intensity;

        // For backward compatibility and UI display
        public string statusId;

        public StatusEffectInstance(StatusEffectType effectType, int duration, int intensity, StatusEffectSO statusSO = null)
        {
            this.effectType = effectType;
            this.duration = duration;
            this.intensity = intensity;
            this.statusSO = statusSO;
            this.statusId = (statusSO != null) ? statusSO.displayName : effectType.ToString();
        }

        public StatusEffectInstance Clone()
        {
            return new StatusEffectInstance(effectType, duration, intensity, statusSO);
        }
    }
}
