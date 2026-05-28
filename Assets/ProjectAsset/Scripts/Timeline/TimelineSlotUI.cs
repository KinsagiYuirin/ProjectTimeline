using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Component attached to visual timeline slots to detect card drop inputs.
    /// Implements IDropHandler to process dragged card drop events at runtime.
    /// </summary>
    public class TimelineSlotUI : MonoBehaviour, IDropHandler
    {
        [Header("Slot Configuration")]
        [Range(0, 4)]
        [Tooltip("The index of this slot on the timeline (0 to 4).")]
        public int slotIndex;

        [Header("View Reference")]
        [SerializeField]
        [Tooltip("Reference to the gameplay card view manager driving the deck/hand UI.")]
        private GameplayCardViewManager viewManager;

        private void Awake()
        {
            // Auto-discover the view manager in the scene if not explicitly assigned
            if (viewManager == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                viewManager = FindFirstObjectByType<GameplayCardViewManager>();
#else
                viewManager = FindObjectOfType<GameplayCardViewManager>();
#endif
            }
        }

        /// <summary>
        /// Triggered by Unity's EventSystem when an object is dropped on this slot UI component.
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null) return;

            // Attempt to retrieve the card item script from the dragged object
            GameplayCardItem draggedCard = eventData.pointerDrag.GetComponent<GameplayCardItem>();
            if (draggedCard != null && viewManager != null)
            {
                // Attempt to play the card to this slot index
                bool success = viewManager.PlayCardToSlot(draggedCard, slotIndex);
                if (success)
                {
                    // Card was successfully played. ViewManager handles destroying the card.
                }
                else
                {
                    // Play failed (e.g. not enough energy). Snap the card back to the hand.
                    draggedCard.ReturnToHand();
                }
            }
        }
    }
}
