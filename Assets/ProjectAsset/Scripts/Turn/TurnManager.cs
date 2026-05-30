using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectTimeline.Timeline
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Execution phase state machine
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Tracks where the turn-loop finite-state machine currently sits.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>Player is freely planning (drag / drop cards, scrub slider).</summary>
        Planning,

        /// <summary>Coroutine is animating the playhead slot-by-slot.</summary>
        Executing,

        /// <summary>Enemy HP reached zero – victory screen is visible.</summary>
        Victory,
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TurnManager
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Controller managing turn commit sequences, card collection resource resets,
    /// and round-to-round baseline transitions.
    ///
    /// NEW: Also owns the real-time Visual Execution Phase.
    ///   • <see cref="StartTimelinePlay"/> — bind this to the runtime "Play" Button.
    ///   • <see cref="ExecuteTimelineSlotsRoutine"/> — internal coroutine that
    ///     animates the playhead, evaluates win/loss at every step, and either
    ///     triggers Victory or hands control back to the player via
    ///     <see cref="CommitCurrentTurn"/>.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // ── Inspector References ──────────────────────────────────────────────

        [Header("References")]
        [SerializeField]
        [Tooltip("Visual Inspector bridge component managing MVVM loops.")]
        private TimelineInspectorBridge timelineBridge;

        [SerializeField]
        [Tooltip("Card view manager representing player's deck and hand.")]
        private GameplayCardViewManager cardViewManager;

        [SerializeField]
        [Tooltip("Reference to the Enemy AI Controller.")]
        private EnemyAIController enemyAI;

        // ── Execution Phase UI ────────────────────────────────────────────────

        [Header("Execution Phase UI")]
        [SerializeField]
        [Tooltip("'Play' button – automatically locked during execution. Assign in Inspector.")]
        private Button playButton;

        [SerializeField]
        [Tooltip("Optional root CanvasGroup that wraps all card drag-drop & planning UI. " +
                 "When assigned, it is made non-interactable during execution so the player " +
                 "cannot mutate the timeline while it is animating.")]
        private CanvasGroup planningUiGroup;

        [SerializeField]
        [Tooltip("Optional panel shown when the enemy is defeated. " +
                 "Activate/deactivate it yourself, or leave null to rely on the " +
                 "OnVictory event instead.")]
        private GameObject victoryPanel;

        // ── Execution Timing ──────────────────────────────────────────────────

        [Header("Execution Timing")]
        [SerializeField]
        [Tooltip("Seconds to pause on each slot before advancing the playhead.")]
        [Range(0.1f, 3f)]
        private float slotStepDuration = 0.8f;

        [SerializeField]
        [Tooltip("Duration (seconds) to smoothly slide the playhead back to slot 0 " +
                 "after a full turn completes. Set to 0 for an instant snap.")]
        [Range(0f, 1.5f)]
        private float playheadResetDuration = 0.4f;

        // ── Events (optional wiring) ──────────────────────────────────────────

        [Header("Events (optional)")]
        [Tooltip("Raised when the enemy HP hits zero mid-execution. " +
                 "Wire any additional victory logic here (e.g. scene transition).")]
        public UnityEngine.Events.UnityEvent OnVictory;

        [Tooltip("Raised at the very end of CommitCurrentTurn, right after control " +
                 "returns to the player. Wire any 'new turn started' logic here.")]
        public UnityEngine.Events.UnityEvent OnTurnCommitted;

        // ── Runtime State ─────────────────────────────────────────────────────

        /// <summary>Public read-only view of the current FSM phase.</summary>
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Planning;

        /// <summary>True while <see cref="ExecuteTimelineSlotsRoutine"/> is running.</summary>
        public bool IsExecuting => CurrentPhase == TurnPhase.Executing;

        /// <summary>Tracks the current turn number of the combat encounter.</summary>
        public int CurrentTurnNumber { get; private set; } = 1;

        private Coroutine _activeExecution;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

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

            if (enemyAI == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                enemyAI = FindFirstObjectByType<EnemyAIController>();
#else
                enemyAI = FindObjectOfType<EnemyAIController>();
#endif
            }
        }

        private void Start()
        {
            // Wire the Play button if it is assigned but has no persistent listener yet
            if (playButton != null)
            {
                playButton.onClick.AddListener(StartTimelinePlay);
            }

            // Make sure we open in the Planning phase with UI fully enabled
            SetPhase(TurnPhase.Planning);
            
            if (enemyAI != null)
            {
                enemyAI.CalculateNextTurnActions();
            }
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(StartTimelinePlay);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – Execution Phase Entry Point
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Entry point bound to the runtime "Play" Button (onClick event).
        /// Validates state, locks the planning UI, and launches the execution coroutine.
        /// Safe to call from Inspector onClick bindings.
        /// </summary>
        public void StartTimelinePlay()
        {
            if (CurrentPhase != TurnPhase.Planning)
            {
                Debug.LogWarning("[TurnManager] StartTimelinePlay ignored – not in Planning phase " +
                                 $"(current phase: {CurrentPhase}).");
                return;
            }

            if (timelineBridge == null || cardViewManager == null)
            {
                Debug.LogWarning("[TurnManager] StartTimelinePlay aborted – missing bridge or view manager.");
                return;
            }

            TimelineModel model = timelineBridge.Model;
            if (model == null)
            {
                Debug.LogWarning("[TurnManager] StartTimelinePlay aborted – TimelineModel is null.");
                return;
            }

            Debug.Log("[TurnManager] ▶ Starting timeline execution phase.");

            // Stop any stale coroutine just in case
            if (_activeExecution != null)
            {
                StopCoroutine(_activeExecution);
                _activeExecution = null;
            }

            SetPhase(TurnPhase.Executing);
            _activeExecution = StartCoroutine(ExecuteTimelineSlotsRoutine());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core Execution Coroutine
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Animates the playhead slot-by-slot (0 → TIMELINE_SLOTS-1).
        /// Checks for victory at every step, then either triggers victory or
        /// commits the turn and hands control back to the player.
        /// </summary>
        private IEnumerator ExecuteTimelineSlotsRoutine()
        {
            TimelineModel model = timelineBridge.Model;
            int totalSlots = TimelineModel.TIMELINE_SLOTS; // e.g. 5 → indices 0..4

            // ── Step through each slot ────────────────────────────────────────
            for (int slot = 0; slot < totalSlots; slot++)
            {
                Debug.Log($"[TurnManager] ⏩ Executing slot {slot}…");

                // Wait the configured dwell time so the player can watch
                yield return new WaitForSeconds(slotStepDuration);

                // Drive the bridge to simulate and render up to this slot
                timelineBridge.targetScrubSlot = slot;
                timelineBridge.SyncAndRunSimulation();

                // ── Mid-loop win/loss evaluation ──────────────────────────────
                if (model.simulatedCharacters.TryGetValue(CharacterID.Enemy, out CharacterData enemyState))
                {
                    if (enemyState.currentHp <= 0)
                    {
                        Debug.Log($"[TurnManager] 🏆 Enemy defeated at slot {slot}! Triggering victory.");
                        yield return TriggerVictoryRoutine();
                        yield break; // ← hard stop; coroutine never falls through to CommitCurrentTurn
                    }
                }
            }

            // ── All slots resolved; enemy still alive → commit the turn ───────
            Debug.Log("[TurnManager] 🔄 All slots resolved. Enemy survived – committing turn.");

            // CRITICAL: Deep-copy the final simulated combat results NOW, before
            // SmoothResetPlayheadRoutine calls SyncAndRunSimulation() at slot 0 and
            // overwrites model.simulatedCharacters with the turn's baseline stats.
            var finalTurnStates = new System.Collections.Generic.Dictionary<CharacterID, CharacterData>();
            foreach (var kvp in model.simulatedCharacters)
            {
                finalTurnStates[kvp.Key] = kvp.Value.Clone();
            }
            Debug.Log($"[TurnManager] 📸 Final states captured before playhead reset " +
                      $"({finalTurnStates.Count} combatants).");

            // Animate the slider back to 0 – safe now because the snapshot is already taken
            yield return SmoothResetPlayheadRoutine();

            // Pass the pre-reset snapshot so CommitCurrentTurn persists the real results
            CommitCurrentTurn(finalTurnStates);

            // CommitCurrentTurn already resets targetScrubSlot = 0, but just ensure
            // we end up cleanly in Planning with UI re-enabled.
            SetPhase(TurnPhase.Planning);
            OnTurnCommitted?.Invoke();

            Debug.Log("[TurnManager] ✅ Turn committed. Returning control to player.");

            _activeExecution = null;
            yield break;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Victory Handler
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggered when the enemy HP reaches ≤ 0 during execution.
        /// Shows the victory panel and raises the OnVictory event.
        /// Does NOT call CommitCurrentTurn or re-enable planning UI.
        /// </summary>
        private IEnumerator TriggerVictoryRoutine()
        {
            SetPhase(TurnPhase.Victory);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }

            OnVictory?.Invoke();

            // Give the player a moment to see the final frame before any overlay appears
            yield return new WaitForSeconds(0.5f);

            _activeExecution = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Playhead Reset Helper
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Smoothly slides the playhead slider back to slot 0.
        /// If <see cref="playheadResetDuration"/> is 0, snaps instantly.
        /// </summary>
        private IEnumerator SmoothResetPlayheadRoutine()
        {
            // Attempt to grab the playhead slider via the bridge's exposed property
            Slider slider = timelineBridge.GetComponentInChildren<Slider>(true);

            if (slider == null || playheadResetDuration <= 0f)
            {
                // Instant snap – no tween needed
                timelineBridge.targetScrubSlot = 0;
                timelineBridge.SyncAndRunSimulation();
                yield break;
            }

            float startValue = slider.value;
            float elapsed = 0f;

            while (elapsed < playheadResetDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / playheadResetDuration);
                slider.value = Mathf.Lerp(startValue, 0f, t);
                yield return null;
            }

            // Guarantee final value is exactly 0
            slider.value = 0f;
            timelineBridge.targetScrubSlot = 0;
            timelineBridge.SyncAndRunSimulation();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase FSM – UI Lock / Unlock
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions to a new <see cref="TurnPhase"/> and updates UI lock state accordingly.
        /// </summary>
        private void SetPhase(TurnPhase phase)
        {
            CurrentPhase = phase;

            bool isPlanning = phase == TurnPhase.Planning;

            // Lock / unlock the Play button
            if (playButton != null)
            {
                playButton.interactable = isPlanning;
            }

            // Lock / unlock the entire planning CanvasGroup (cards, hand area, etc.)
            if (planningUiGroup != null)
            {
                planningUiGroup.interactable    = isPlanning;
                planningUiGroup.blocksRaycasts  = isPlanning;
                planningUiGroup.alpha           = isPlanning ? 1f : 0.5f; // dim while locked
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CommitCurrentTurn
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Commits the current turn planning state:
        /// 1. Updates starting baselines with the final combat results.
        /// 2. Discards the current hand and draws a fresh one.
        /// 3. Resets energy (AP).
        /// 4. Wipes active actions on the timeline.
        /// 5. Restarts simulation for the new turn.
        ///
        /// Can be called directly from Inspector buttons for debugging.
        /// During normal gameplay it is invoked automatically by
        /// <see cref="ExecuteTimelineSlotsRoutine"/> after all slots resolve.
        /// </summary>
        /// <param name="explicitFinalStates">
        /// Optional pre-captured snapshot of the post-execution combat results.
        /// When provided (non-null), these values are used to update the baselines
        /// instead of <c>model.simulatedCharacters</c>, which may have already been
        /// reset to slot-0 baseline values by <see cref="SmoothResetPlayheadRoutine"/>.
        /// Pass <c>null</c> (or omit) when calling manually from the Inspector.
        /// </param>
        public void CommitCurrentTurn(
            System.Collections.Generic.Dictionary<CharacterID, CharacterData> explicitFinalStates = null)
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
            // Prefer the pre-captured snapshot when available so that the playhead
            // reset animation (which calls SyncAndRunSimulation at slot 0) cannot
            // overwrite model.simulatedCharacters before we read it.
            // Fall back to model.simulatedCharacters only for manual/debug calls.
            var sourceStates = (explicitFinalStates != null && explicitFinalStates.Count > 0)
                ? explicitFinalStates
                : model.simulatedCharacters;

            if (explicitFinalStates != null)
                Debug.Log("[TurnManager] Using explicit pre-reset snapshot for baseline update.");
            else
                Debug.LogWarning("[TurnManager] No explicit snapshot provided – falling back to " +
                                 "model.simulatedCharacters (may reflect reset slot-0 values).");

            foreach (var kvp in sourceStates)
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
                if (charData != null &&
                    System.Enum.TryParse<CharacterID>(charData.id, true, out CharacterID characterEnum) &&
                    model.baselineCharacters.TryGetValue(characterEnum, out var committedChar))
                {
                    timelineBridge.baselineCharacters[i] = committedChar.Clone();
                }
            }

            // B. Card Pile & Resource Reset
            presenter.DiscardHand();
            presenter.ResetEnergy();
            presenter.DrawCards(cardViewManager.startingDrawCount);

            // Increment Turn Number immediately after resetting energy
            CurrentTurnNumber++;

            // C. Timeline Wipe
            // Run Enemy AI first so it can scan the player's last actions before they are wiped
            if (enemyAI != null)
            {
                enemyAI.CalculateNextTurnActions();
            }
            else
            {
                model.enemyActions.Clear();
            }

            model.playerActions.Clear();
            timelineBridge.initialTimelineActions.RemoveAll(setup =>
                setup == null ||
                (setup.cardBlueprint != null && setup.cardBlueprint.actionBlueprint != null && setup.cardBlueprint.actionBlueprint.sourceId == CharacterID.Player)
            );

            // D. Next Turn Setup
            // Re-simulate at slot 0 to initialize fresh baseline state
            timelineBridge.targetScrubSlot = 0;
            timelineBridge.SyncAndRunSimulation();

            Debug.Log("[TurnManager] Turn committed successfully. Next round initialized.");
        }
    }
}
