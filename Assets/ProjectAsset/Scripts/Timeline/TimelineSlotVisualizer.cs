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
        
        public void RefreshSlot(List<ActionNodeData> actions, List<TimelineActionSetup> inspectorSetups = null)
        {
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

                if (inspectorSetups != null)
                {
                    foreach (var setup in inspectorSetups)
                    {
                        if (setup != null && setup.cardBlueprint != null)
                        {
                            bool isMatch = setup.runtimeInstanceId == action.id ||
                                           setup.cardBlueprint.actionBlueprint.id == action.id ||
                                           action.id.Contains(setup.cardBlueprint.cardId);

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

                if (!foundInfo)
                {
                    displayName = action.sourceId == CharacterID.Player ? $"Player {action.actionType}" : $"Enemy {action.actionType} ({action.value})";
                }

                bool playerOwned = action.sourceId == CharacterID.Player;
                iconUI.Setup(displayName, displayIcon, action.id, playerOwned, action.cardSpeed);
            }
        }
    }
}
