using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Controller managing turn commit sequences, card collection resource resets,
    /// and round-to-round baseline transitions.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] 
        [Tooltip("Visual Inspector bridge component managing MVVM loops.")]
        private TimelineInspectorBridge timelineBridge;

        [SerializeField] 
        [Tooltip("Card view manager representing player's deck and hand.")]
        private GameplayCardViewManager cardViewManager;

        private void Awake()
        {
            // Auto-discover references in the scene if not explicitly assigned
            if (timelineBridge == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                timelineBridge = FindFirstObjectByType<TimelineInspectorBridge>();
#else
                timelineBridge = FindObjectOfType<TimelineInspectorBridge>();
#endif
            }

            if (cardViewManager == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                cardViewManager = FindFirstObjectByType<GameplayCardViewManager>();
#else
                cardViewManager = FindObjectOfType<GameplayCardViewManager>();
#endif
            }
        }

        /// <summary>
        /// Commits the current turn planning state:
        /// 1. Updates starting baselines with preview stats.
        /// 2. Discards the current hand and draws a fresh one.
        /// 3. Resets energy (AP).
        /// 4. Wipes active actions on the timeline.
        /// 5. Restarts simulation for the new turn.
        /// </summary>
        public void CommitCurrentTurn()
        {
            if (timelineBridge == null || cardViewManager == null)
            {
                Debug.LogWarning("[TurnManager] Cannot commit turn: missing bridge or view manager reference.");
                return;
            }

            TimelineModel model = timelineBridge.Model;
            CardTimelinePresenter presenter = cardViewManager.Presenter;

            if (model == null || presenter == null)
            {
                Debug.LogWarning("[TurnManager] Model or Presenter layers are not initialized.");
                return;
            }

            Debug.Log("[TurnManager] Committing current turn...");

            // A. Update Base States
            // Permanently update baseline characters with simulated preview parameters from the end of slot simulations
            foreach (var kvp in model.simulatedCharacters)
            {
                if (model.baselineCharacters.ContainsKey(kvp.Key))
                {
                    model.baselineCharacters[kvp.Key] = kvp.Value.Clone();
                }
            }

            // Sync baseline updates back into the bridge's inspector baseline list to persist values
            for (int i = 0; i < timelineBridge.baselineCharacters.Count; i++)
            {
                var charData = timelineBridge.baselineCharacters[i];
                if (charData != null && model.baselineCharacters.TryGetValue(charData.id, out var committedChar))
                {
                    timelineBridge.baselineCharacters[i] = committedChar.Clone();
                }
            }

            // B. Card Pile & Resource Reset
            presenter.DiscardHand();
            presenter.ResetEnergy();
            presenter.DrawCards(cardViewManager.startingDrawCount);

            // C. Timeline Wipe
            model.playerActions.Clear();
            model.enemyActions.Clear();
            timelineBridge.initialTimelineActions.Clear(); // also clear the inspector reference list

            // D. Next Turn Setup
            // Re-simulate at slot 0 to initialize fresh baseline state
            timelineBridge.targetScrubSlot = 0;
            timelineBridge.SyncAndRunSimulation();

            Debug.Log("[TurnManager] Turn committed successfully. Next round initialized.");
        }
    }
}
