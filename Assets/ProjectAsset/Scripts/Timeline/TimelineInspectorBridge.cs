using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// View component acting as the Unity Inspector and Runtime UI Bridge.
    /// Connects Model and ViewModel to Unity UI Canvas elements and SpriteRenderers,
    /// driving real-time updates and combat visual feedback during Play Mode and Edit Mode.
    /// </summary>
    public class TimelineInspectorBridge : MonoBehaviour
    {
        [Header("Baseline Setup (Start of Turn)")]
        [Tooltip("Configure combatants and their starting stats for the turn.")]
        public List<CharacterData> baselineCharacters = new List<CharacterData>()
        {
            new CharacterData("player", "Hero (Player)", 100, 100, 0),
            new CharacterData("enemy", "Goblin Boss (Enemy)", 80, 80, 0)
        };

        [Header("Timeline Actions Setup")]
        [Tooltip("Configure the sequence of actions scheduled on the timeline slots (0 to 4).")]
        public List<TimelineActionSetup> initialTimelineActions = new List<TimelineActionSetup>();

        [Header("Simulation Scrubbing Control")]
        [Range(0, 4)]
        [Tooltip("Scrub the timeline playhead to preview the combat outcome up to this slot index.")]
        public int targetScrubSlot = 1;

        [Header("Runtime UI Bindings")]
        [SerializeField] private Slider playheadSlider;
        [Space]
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text playerShieldText;
        [SerializeField] private Image playerHpFill;
        [SerializeField] private Image playerShieldFill;
        [Space]
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private TMP_Text enemyShieldText;
        [SerializeField] private Image enemyHpFill;
        [SerializeField] private Image enemyShieldFill;
        [Space]
        [SerializeField] private TMP_Text simulationLogText;
        [Space]
        [Header("Timeline Slots Visualizers")]
        [SerializeField] private List<TimelineSlotVisualizer> playerSlotVisualizers = new List<TimelineSlotVisualizer>();
        [SerializeField] private List<TimelineSlotVisualizer> enemySlotVisualizers = new List<TimelineSlotVisualizer>();

        [Header("Visual Feedback (Avatars)")]
        [SerializeField] private SpriteRenderer playerSpriteRenderer;
        [SerializeField] private SpriteRenderer enemySpriteRenderer;

        [Header("Feedback Flash Settings")]
        [SerializeField] private Color attackFlashColor = new Color(1f, 0.5f, 0f, 1f); // Orange
        [SerializeField] private Color damageFlashColor = Color.red;
        [SerializeField] private Color shieldFlashColor = Color.cyan;
        [SerializeField] private Color delayFlashColor = Color.yellow;
        [SerializeField] private float flashDuration = 0.3f;

        // MVVM Core references
        private TimelineModel model;
        private TimelineViewModel viewModel;

        // Active flash coroutines per avatar to prevent overlapping conflicts
        private Dictionary<SpriteRenderer, Coroutine> activeFlashes = new Dictionary<SpriteRenderer, Coroutine>();

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeMVVM();
        }

        private void Start()
        {
            // Bind UI Slider value listener at runtime
            if (playheadSlider != null)
            {
                playheadSlider.minValue = 0;
                playheadSlider.maxValue = TimelineModel.TIMELINE_SLOTS - 1;
                playheadSlider.wholeNumbers = true;
                playheadSlider.value = targetScrubSlot;
                
                // Add runtime scrub listener
                playheadSlider.onValueChanged.AddListener(OnPlayheadSliderValueChanged);
            }

            // Perform initial simulation layout update
            RunSimulation();
        }

        /// <summary>
        /// Unity lifecycle event called when script is loaded or values are modified in the Inspector.
        /// Drives the instant recalculation in Edit Mode without running the game.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            // Guard against executing simulation loops during scene teardown or playmode transition phases
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
            {
                return;
            }
#endif

            // Protect against accessing lists during editor deserialization/destruction states
            if (baselineCharacters == null || initialTimelineActions == null) return;

            RunSimulation();
        }

        #endregion

        #region Setup & Commands

        /// <summary>
        /// Context Menu command to manually trigger a full recalculation.
        /// Right-click the component header in the Inspector and select '⚡ Force Recalculate'.
        /// </summary>
        [ContextMenu("⚡ Force Recalculate")]
        public void ForceRecalculate()
        {
            InitializeMVVM();
            RunSimulation();
        }

        /// <summary>
        /// Prepares the pure C# MVVM layers and registers event hooks.
        /// </summary>
        private void InitializeMVVM()
        {
            model = new TimelineModel();
            viewModel = new TimelineViewModel(model);

            // Subscribe view updates to the ViewModel's update notification event
            viewModel.OnTimelineUpdated += OnTimelineSimulationUpdated;
        }

        /// <summary>
        /// Exposes the active timeline model. Initializes MVVM layers if not already done.
        /// </summary>
        public TimelineModel Model
        {
            get
            {
                if (model == null)
                {
                    InitializeMVVM();
                }
                return model;
            }
        }

        /// <summary>
        /// Public wrapper to synchronize Inspector lists to the Model and run the simulation.
        /// </summary>
        public void SyncAndRunSimulation()
        {
            RunSimulation();
        }

        /// <summary>
        /// Synchronizes the Inspector's list data into the pure C# Model structures,
        /// then commands the ViewModel to perform scrubbing simulation.
        /// </summary>
        private void RunSimulation()
        {
            if (viewModel == null || model == null)
            {
                InitializeMVVM();
            }

            // Sync baseline characters
            model.baselineCharacters.Clear();
            foreach (var character in baselineCharacters)
            {
                if (character != null && !string.IsNullOrEmpty(character.id))
                {
                    model.baselineCharacters[character.id] = character;
                }
            }

            // Sync actions (Segregate by sourceId to identify Player vs Enemy actions)
            model.enemyActions.Clear();
            model.playerActions.Clear();
            for (int i = 0; i < initialTimelineActions.Count; i++)
            {
                var setup = initialTimelineActions[i];
                if (setup == null || setup.cardBlueprint == null) continue;

                // Deep copy/clone the inner payload
                ActionNodeData clonedNode = setup.cardBlueprint.actionBlueprint.Clone();

                // Ensure a valid ID exists
                clonedNode.id = string.IsNullOrEmpty(clonedNode.id) ? setup.cardBlueprint.cardId : clonedNode.id;

                // Inject the runtime parameters from the struct
                clonedNode.startSlot = setup.startSlot;
                clonedNode.effectiveSlot = setup.startSlot;

                // If overrideSourceId is not empty, re-assign sourceId
                clonedNode.sourceId = string.IsNullOrEmpty(setup.overrideSourceId) ? clonedNode.sourceId : setup.overrideSourceId;

                // Generate or assign a unique instance ID
                if (string.IsNullOrEmpty(setup.runtimeInstanceId))
                {
                    setup.runtimeInstanceId = clonedNode.id + "_" + i + "_" + System.Guid.NewGuid().ToString().Substring(0, 8);
                }
                clonedNode.id = setup.runtimeInstanceId;

                // Sort and distribute based on finalized sourceId
                if (clonedNode.sourceId == "player")
                {
                    model.playerActions.Add(clonedNode);
                }
                else
                {
                    model.enemyActions.Add(clonedNode);
                }
            }

            // Scrub the playhead to trigger simulation recalculation
            viewModel.ScrubToSlot(targetScrubSlot);
        }

        #endregion

        #region Interactive Bindings & Callbacks

        /// <summary>
        /// Event listener bound to the runtime UI Slider's OnValueChanged event.
        /// </summary>
        public void OnPlayheadSliderValueChanged(float value)
        {
            targetScrubSlot = Mathf.Clamp((int)value, 0, TimelineModel.TIMELINE_SLOTS - 1);
            RunSimulation();
        }

        /// <summary>
        /// Event handler triggered by the ViewModel. Synchronizes texts, bars, slider, and triggers visual flashes.
        /// </summary>
        private void OnTimelineSimulationUpdated(Dictionary<string, CharacterData> simulatedCharacters, List<string> logs)
        {
            // 1. Reset sprite renderer colors to prevent visual locks
            ResetSpriteColors();

            // 2. Synchronize character texts and progress bar fills
            UpdateCharacterUI(simulatedCharacters);

            // 3. Synchronize playhead slider value without looping listeners
            UpdateSliderUI();

            // 4. Synchronize simulation log text component
            UpdateLogsUI(logs);

            // 5. Trigger reactive feedback flashes based on actions executed in the current slot
            TriggerActiveSlotFlashes();

            // 6. Output to developer console for safety check
            LogToUnityConsole(logs, simulatedCharacters);

            // 7. Update visual representations inside the timeline slot bars
            UpdateSlotVisualizers();
        }

        #endregion

        #region UI Sync Helpers

        private void ResetSpriteColors()
        {
            if (playerSpriteRenderer != null) playerSpriteRenderer.color = Color.white;
            if (enemySpriteRenderer != null) enemySpriteRenderer.color = Color.white;
        }

        private void UpdateCharacterUI(Dictionary<string, CharacterData> simulatedCharacters)
        {
            // Sync Player UI
            if (simulatedCharacters.TryGetValue("player", out CharacterData player))
            {
                if (playerHpText != null) playerHpText.text = $"HP: {player.currentHp} / {player.maxHp}";
                if (playerShieldText != null) playerShieldText.text = $"Shield: {player.shield}";
                if (playerHpFill != null) playerHpFill.fillAmount = player.maxHp > 0 ? (float)player.currentHp / player.maxHp : 0f;
                if (playerShieldFill != null) playerShieldFill.fillAmount = player.maxHp > 0 ? Mathf.Clamp01((float)player.shield / player.maxHp) : 0f;
            }

            // Sync Enemy UI
            if (simulatedCharacters.TryGetValue("enemy", out CharacterData enemy))
            {
                if (enemyHpText != null) enemyHpText.text = $"HP: {enemy.currentHp} / {enemy.maxHp}";
                if (enemyShieldText != null) enemyShieldText.text = $"Shield: {enemy.shield}";
                if (enemyHpFill != null) enemyHpFill.fillAmount = enemy.maxHp > 0 ? (float)enemy.currentHp / enemy.maxHp : 0f;
                if (enemyShieldFill != null) enemyShieldFill.fillAmount = enemy.maxHp > 0 ? Mathf.Clamp01((float)enemy.shield / enemy.maxHp) : 0f;
            }
        }

        private void UpdateSliderUI()
        {
            if (playheadSlider != null && (int)playheadSlider.value != targetScrubSlot)
            {
                playheadSlider.onValueChanged.RemoveListener(OnPlayheadSliderValueChanged);
                playheadSlider.value = targetScrubSlot;
                playheadSlider.onValueChanged.AddListener(OnPlayheadSliderValueChanged);
            }
        }

        private void UpdateLogsUI(List<string> logs)
        {
            if (simulationLogText != null)
            {
                simulationLogText.text = string.Join("\n", logs);
            }
        }

        /// <summary>
        /// Scans all simulated actions and triggers color flashes for combatants acting in the target slot.
        /// </summary>
        private void TriggerActiveSlotFlashes()
        {
            if (viewModel == null || viewModel.Model == null) return;

            var simActions = viewModel.Model.simulatedActions;
            foreach (var action in simActions)
            {
                // Only flash for actions that resolve at the currently active scrub slot
                if (action.effectiveSlot == targetScrubSlot)
                {
                    SpriteRenderer sourceSR = GetSpriteRenderer(action.sourceId);
                    SpriteRenderer targetSR = GetSpriteRenderer(action.targetId);

                    switch (action.actionType)
                    {
                        case ActionType.Attack:
                            // Source flashes attack color, target flashes damage color
                            TriggerFlash(sourceSR, attackFlashColor);
                            TriggerFlash(targetSR, damageFlashColor);
                            break;
                        case ActionType.Defend:
                            // Target receives shield block color
                            TriggerFlash(targetSR, shieldFlashColor);
                            break;
                        case ActionType.Delay:
                            // Target receives delay yellow flash
                            TriggerFlash(targetSR, delayFlashColor);
                            break;
                    }
                }
            }
        }

        private SpriteRenderer GetSpriteRenderer(string characterId)
        {
            if (characterId == "player") return playerSpriteRenderer;
            if (characterId == "enemy") return enemySpriteRenderer;
            return null;
        }

        private void UpdateSlotVisualizers()
        {
            if (model == null) return;

            // Ensure lists are initialized
            if (playerSlotVisualizers == null) playerSlotVisualizers = new List<TimelineSlotVisualizer>();
            if (enemySlotVisualizers == null) enemySlotVisualizers = new List<TimelineSlotVisualizer>();

            // Auto-discover if either list is empty
            if (playerSlotVisualizers.Count == 0 || enemySlotVisualizers.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                var visualizers = FindObjectsByType<TimelineSlotVisualizer>(FindObjectsSortMode.None);
#else
                var visualizers = FindObjectsOfType<TimelineSlotVisualizer>();
#endif
                var discoveredPlayer = new List<TimelineSlotVisualizer>();
                var discoveredEnemy = new List<TimelineSlotVisualizer>();

                foreach (var vis in visualizers)
                {
                    if (vis == null) continue;

                    string nameLower = vis.gameObject.name.ToLower();
                    string parentNameLower = vis.transform.parent != null ? vis.transform.parent.gameObject.name.ToLower() : "";

                    bool isPlayer = nameLower.Contains("player") || nameLower.Contains("hero") || 
                                     parentNameLower.Contains("player") || parentNameLower.Contains("hero") ||
                                     nameLower.Contains("p_") || parentNameLower.Contains("p_");
                    
                    bool isEnemy = nameLower.Contains("enemy") || nameLower.Contains("boss") || 
                                    parentNameLower.Contains("enemy") || parentNameLower.Contains("boss") ||
                                    nameLower.Contains("e_") || parentNameLower.Contains("e_");

                    if (isPlayer)
                    {
                        discoveredPlayer.Add(vis);
                    }
                    else if (isEnemy)
                    {
                        discoveredEnemy.Add(vis);
                    }
                }

                if (playerSlotVisualizers.Count == 0 && discoveredPlayer.Count > 0)
                {
                    discoveredPlayer.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
                    playerSlotVisualizers = discoveredPlayer;
                }

                if (enemySlotVisualizers.Count == 0 && discoveredEnemy.Count > 0)
                {
                    discoveredEnemy.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
                    enemySlotVisualizers = discoveredEnemy;
                }
            }

            GameplayCardViewManager viewManager = null;
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            viewManager = FindFirstObjectByType<GameplayCardViewManager>();
#else
            viewManager = FindObjectOfType<GameplayCardViewManager>();
#endif
            CardTimelinePresenter presenter = viewManager != null ? viewManager.Presenter : null;

            // 1. Update player slots
            for (int i = 0; i < playerSlotVisualizers.Count; i++)
            {
                var vis = playerSlotVisualizers[i];
                if (vis != null)
                {
                    int index = vis.slotIndex;
                    List<ActionNodeData> playerActions = model.simulatedActions.FindAll(a => a.effectiveSlot == index && a.sourceId == "player");
                    vis.RefreshSlot(playerActions, presenter, initialTimelineActions);
                }
            }

            // 2. Update enemy slots
            for (int i = 0; i < enemySlotVisualizers.Count; i++)
            {
                var vis = enemySlotVisualizers[i];
                if (vis != null)
                {
                    int index = vis.slotIndex;
                    List<ActionNodeData> enemyActions = model.simulatedActions.FindAll(a => a.effectiveSlot == index && a.sourceId != "player");
                    vis.RefreshSlot(enemyActions, presenter, initialTimelineActions);
                }
            }
        }

        #endregion

        #region Visual Effects (Coroutines)

        /// <summary>
        /// Triggers a color flash on the target SpriteRenderer.
        /// Handles Edit Mode immediate previewing and Play Mode smooth fade-back animation.
        /// </summary>
        private void TriggerFlash(SpriteRenderer spriteRenderer, Color flashColor)
        {
            if (spriteRenderer == null) return;

            if (Application.isPlaying)
            {
                // Interrupt active flash coroutine to prevent visual jitter
                if (activeFlashes.TryGetValue(spriteRenderer, out Coroutine activeCor) && activeCor != null)
                {
                    StopCoroutine(activeCor);
                }

                // Launch play-mode fading routine
                activeFlashes[spriteRenderer] = StartCoroutine(FlashColorCoroutine(spriteRenderer, flashColor));
            }
            else
            {
                // Immediate color shift in Edit Mode
                spriteRenderer.color = flashColor;
            }
        }

        private IEnumerator FlashColorCoroutine(SpriteRenderer renderer, Color startColor)
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                
                if (renderer != null)
                {
                    renderer.color = Color.Lerp(startColor, Color.white, t);
                }
                
                yield return null;
            }

            if (renderer != null)
            {
                renderer.color = Color.white;
            }
        }

        #endregion

        #region Console Logging

        private void LogToUnityConsole(List<string> logs, Dictionary<string, CharacterData> simulatedCharacters)
        {
            string output = $"<b>[MVVM Timeline Bridge] Recalculated up to Slot {targetScrubSlot}</b>\n";
            output += "================ SIMULATION LOGS ==============\n";
            foreach (var log in logs)
            {
                output += log + "\n";
            }
            output += "================ SIMULATED CHARACTERS ================\n";
            foreach (var kvp in simulatedCharacters)
            {
                var chr = kvp.Value;
                output += $"* {chr.name} ({chr.id}) -> HP: {chr.currentHp}/{chr.maxHp} | Shield: {chr.shield}\n";
            }
            Debug.Log(output);
        }

        #endregion
    }

    [System.Serializable]
    public class TimelineActionSetup
    {
        public CardDataBlueprint cardBlueprint;
        [Range(0, 4)] public int startSlot;
        public string overrideSourceId;
        [HideInInspector] public string runtimeInstanceId; // hidden field to retain runtime IDs
    }
}
