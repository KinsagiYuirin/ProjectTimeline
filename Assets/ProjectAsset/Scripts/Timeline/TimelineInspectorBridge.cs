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
        public List<ActionNodeData> timelineActions = new List<ActionNodeData>()
        {
            new ActionNodeData("enemy_atk", "enemy", "player", 1, ActionType.Attack, 20),
            new ActionNodeData("player_def", "player", "player", 1, ActionType.Defend, 10),
            new ActionNodeData("player_delay", "player", "enemy", 1, ActionType.Delay, 1)
        };

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
            // Protect against accessing lists during editor deserialization/destruction states
            if (baselineCharacters == null || timelineActions == null) return;

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
            foreach (var action in timelineActions)
            {
                if (action == null || string.IsNullOrEmpty(action.id)) continue;

                if (action.sourceId == "player")
                {
                    model.playerActions.Add(action);
                }
                else
                {
                    model.enemyActions.Add(action);
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
}
