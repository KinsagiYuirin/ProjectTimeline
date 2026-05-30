using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Controller managing enemy combat decisions and action scheduling.
    /// Analyzes the current game state to decide which actions the enemy should perform next turn.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        // ── Inspector References ──────────────────────────────────────────────

        [Header("References")]
        [SerializeField]
        [Tooltip("Visual Inspector bridge component managing MVVM loops.")]
        private TimelineInspectorBridge timelineBridge;

        [SerializeField]
        [Tooltip("Reference to the TurnManager orchestrator.")]
        private TurnManager turnManager;

        [Header("AI Card Pools")]
        [SerializeField]
        [Tooltip("List of card blueprint assets utilized for emergency defense.")]
        private List<CardDataBlueprint> emergencyDefensePool;

        [SerializeField]
        [Tooltip("List of card blueprint assets utilized for punishing passive players.")]
        private List<CardDataBlueprint> fullOffensivePool;

        [SerializeField]
        [Tooltip("List of card blueprint assets utilized for default tactical balance.")]
        private List<CardDataBlueprint> defaultTacticalPool;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-discover the bridge reference if not explicitly assigned
            if (timelineBridge == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                timelineBridge = FindFirstObjectByType<TimelineInspectorBridge>();
#else
                timelineBridge = FindObjectOfType<TimelineInspectorBridge>();
#endif
            }

            if (turnManager == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                turnManager = FindFirstObjectByType<TurnManager>();
#else
                turnManager = FindObjectOfType<TurnManager>();
#endif
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyzes combat state and schedules telegraph intentions for the next turn.
        /// Evaluates a reactive decision tree and injects action nodes into the simulation.
        /// </summary>
        public void CalculateNextTurnActions()
        {
            if (timelineBridge == null)
            {
                Debug.LogWarning("[EnemyAIController] Cannot calculate next turn actions: TimelineInspectorBridge is null.");
                return;
            }

            TimelineModel model = timelineBridge.Model;
            if (model == null)
            {
                Debug.LogWarning("[EnemyAIController] Cannot calculate next turn actions: TimelineModel is null.");
                return;
            }

            // 1. Clear existing enemy action lists and setups
            model.enemyActions.Clear();

            if (timelineBridge.initialTimelineActions != null)
            {
                timelineBridge.initialTimelineActions.RemoveAll(setup => 
                    setup == null ||
                    (setup.cardBlueprint != null && setup.cardBlueprint.actionBlueprint != null && setup.cardBlueprint.actionBlueprint.sourceId == CharacterID.Enemy) ||
                    (!string.IsNullOrEmpty(setup.runtimeInstanceId) && setup.runtimeInstanceId.StartsWith("enemy_ai_"))
                );
            }

            int currentTurn = turnManager != null ? turnManager.CurrentTurnNumber : 1;
            if (currentTurn == 1 && model.enemyActions.Count == 0)
            {
                Debug.Log("[EnemyAIController] 🎲 Turn 1 detected! Scheduling randomized normal balanced actions.");
        
                // สุ่มกระจายช่องสล็อตเวลา 2 ช่องแบบไม่ซ้ำกัน[cite: 12]
                List<int> turn1Slots = GetRandomizedSlots(2);
        
                // สุ่มหยิบการ์ดจากกองท่าต่อสู้ปกติ (Default Tactical Pool) มาวางบนสล็อตที่สุ่มได้[cite: 12]
                ScheduleAction(GetRandomCardFromPool(defaultTacticalPool), turn1Slots[0], model);
                ScheduleAction(GetRandomCardFromPool(defaultTacticalPool), turn1Slots[1], model);
        
                timelineBridge.SyncAndRunSimulation();
        
                return; // ออกจากการคำนวณทันทีค่ะ[cite: 12]
            }
            
            // 2. Evaluate Decision Tree Conditions
            
            // CONDITION A (Emergency Defense)
            if (model.baselineCharacters.TryGetValue(CharacterID.Enemy, out CharacterData enemyData))
            {
                float hpThreshold = enemyData.maxHp * 0.35f;
                if (enemyData.currentHp <= hpThreshold)
                {
                    Debug.Log($"[EnemyAIController] 🚨 CONDITION A: Emergency Defense triggered (HP: {enemyData.currentHp}/{enemyData.maxHp} <= 35%).");
                    List<int> chosenSlots = GetRandomizedSlots(2);
                    ScheduleAction(GetRandomCardFromPool(emergencyDefensePool), chosenSlots[0], model);
                    ScheduleAction(GetRandomCardFromPool(emergencyDefensePool), chosenSlots[1], model);
                    
                    timelineBridge.SyncAndRunSimulation();
                    return;
                }
            }

            // CONDITION B (Punish Passive Player)
            int playerAttackCount = 0;
            foreach (var action in model.playerActions)
            {
                if (action != null && action.actionType == ActionType.Attack)
                {
                    playerAttackCount++;
                }
            }

            if (playerAttackCount == 0)
            {
                Debug.Log("[EnemyAIController] 🔥 CONDITION B: Punishing passive player (0 player attacks detected). Going full offensive.");
                List<int> chosenSlots = GetRandomizedSlots(3);
                ScheduleAction(GetRandomCardFromPool(fullOffensivePool), chosenSlots[0], model);
                ScheduleAction(GetRandomCardFromPool(fullOffensivePool), chosenSlots[1], model);
                ScheduleAction(GetRandomCardFromPool(fullOffensivePool), chosenSlots[2], model);
                
                timelineBridge.SyncAndRunSimulation();
                return;
            }

            // CONDITION C (Default Tactical Balance)
            Debug.Log("[EnemyAIController] ⚖️ CONDITION C: Default Tactical Balance scheduled.");
            List<int> defaultSlots = GetRandomizedSlots(2);
            ScheduleAction(GetRandomCardFromPool(defaultTacticalPool), defaultSlots[0], model);
            ScheduleAction(GetRandomCardFromPool(defaultTacticalPool), defaultSlots[1], model);

            timelineBridge.SyncAndRunSimulation();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches a random card asset safely from a pool.
        /// </summary>
        private CardDataBlueprint GetRandomCardFromPool(List<CardDataBlueprint> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int index = UnityEngine.Random.Range(0, pool.Count);
            return pool[index];
        }

        /// <summary>
        /// Generates a list of randomized, unique slot indices.
        /// </summary>
        private List<int> GetRandomizedSlots(int count)
        {
            List<int> availableSlots = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = 0; i < availableSlots.Count; i++)
            {
                int temp = availableSlots[i];
                int randomIndex = UnityEngine.Random.Range(i, availableSlots.Count);
                availableSlots[i] = availableSlots[randomIndex];
                availableSlots[randomIndex] = temp;
            }
            count = Mathf.Clamp(count, 0, availableSlots.Count);
            return availableSlots.GetRange(0, count);
        }

        /// <summary>
        /// Creates an action node from a card blueprint, adds it to the model, and registers it with the visualizer bridge.
        /// </summary>
        private void ScheduleAction(CardDataBlueprint cardBlueprint, int slot, TimelineModel model)
        {
            if (cardBlueprint == null || cardBlueprint.actionBlueprint == null) return;

            // Deep copy/clone the action node directly from the provided card asset
            ActionNodeData node = cardBlueprint.actionBlueprint.Clone();

            // Inject the dynamic runtime properties into the cloned node
            node.id = $"enemy_ai_{node.actionType.ToString().ToLower()}_{slot}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            node.startSlot = slot;
            node.effectiveSlot = slot;
            node.sourceId = CharacterID.Enemy;
            node.targetId = (node.actionType == ActionType.Defend) ? CharacterID.Enemy : CharacterID.Player;

            // Push the fully prepped node into model.enemyActions
            model.enemyActions.Add(node);

            // Create TimelineActionSetup using the original card blueprint asset reference directly
            TimelineActionSetup setup = new TimelineActionSetup
            {
                cardBlueprint = cardBlueprint,
                startSlot = slot,
                runtimeInstanceId = node.id
            };

            // Add to timeline bridge list
            if (timelineBridge.initialTimelineActions == null)
            {
                timelineBridge.initialTimelineActions = new List<TimelineActionSetup>();
            }
            timelineBridge.initialTimelineActions.Add(setup);

            Debug.Log($"[EnemyAIController] Scheduled action {node.actionType} from Card Blueprint '{cardBlueprint.cardName}' on Slot {slot} (ID: {node.id})");
        }
    }
}
