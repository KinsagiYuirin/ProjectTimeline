using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    [System.Serializable]
    public class AICondition
    {
        public enum ConditionType
        {
            Always,
            SelfHPLessThanOrEqual,
            PlayerHPLessThanOrEqual,
            TurnNumberEqual,
            PlayerLastActionsPassive,
            PlayerLastActionsAggressive,
            SelfHasStatus,
            TargetHasStatus
        }

        [Tooltip("The category of condition to evaluate.")]
        public ConditionType type = ConditionType.Always;

        [Tooltip("Value used for HP ratio (e.g., 0.35 for 35%), turn number, or action thresholds.")]
        public float thresholdValue;

        [Tooltip("The specific status type to check for status-based conditions.")]
        public StatusEffectType statusToCheck = StatusEffectType.None;
    }

    [System.Serializable]
    public class AIActionDecision
    {
        public enum SlotStrategy
        {
            RandomEmpty,
            FixedSlot,
            ClashPlayerAttack,
            CounterPlayerDefend
        }

        [Header("Card Selection")]
        [Tooltip("If assigned, triggers this specific card. Otherwise, picks from the pool below.")]
        public CardDataBlueprint specificCard;

        [Tooltip("Pool of cards to select randomly from if specificCard is not assigned.")]
        public List<CardDataBlueprint> cardPool = new List<CardDataBlueprint>();

        [Header("Slot Strategy")]
        [Tooltip("How the slot is selected for this action.")]
        public SlotStrategy slotStrategy = SlotStrategy.RandomEmpty;

        [Range(0, 4)]
        [Tooltip("Target slot index if FixedSlot is selected.")]
        public int fixedSlotIndex = 0;
    }

    [System.Serializable]
    public class AIRule
    {
        public string ruleName;
        public AICondition condition;
        public List<AIActionDecision> decisions = new List<AIActionDecision>();
    }

    /// <summary>
    /// ScriptableObject defining an enemy combat behavior profile.
    /// Lists prioritized behavior rules evaluated chronologically from top to bottom.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyAIProfile", menuName = "Project Timeline/Enemy AI Profile")]
    public class EnemyAIProfileSO : ScriptableObject
    {
        [Tooltip("Ordered rules evaluated during combat setup. The first rule to pass all conditions runs.")]
        public List<AIRule> rules = new List<AIRule>();

        [Tooltip("Fallback card pool if no rules match their conditions.")]
        public List<CardDataBlueprint> fallbackCardPool = new List<CardDataBlueprint>();
    }
}
