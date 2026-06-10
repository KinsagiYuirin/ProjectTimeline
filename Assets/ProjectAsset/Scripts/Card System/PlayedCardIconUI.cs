using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// UI component representing a miniature Played Card or telegraph icon within a visual timeline slot UI bar.
    /// Displays the name and icon associated with an action node.
    ///
    /// NEW: Implements <see cref="IPointerClickHandler"/> so that clicking the icon during the
    /// Planning phase triggers a card recall — returning the card to hand and refunding its energy.
    /// </summary>
    public class PlayedCardIconUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI Component Bindings")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text speedText;

        [Header("Recall Feedback (optional)")]
        [SerializeField]
        [Tooltip("Optional highlight shown when the pointer hovers over this icon during the Planning phase. " +
                 "Assign an Image overlay set to alpha 0 at rest and alpha ~0.25 on hover.")]
        private Image hoverHighlight;

        // ── Runtime State ─────────────────────────────────────────────────────

        /// <summary>The unique runtime instance ID of the action / card this icon represents.</summary>
        private string cardInstanceId;

        /// <summary>True only for player-owned action icons (enemy telegraphs cannot be recalled).</summary>
        private bool isPlayerCard;

        // Cached scene references resolved lazily in Awake
        private TurnManager turnManager;
        private GameplayCardViewManager viewManager;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Resolve scene singletons once at spawn time.
            // These lookups are acceptable here because PlayedCardIconUI is
            // instantiated at runtime (not in Awake of a persistent object).
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            turnManager = FindFirstObjectByType<TurnManager>();
            viewManager = FindFirstObjectByType<GameplayCardViewManager>();
#else
            turnManager = FindObjectOfType<TurnManager>();
            viewManager = FindObjectOfType<GameplayCardViewManager>();
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public Setup API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Populates the icon's visual fields and binds it to a specific action node instance.
        /// </summary>
        /// <param name="displayName">The formatted text to show (e.g. Card Name or enemy telegraph description).</param>
        /// <param name="icon">The Sprite icon to display. If null the Image component is hidden.</param>
        /// <param name="instanceId">The unique runtime instance ID from <see cref="ActionNodeData.id"/> — used to identify which card to recall on click.</param>
        /// <param name="playerOwned">
        /// Pass <c>true</c> for player-played cards (recall is allowed).
        /// Pass <c>false</c> for enemy telegraphs (click is silently ignored).
        /// </param>
        /// <param name="speed">The priority card speed.</param>
        public void Setup(string displayName, Sprite icon, string instanceId, bool playerOwned = true, CardSpeed speed = CardSpeed.Normal)
        {
            cardInstanceId = instanceId;
            isPlayerCard   = playerOwned;

            if (nameText != null)
            {
                nameText.text = displayName;
            }

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite  = icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            if (speedText != null)
            {
                speedText.text = speed.ToString();
            }

            // Hide hover highlight at rest
            SetHighlight(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IPointerClickHandler – Recall on Click
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Unity's EventSystem when the icon is clicked.
        /// Only processes left-clicks on player-owned cards during the Planning phase.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // Only left-click triggers recall
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // Enemy telegraphs cannot be recalled by the player
            if (!isPlayerCard) return;

            // Guard: refuse if the game is not in Planning phase
            if (turnManager == null)
            {
                Debug.LogWarning("[PlayedCardIconUI] TurnManager not found in scene. Cannot validate phase.");
                return;
            }

            if (turnManager.CurrentPhase != TurnPhase.Planning)
            {
                Debug.Log($"[PlayedCardIconUI] Recall ignored – current phase is '{turnManager.CurrentPhase}' (must be Planning).");
                return;
            }

            // Guard: ensure view manager is available
            if (viewManager == null)
            {
                Debug.LogWarning("[PlayedCardIconUI] GameplayCardViewManager not found. Cannot recall card.");
                return;
            }

            if (string.IsNullOrEmpty(cardInstanceId))
            {
                Debug.LogWarning("[PlayedCardIconUI] cardInstanceId is null/empty. Setup() may not have been called.");
                return;
            }

            Debug.Log($"[PlayedCardIconUI] Recall requested for card instance '{cardInstanceId}'.");

            // Delegate the full recall flow to the view manager layer
            viewManager.RecallCardFromSlot(cardInstanceId);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hover Highlight (optional UX polish)
        // ─────────────────────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isPlayerCard && turnManager != null && turnManager.CurrentPhase == TurnPhase.Planning)
            {
                SetHighlight(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
        }

        private void SetHighlight(bool active)
        {
            if (hoverHighlight == null) return;
            Color c = hoverHighlight.color;
            c.a = active ? 0.25f : 0f;
            hoverHighlight.color = c;
        }
    }
}
