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
        [Tooltip("Upper row container transform where Main Actions (isExclusive == true) are spawned.")]
        private Transform mainActionContainer;

        [SerializeField]
        [Tooltip("Lower row container transform where Free Actions (isExclusive == false) are spawned.")]
        private Transform freeActionContainer;

        private void Awake()
        {
            if (mainActionContainer == null)
            {
                mainActionContainer = transform;
            }
            if (freeActionContainer == null)
            {
                freeActionContainer = transform;
            }
        }

        /// <summary>
        /// Clears all child game objects from the target container transform safely.
        /// </summary>
        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            List<Transform> childrenToDestroy = new List<Transform>();
            foreach (Transform child in container)
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

        /// <summary>
        /// Clears old visual representations and spawns a miniature icon for each action scheduled at this slot.
        /// </summary>
        /// <param name="actions">The list of action nodes resolving at this slot.</param>
        /// <param name="presenter">Reference to the presenter broker to resolve played card blueprints.</param>
        /// <param name="inspectorSetups">Optional list of inspector action setups to retrieve static card details.</param>
        public void RefreshSlot(List<ActionNodeData> actions, CardTimelinePresenter presenter, List<TimelineActionSetup> inspectorSetups = null)
        {
            // Clear current spawned icons safely in both Play Mode and Edit Mode
            ClearContainer(mainActionContainer);
            ClearContainer(freeActionContainer);

            if (playedCardIconPrefab == null || actions == null) return;

            foreach (var action in actions)
            {
                if (action == null) continue;

                Transform parentContainer = action.isExclusive ? mainActionContainer : freeActionContainer;
                GameObject iconObj = Instantiate(playedCardIconPrefab, parentContainer);
                PlayedCardIconUI iconUI = iconObj.GetComponent<PlayedCardIconUI>();
                if (iconUI == null) continue;

                string displayName = "";
                Sprite displayIcon = null;
                bool foundInfo = false;

                // 1. Search player collections (if source is player)
                if (action.sourceId == CharacterID.Player && presenter != null)
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
                    if (action.sourceId == CharacterID.Player)
                    {
                        displayName = $"Player {action.actionType}";
                    }
                    else
                    {
                        displayName = $"Enemy {action.actionType} ({action.value})";
                    }
                }

                // Pass the instance ID and player-ownership flag so the icon can
                // identify itself during recall and validate click permissions.
                bool playerOwned = action.sourceId == CharacterID.Player;
                iconUI.Setup(displayName, displayIcon, action.id, playerOwned, action.cardSpeed);
            }
        }
    }
}
