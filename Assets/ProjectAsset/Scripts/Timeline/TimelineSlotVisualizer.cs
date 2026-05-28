using UnityEngine;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Visual component attached to each of the 5 visual timeline slots.
    /// Dynamically instantiates PlayedCardIconPrefab UI elements to represent scheduled actions.
    /// </summary>
    public class TimelineSlotVisualizer : MonoBehaviour
    {
        [Header("Slot Configuration")]
        [Tooltip("The chronological slot index this visualizer represents (0 to 4).")]
        [Range(0, 4)]
        public int slotIndex;

        [Header("Prefab & Spawning Config")]
        [SerializeField] 
        [Tooltip("Prefab representing a Played Card or telegraph icon.")]
        private GameObject playedCardIconPrefab;

        [SerializeField] 
        [Tooltip("The container transform where icons will be spawned. Defaults to this transform if null.")]
        private Transform iconContainer;

        private void Awake()
        {
            if (iconContainer == null)
            {
                iconContainer = transform;
            }
        }

        /// <summary>
        /// Clears old visual representations and spawns a miniature icon for each action scheduled at this slot.
        /// </summary>
        /// <param name="actions">The list of action nodes resolving at this slot.</param>
        /// <param name="presenter">Reference to the presenter broker to resolve played card blueprints.</param>
        /// <param name="inspectorSetups">Optional list of inspector action setups to retrieve static card details.</param>
        public void RefreshSlot(List<ActionNodeData> actions, CardTimelinePresenter presenter, List<TimelineActionSetup> inspectorSetups = null)
        {
            // Clear current spawned icons safely in both Play Mode and Edit Mode
            if (iconContainer != null)
            {
                List<Transform> childrenToDestroy = new List<Transform>();
                foreach (Transform child in iconContainer)
                {
                    if (child != null)
                    {
                        childrenToDestroy.Add(child);
                    }
                }

                foreach (var child in childrenToDestroy)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            if (playedCardIconPrefab == null || actions == null) return;

            foreach (var action in actions)
            {
                if (action == null) continue;

                GameObject iconObj = Instantiate(playedCardIconPrefab, iconContainer);
                PlayedCardIconUI iconUI = iconObj.GetComponent<PlayedCardIconUI>();
                if (iconUI == null) continue;

                string displayName = "";
                Sprite displayIcon = null;
                bool foundInfo = false;

                // 1. Search player collections (if source is player)
                if (action.sourceId == "player" && presenter != null)
                {
                    RuntimeCardInstance card = null;

                    // Search Discard Pile (main location for played cards)
                    foreach (var c in presenter.Collection.DiscardPile)
                    {
                        if (c.instanceId == action.id)
                        {
                            card = c;
                            break;
                        }
                    }

                    // Search Hand (fallback)
                    if (card == null)
                    {
                        foreach (var c in presenter.Collection.Hand)
                        {
                            if (c.instanceId == action.id)
                            {
                                card = c;
                                break;
                            }
                        }
                    }

                    // Search Deck (fallback)
                    if (card == null)
                    {
                        foreach (var c in presenter.Collection.Deck)
                        {
                            if (c.instanceId == action.id)
                            {
                                card = c;
                                break;
                            }
                        }
                    }

                    if (card != null)
                    {
                        displayName = card.blueprint.cardName;
                        displayIcon = card.blueprint.icon;
                        foundInfo = true;
                    }
                }

                // 2. Search inspector setups (fallback for both player and enemy initial setups)
                if (!foundInfo && inspectorSetups != null)
                {
                    foreach (var setup in inspectorSetups)
                    {
                        if (setup != null && setup.cardBlueprint != null)
                        {
                            bool isMatch = setup.runtimeInstanceId == action.id ||
                                           setup.cardBlueprint.actionBlueprint.id == action.id ||
                                           action.id.Contains(setup.cardBlueprint.cardId) ||
                                           action.id.Contains(setup.cardBlueprint.actionBlueprint.id);

                            if (isMatch)
                            {
                                displayName = setup.cardBlueprint.cardName;
                                displayIcon = setup.cardBlueprint.icon;
                                foundInfo = true;
                                break;
                            }
                        }
                    }
                }

                // 3. Fallbacks if name/icon not found
                if (!foundInfo)
                {
                    if (action.sourceId == "player")
                    {
                        displayName = $"Player {action.actionType}";
                    }
                    else
                    {
                        displayName = $"Enemy {action.actionType} ({action.value})";
                    }
                }

                iconUI.Setup(displayName, displayIcon);
            }
        }
    }
}
