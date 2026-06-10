using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Controller managing enemy combat decisions and action scheduling.
    /// Analyzes the current game state to decide which actions the enemy should perform next turn.
    /// Uses modular ScriptableObject profiles to evaluate rules and run slot strategies.
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

        [Header("AI Behavior Setup")]
        [SerializeField]
        [Tooltip("The ScriptableObject profile containing behavior rules for this enemy.")]
        private EnemyAIProfileSO aiProfile;

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
        /// Evaluates a reactive decision tree configured via ScriptableObjects.
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

            if (aiProfile == null)
            {
                Debug.LogWarning("[EnemyAIController] Cannot calculate next turn actions: EnemyAIProfileSO is null.");
                return;
            }

            // 1. Gather player actions from the turn being committed (before they are cleared)
            int lastPlayerAttacks = 0;
            int lastPlayerDefends = 0;
            if (model.playerActions != null)
            {
                foreach (var action in model.playerActions)
                {
                    if (action != null)
                    {
                        if (action.actionType == ActionType.Attack) lastPlayerAttacks++;
                        else if (action.actionType == ActionType.Defend) lastPlayerDefends++;
                    }
                }
            }

            // Clean up existing enemy actions in the bridge and simulation
            model.enemyActions.Clear();

            if (timelineBridge.initialTimelineActions != null)
            {
                timelineBridge.initialTimelineActions.RemoveAll(setup => 
                    setup == null ||
                    (setup.cardBlueprint != null && setup.cardBlueprint.actionBlueprint != null && setup.cardBlueprint.actionBlueprint.sourceId == CharacterID.Enemy) ||
                    (!string.IsNullOrEmpty(setup.runtimeInstanceId) && setup.runtimeInstanceId.StartsWith("enemy_ai_"))
                );
            }

            // Gather combatant baseline stats
            model.baselineCharacters.TryGetValue(CharacterID.Enemy, out CharacterData selfData);
            model.baselineCharacters.TryGetValue(CharacterID.Player, out CharacterData targetData);
            int currentTurn = turnManager != null ? turnManager.CurrentTurnNumber : 1;

            Debug.Log($"[EnemyAIController] Evaluating behavior profile '{aiProfile.name}' (Turn: {currentTurn}, Self HP: {(selfData != null ? selfData.currentHp : 0)}, Player HP: {(targetData != null ? targetData.currentHp : 0)})");

            // 2. Evaluate Rule Tree sequentially
            bool ruleMatched = false;
            foreach (var rule in aiProfile.rules)
            {
                if (rule == null) continue;

                if (EvaluateCondition(rule.condition, selfData, targetData, currentTurn, lastPlayerAttacks, lastPlayerDefends))
                {
                    Debug.Log($"[EnemyAIController] ★ Behavior rule matching: '{rule.ruleName}'");
                    ExecuteRuleDecisions(rule.decisions, model);
                    ruleMatched = true;
                    break;
                }
            }

            // 3. Fallback if no rules matched
            if (!ruleMatched)
            {
                Debug.Log("[EnemyAIController] ⚠️ No behavior rules matched. Scheduling default fallback actions.");
                ExecuteFallback(model);
            }

            timelineBridge.SyncAndRunSimulation();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AI Decisions & Placement Resolvers
        // ─────────────────────────────────────────────────────────────────────

        private bool EvaluateCondition(AICondition cond, CharacterData self, CharacterData target, int currentTurn, int playerAttacks, int playerDefends)
        {
            if (cond == null) return true;

            switch (cond.type)
            {
                case AICondition.ConditionType.Always:
                    return true;

                case AICondition.ConditionType.SelfHPLessThanOrEqual:
                    if (self == null || self.maxHp <= 0) return false;
                    return ((float)self.currentHp / self.maxHp) <= cond.thresholdValue;

                case AICondition.ConditionType.PlayerHPLessThanOrEqual:
                    if (target == null || target.maxHp <= 0) return false;
                    return ((float)target.currentHp / target.maxHp) <= cond.thresholdValue;

                case AICondition.ConditionType.TurnNumberEqual:
                    return currentTurn == Mathf.RoundToInt(cond.thresholdValue);

                case AICondition.ConditionType.PlayerLastActionsPassive:
                    return playerAttacks == 0;

                case AICondition.ConditionType.PlayerLastActionsAggressive:
                    return playerAttacks >= Mathf.RoundToInt(cond.thresholdValue);

                case AICondition.ConditionType.SelfHasStatus:
                    if (self == null || self.statusEffects == null) return false;
                    return self.statusEffects.Exists(s => s.effectType == cond.statusToCheck && s.duration > 0);

                case AICondition.ConditionType.TargetHasStatus:
                    if (target == null || target.statusEffects == null) return false;
                    return target.statusEffects.Exists(s => s.effectType == cond.statusToCheck && s.duration > 0);

                default:
                    return true;
            }
        }

        private void ExecuteRuleDecisions(List<AIActionDecision> decisions, TimelineModel model)
        {
            HashSet<int> occupiedSlots = new HashSet<int>();

            foreach (var dec in decisions)
            {
                if (dec == null) continue;

                // Resolve card choice
                CardDataBlueprint chosenCard = dec.specificCard;
                if (chosenCard == null)
                {
                    chosenCard = GetRandomCardFromPool(dec.cardPool);
                }
                if (chosenCard == null || chosenCard.actionBlueprint == null) continue;

                // Resolve slot placement
                int slot = ResolveSlot(dec.slotStrategy, dec.fixedSlotIndex, model.playerActions, occupiedSlots);
                if (slot >= 0)
                {
                    occupiedSlots.Add(slot);
                    ScheduleAction(chosenCard, slot, model);
                }
            }
        }

        private void ExecuteFallback(TimelineModel model)
        {
            if (aiProfile.fallbackCardPool == null || aiProfile.fallbackCardPool.Count == 0) return;

            HashSet<int> occupiedSlots = new HashSet<int>();
            // Schedule 2 fallback actions in random slots
            for (int i = 0; i < 2; i++)
            {
                CardDataBlueprint card = GetRandomCardFromPool(aiProfile.fallbackCardPool);
                if (card != null)
                {
                    int slot = ResolveSlot(AIActionDecision.SlotStrategy.RandomEmpty, 0, model.playerActions, occupiedSlots);
                    if (slot >= 0)
                    {
                        occupiedSlots.Add(slot);
                        ScheduleAction(card, slot, model);
                    }
                }
            }
        }

        private int ResolveSlot(AIActionDecision.SlotStrategy strategy, int fixedSlot, List<ActionNodeData> playerLastActions, HashSet<int> occupiedSlots)
        {
            switch (strategy)
            {
                case AIActionDecision.SlotStrategy.FixedSlot:
                    if (!occupiedSlots.Contains(fixedSlot))
                    {
                        return fixedSlot;
                    }
                    break; // Fallback to random empty if occupied

                case AIActionDecision.SlotStrategy.ClashPlayerAttack:
                    if (playerLastActions != null)
                    {
                        foreach (var act in playerLastActions)
                        {
                            if (act != null && act.actionType == ActionType.Attack && !occupiedSlots.Contains(act.startSlot))
                            {
                                return act.startSlot;
                            }
                        }
                    }
                    break; // Fallback to random empty if no player attack or slot occupied

                case AIActionDecision.SlotStrategy.CounterPlayerDefend:
                    if (playerLastActions != null)
                    {
                        foreach (var act in playerLastActions)
                        {
                            if (act != null && act.actionType == ActionType.Defend && !occupiedSlots.Contains(act.startSlot))
                            {
                                return act.startSlot;
                            }
                        }
                    }
                    break; // Fallback to random empty if no player defense or slot occupied
            }

            // Fallback: Pick any random empty slot
            List<int> freeSlots = new List<int>();
            for (int i = 0; i < TimelineModel.TIMELINE_SLOTS; i++)
            {
                if (!occupiedSlots.Contains(i))
                {
                    freeSlots.Add(i);
                }
            }

            if (freeSlots.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, freeSlots.Count);
                return freeSlots[index];
            }

            return -1; // No slots left
        }

        private CardDataBlueprint GetRandomCardFromPool(List<CardDataBlueprint> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int index = UnityEngine.Random.Range(0, pool.Count);
            return pool[index];
        }

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
            node.cardType = cardBlueprint.cardType; // Keep cardType synchronized!
            node.cardSpeed = cardBlueprint.cardSpeed; // Keep cardSpeed synchronized!

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
