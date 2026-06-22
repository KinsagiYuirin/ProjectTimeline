using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// UI Component attached to a Status Effect Icon Prefab.
    /// Manages the rendering of active status icons, text duration counters,
    /// and provides a robust colored fallback if no sprite is assigned to the StatusEffectSO.
    /// </summary>
    public class StatusEffectIconUI : MonoBehaviour
    {
        [Header("UI Component Bindings")]
        [SerializeField]
        [Tooltip("The main image component representing the status icon.")]
        private Image iconImage;

        [SerializeField]
        [Tooltip("Text component used as a single-letter label fallback if no sprite is assigned.")]
        private TMP_Text fallbackTextLabel;

        [SerializeField]
        [Tooltip("Text component displaying the remaining duration of the status effect.")]
        private TMP_Text durationText;

        /// <summary>
        /// Populates the UI visuals using data from the status effect instance.
        /// If a custom icon sprite exists, it is displayed. Otherwise, a color-coded fallback is shown.
        /// </summary>
        /// <param name="statusInstance">The active status effect instance to render.</param>
        public void Setup(StatusEffectInstance statusInstance)
        {
            if (statusInstance == null) return;

            // Reset default visual states
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.white;
            }

            if (fallbackTextLabel != null)
            {
                fallbackTextLabel.text = string.Empty;
                fallbackTextLabel.gameObject.SetActive(false);
            }

            // 1. Check if we have a valid custom sprite in StatusEffectSO
            if (statusInstance.statusSO != null && statusInstance.statusSO.icon != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = statusInstance.statusSO.icon;
                    iconImage.gameObject.SetActive(true);
                }
            }
            else
            {
                // 2. Fallback: Render color-coded circles and letter codes
                if (iconImage != null)
                {
                    iconImage.gameObject.SetActive(true);
                    
                    switch (statusInstance.effectType)
                    {
                        case StatusEffectType.Poison:
                            iconImage.color = new Color(0.18f, 0.67f, 0.18f, 1f); // Forest Green
                            SetFallbackText("P");
                            break;

                        case StatusEffectType.Burn:
                            iconImage.color = new Color(0.92f, 0.26f, 0.08f, 1f); // Fire Orange-Red
                            SetFallbackText("B");
                            break;

                        case StatusEffectType.Weak:
                            iconImage.color = new Color(0.53f, 0.53f, 0.53f, 1f); // Slate Gray
                            SetFallbackText("W");
                            break;

                        case StatusEffectType.Vulnerable:
                            iconImage.color = new Color(0.96f, 0.58f, 0.12f, 1f); // Warning Orange
                            SetFallbackText("V");
                            break;

                        case StatusEffectType.Stun:
                            iconImage.color = new Color(0.98f, 0.84f, 0.08f, 1f); // Electric Yellow
                            SetFallbackText("S");
                            break;

                        case StatusEffectType.Freeze:
                            iconImage.color = new Color(0.14f, 0.63f, 0.94f, 1f); // Ice Blue
                            SetFallbackText("F");
                            break;

                        default:
                            iconImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                            SetFallbackText("?");
                            break;
                    }
                }
            }

            // 3. Set the duration stack text
            if (durationText != null)
            {
                durationText.text = statusInstance.duration.ToString();
            }
        }

        private void SetFallbackText(string text)
        {
            if (fallbackTextLabel != null)
            {
                fallbackTextLabel.text = text;
                fallbackTextLabel.gameObject.SetActive(true);
            }
        }
    }
}
