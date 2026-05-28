using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// UI component representing a miniature Played Card or telegraph icon within a visual timeline slot UI bar.
    /// Displays the name, status value, and icon associated with the action node.
    /// </summary>
    public class PlayedCardIconUI : MonoBehaviour
    {
        [Header("UI Component Bindings")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image iconImage;

        /// <summary>
        /// Populates the UI fields with the action node's visual data.
        /// </summary>
        /// <param name="displayName">The formatted text to show (e.g. Card Name or Action telegraph description).</param>
        /// <param name="icon">The Sprite icon to display. If null, the Image component is hidden.</param>
        public void Setup(string displayName, Sprite icon)
        {
            if (nameText != null)
            {
                nameText.text = displayName;
            }

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
        }
    }
}
