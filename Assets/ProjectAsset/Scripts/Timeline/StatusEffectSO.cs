using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// ScriptableObject defining static configuration and metadata for a Status Effect.
    /// Creates assets representing custom statuses (e.g. Poison, Burn, Weak, Vulnerable, Stun, Freeze).
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Project Timeline/Status Effect Definition")]
    public class StatusEffectSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The category type of this status effect.")]
        public StatusEffectType effectType;

        [Tooltip("Readable display name for the UI.")]
        public string displayName;

        [Header("Visuals & Description")]
        [Tooltip("Icon shown in combat UI.")]
        public Sprite icon;

        [TextArea(3, 5)]
        [Tooltip("What this status does.")]
        public string description;
    }
}
