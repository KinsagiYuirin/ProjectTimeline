using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Represents the core action types supported on the timeline model.
    /// </summary>
    public enum ActionType
    {
        Attack,
        Defend,
        Delay
    }

    public enum CharacterID
    {
        Player,
        Enemy
    }

    public enum DelayTargetMode
    {
        ByActionType,     // Original: Delays only the matching ActionType (e.g. Attack only)
        AllActionsInSlot   // New Option: Delays every action belonging to the target in that slot
    }

    public enum StatusEffectType
    {
        None,
        Poison,
        Burn,
        Weak,
        Vulnerable,
        Stun,
        Freeze
    }

    public enum CardType
    {
        Normal,
        Combo
    }

    public enum CardSpeed
    {
        Slow = -1,
        Normal = 0,
        Fast = 1,
        Instant = 2
    }

    /// <summary>
    /// Pure C# Model that implements the Instant Recalculation pattern.
    /// Evaluates the combat outcomes for timeline slots from 0 to upToSlot.
    /// </summary>
    public class TimelineModel
    {
        public const int TIMELINE_SLOTS = 5;

        // Tracks bonus card draws scheduled for the next turn commit
        public int bonusDrawNextTurn = 0;

        // Baseline (start-of-turn) snapshot of combatants
        public Dictionary<CharacterID, CharacterData> baselineCharacters = new Dictionary<CharacterID, CharacterData>();
        
        // Computed preview state at the playhead position
        public Dictionary<CharacterID, CharacterData> simulatedCharacters = new Dictionary<CharacterID, CharacterData>();

        // Lists of telegraphed actions and player played cards
        public List<ActionNodeData> enemyActions = new List<ActionNodeData>();
        public List<ActionNodeData> playerActions = new List<ActionNodeData>();

        // Cache of the actions representing their simulated state (e.g. final effective slot positions)
        public List<ActionNodeData> simulatedActions = new List<ActionNodeData>();

        // Execution history for UI/Console output
        public List<string> simulationLog = new List<string>();

        private class SimulatedNode
        {
            public ActionNodeData node;
            public int effectiveSlot;
            public bool processed;

            public SimulatedNode(ActionNodeData source)
            {
                node = source;
                effectiveSlot = source.startSlot;
                node.effectiveSlot = source.startSlot;
                processed = false;
            }
        }

        // Temporary reference to the active simulation nodes during simulation run
        private List<SimulatedNode> activeSimNodes;

        /// <summary>
        /// Simulates all actions slot-by-slot chronologically in a single frame.
        /// </summary>
        public void SimulateUpTo(int upToSlot)
        {
            // Clamp slot index to valid bounds
            upToSlot = Math.Min(TIMELINE_SLOTS - 1, Math.Max(0, upToSlot));

            // 1. Wipe simulated characters and restore to turn-start baseline clones
            simulatedCharacters.Clear();
            foreach (var kvp in baselineCharacters)
            {
                simulatedCharacters[kvp.Key] = kvp.Value.Clone();
            }

            simulationLog.Clear();
            simulationLog.Add($"--- MVVM SIMULATION START (Scrub Slot {upToSlot}) ---");

            // 2. Queue all active actions for simulation
            List<SimulatedNode> simNodes = new List<SimulatedNode>();
            foreach (var act in enemyActions) simNodes.Add(new SimulatedNode(act.Clone()));
            foreach (var act in playerActions) simNodes.Add(new SimulatedNode(act.Clone()));

            activeSimNodes = simNodes;

            // Process player card combos before chronological slot evaluation
            ProcessCardCombos(simulationLog);

            // 3. Chronological slot evaluation loop
            for (int slot = 0; slot <= upToSlot; slot++)
            {
                simulationLog.Add($"[Slot {slot}] Processing actions...");

                // Tick Status Effects at start of slot for active characters
                foreach (var character in simulatedCharacters.Values)
                {
                    character.TickStatusEffects(simulationLog);
                }

                // FREEZE SHIFT PHASE
                // If a character has a "Freeze" status effect in this slot, it delays all their future actions by 1 slot
                foreach (var characterKvp in simulatedCharacters)
                {
                    var charId = characterKvp.Key;
                    var charData = characterKvp.Value;
                    var freeze = charData.statusEffects.Find(s => s.effectType == StatusEffectType.Freeze);
                    if (freeze != null && freeze.duration > 0)
                    {
                        simulationLog.Add($"     * [FROZEN] {charData.name} is Frozen! Shifting all future actions by 1 slot.");
                        ApplyDelay(charId, charId, slot, 1, false, ActionType.Attack, DelayTargetMode.AllActionsInSlot, simulationLog);
                    }
                }

                // Process Phase: Delay (always resolve delays first to compute shifts correctly)
                ProcessPhase(slot, EffectPhase.Delay, simulationLog);

                // Check for conflicts: multiple exclusive actions on the same character at the same slot
                CheckSlotConflicts(slot, simulationLog);

                // Resolve Attack Clash (offsetting player and enemy damage values in the same slot)
                ResolveAttackClashes(slot, simulationLog);

                // Gather active actions in this slot that are not processed
                List<SimulatedNode> slotNodes = activeSimNodes.FindAll(n => 
                    !n.processed && n.effectiveSlot == slot
                );

                // Sort actions by Global Slot Priority (Speed descending, then Type order, then Player first)
                slotNodes.Sort((a, b) => {
                    // 1. Priority (Speed) - descending
                    int comparePriority = ((int)b.node.cardSpeed).CompareTo((int)a.node.cardSpeed);
                    if (comparePriority != 0) return comparePriority;

                    // 2. Type order (Delay < Defend < Attack < Utility) - ascending
                    int aOrder = GetPhaseOrder(a.node.actionType);
                    int bOrder = GetPhaseOrder(b.node.actionType);
                    int compareType = aOrder.CompareTo(bOrder);
                    if (compareType != 0) return compareType;

                    // 3. Source ID (Player < Enemy) - ascending
                    return a.node.sourceId.CompareTo(b.node.sourceId);
                });

                // Execute sorted actions one by one
                foreach (var nodeWrapper in slotNodes)
                {
                    var action = nodeWrapper.node;

                    // Check if the source character is Stunned or Frozen (skips action execution in this slot)
                    if (simulatedCharacters.TryGetValue(action.sourceId, out var srcData))
                    {
                        var stun = srcData.statusEffects.Find(s => s.effectType == StatusEffectType.Stun);
                        var freeze = srcData.statusEffects.Find(s => s.effectType == StatusEffectType.Freeze);
                        if ((stun != null && stun.duration > 0) || (freeze != null && freeze.duration > 0))
                        {
                            string reason = stun != null ? "Stunned" : "Frozen";
                            simulationLog.Add($"     * [{reason.ToUpper()}] {srcData.name} is {reason}! Skipping action.");
                            nodeWrapper.processed = true;
                            continue;
                        }
                    }

                    // Execute all remaining effects of the card (excluding Delay which ran first)
                    if (action.effects != null && action.effects.Count > 0)
                    {
                        var remainingEffects = action.effects.FindAll(e => e != null && e.Phase != EffectPhase.Delay);
                        foreach (var effect in remainingEffects)
                        {
                            effect.Execute(action.sourceId, action.targetId, action.value, this, slot, action, simulationLog);
                        }
                    }

                    nodeWrapper.processed = true;
                }
            }

            simulationLog.Add($"--- MVVM SIMULATION END ---");

            // 4. Cache final simulated action states for View/ViewModel queries
            simulatedActions.Clear();
            foreach (var simNode in simNodes)
            {
                simulatedActions.Add(simNode.node);
            }

            activeSimNodes = null; // Clean up active reference
        }

        private int GetPhaseOrder(ActionType type)
        {
            switch (type)
            {
                case ActionType.Delay: return 0;
                case ActionType.Defend: return 1;
                case ActionType.Attack: return 2;
                default: return 3;
            }
        }

        private void ProcessPhase(int slot, EffectPhase phase, List<string> logs)
        {
            if (activeSimNodes == null) return;

            // Find nodes executing in this slot that are not processed
            List<SimulatedNode> phaseNodes = activeSimNodes.FindAll(n => 
                !n.processed && n.effectiveSlot == slot
            );

            foreach (var nodeWrapper in phaseNodes)
            {
                var action = nodeWrapper.node;

                // Check if the source character is Stunned or Frozen (skips action execution in this slot)
                if (simulatedCharacters.TryGetValue(action.sourceId, out var srcData))
                {
                    var stun = srcData.statusEffects.Find(s => s.effectType == StatusEffectType.Stun);
                    var freeze = srcData.statusEffects.Find(s => s.effectType == StatusEffectType.Freeze);
                    if ((stun != null && stun.duration > 0) || (freeze != null && freeze.duration > 0))
                    {
                        string reason = stun != null ? "Stunned" : "Frozen";
                        logs.Add($"     * [{reason.ToUpper()}] {srcData.name} is {reason}! Skipping action.");
                        nodeWrapper.processed = true; // Skip executing any other phase of this action
                        continue;
                    }
                }

                if (action.effects != null && action.effects.Count > 0)
                {
                    // Execute all custom effects matching the phase
                    var matchingEffects = action.effects.FindAll(e => e != null && e.Phase == phase);
                    foreach (var effect in matchingEffects)
                    {
                        effect.Execute(action.sourceId, action.targetId, action.value, this, slot, action, logs);
                    }
                }
            }
        }

        private void CheckSlotConflicts(int slot, List<string> logs)
        {
            if (activeSimNodes == null) return;

            List<SimulatedNode> slotActions = activeSimNodes.FindAll(n => !n.processed && n.effectiveSlot == slot);
            HashSet<CharacterID> exclusiveUsers = new HashSet<CharacterID>();
            HashSet<CharacterID> flaggedUsers = new HashSet<CharacterID>();
            foreach (var wrapper in slotActions)
            {
                if (wrapper.node.isExclusive)
                {
                    if (!exclusiveUsers.Add(wrapper.node.sourceId))
                    {
                        if (flaggedUsers.Add(wrapper.node.sourceId))
                        {
                            string sourceName = GetName(wrapper.node.sourceId);
                            logs.Add($"[Conflict Slot {slot}] {sourceName} cannot execute multiple exclusive actions!");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// API for modular effects to delay targets on the timeline.
        /// </summary>
        public void ApplyDelay(
            CharacterID sourceId, 
            CharacterID targetId, 
            int slot, 
            int value, 
            bool isTargetedDelay, 
            ActionType targetActionType, 
            DelayTargetMode delayTargetMode, 
            List<string> logs
        )
        {
            if (activeSimNodes == null) return;

            string src = GetName(sourceId);
            string tgt = GetName(targetId);

            if (isTargetedDelay)
            {
                logs.Add($"  -> [TARGETED DELAY] {src} targeted delay resolved at slot {slot} (Mode: {delayTargetMode}, targets CharacterID.{targetId}).");

                foreach (var other in activeSimNodes)
                {
                    if (!other.processed && 
                        other.effectiveSlot == slot && 
                        other.node.sourceId == targetId)
                    {
                        if (delayTargetMode == DelayTargetMode.ByActionType)
                        {
                            if (other.node.actionType == targetActionType)
                            {
                                int oldSlot = other.effectiveSlot;
                                other.effectiveSlot = Math.Min(other.effectiveSlot + 1, TIMELINE_SLOTS - 1);
                                other.node.effectiveSlot = other.effectiveSlot;
                                logs.Add($"     * Action '{other.node.actionType}' shifted: Slot {oldSlot} -> Slot {other.effectiveSlot}");
                            }
                        }
                        else if (delayTargetMode == DelayTargetMode.AllActionsInSlot)
                        {
                            int oldSlot = other.effectiveSlot;
                            other.effectiveSlot = Math.Min(other.effectiveSlot + 1, TIMELINE_SLOTS - 1);
                            other.node.effectiveSlot = other.effectiveSlot;
                            logs.Add($"     * [Slot-Wide Delay] Shifted action '{other.node.actionType}' from Slot {slot} to {other.effectiveSlot}");
                        }
                    }
                }
            }
            else
            {
                logs.Add($"  -> [DELAY] {src} delays {tgt} by {value} slots.");

                foreach (var other in activeSimNodes)
                {
                    if (!other.processed && 
                        other.node.sourceId == targetId && 
                        other.effectiveSlot >= slot)
                    {
                        int oldSlot = other.effectiveSlot;
                        other.effectiveSlot = Math.Min(TIMELINE_SLOTS, other.effectiveSlot + value);
                        other.node.effectiveSlot = other.effectiveSlot;
                        logs.Add($"     * Action '{other.node.actionType}' shifted: Slot {oldSlot} -> Slot {other.effectiveSlot}");
                    }
                }
            }
        }

        private void ResolveAttackClashes(int slot, List<string> logs)
        {
            if (activeSimNodes == null) return;

            var slotNodes = activeSimNodes.FindAll(n => !n.processed && n.effectiveSlot == slot);
            
            var playerAttacks = slotNodes.FindAll(n => n.node.sourceId == CharacterID.Player && n.node.effects.Exists(e => e is DamageEffect));
            var enemyAttacks = slotNodes.FindAll(n => n.node.sourceId == CharacterID.Enemy && n.node.effects.Exists(e => e is DamageEffect));

            if (playerAttacks.Count > 0 && enemyAttacks.Count > 0)
            {
                int playerTotal = 0;
                foreach (var a in playerAttacks) playerTotal += a.node.value;

                int enemyTotal = 0;
                foreach (var a in enemyAttacks) enemyTotal += a.node.value;

                if (playerTotal == enemyTotal)
                {
                    foreach (var a in playerAttacks) a.node.value = 0;
                    foreach (var a in enemyAttacks) a.node.value = 0;
                    logs.Add($"⚔️ [CLASH] Player Attack ({playerTotal} dmg) meets Enemy Attack ({enemyTotal} dmg). Both offset completely!");
                }
                else if (playerTotal > enemyTotal)
                {
                    int remaining = playerTotal - enemyTotal;
                    foreach (var a in enemyAttacks) a.node.value = 0;
                    
                    for (int i = 0; i < playerAttacks.Count; i++)
                    {
                        playerAttacks[i].node.value = (i == 0) ? remaining : 0;
                    }
                    logs.Add($"⚔️ [CLASH] Player Attack ({playerTotal} dmg) vs Enemy Attack ({enemyTotal} dmg). Player wins! Remaining dmg: {remaining}");
                }
                else
                {
                    int remaining = enemyTotal - playerTotal;
                    foreach (var a in playerAttacks) a.node.value = 0;
                    
                    for (int i = 0; i < enemyAttacks.Count; i++)
                    {
                        enemyAttacks[i].node.value = (i == 0) ? remaining : 0;
                    }
                    logs.Add($"⚔️ [CLASH] Enemy Attack ({enemyTotal} dmg) vs Player Attack ({playerTotal} dmg). Enemy wins! Remaining dmg: {remaining}");
                }
            }
        }
        private void ProcessCardCombos(List<string> logs)
        {
            if (activeSimNodes == null) return;

            // Find all player actions
            var playerNodes = activeSimNodes.FindAll(n => n.node.sourceId == CharacterID.Player);
            
            // To check adjacency easily, store player start slots
            HashSet<int> playerStartSlots = new HashSet<int>();
            foreach (var n in playerNodes)
            {
                playerStartSlots.Add(n.node.startSlot);
            }

            foreach (var wrapper in playerNodes)
            {
                var node = wrapper.node;
                if (node.cardType == CardType.Combo)
                {
                    // Check if there is any player action in the preceding slot
                    if (playerStartSlots.Contains(node.startSlot - 1))
                    {
                        int oldValue = node.value;
                        node.value = Mathf.RoundToInt(node.value * node.comboValueMultiplier) + node.comboValueBonus;
                        
                        // Append combo effects to active effects list
                        if (node.comboEffects != null && node.comboEffects.Count > 0)
                        {
                            node.effects.AddRange(node.comboEffects);
                        }

                        logs.Add($"✨ [COMBO ACTIVATED] Combo Card '{node.id}' in Slot {node.startSlot} triggered! Value: {oldValue} -> {node.value}");
                    }
                    else
                    {
                        logs.Add($"⚠️ [COMBO FAILED] Combo Card '{node.id}' in Slot {node.startSlot} requires a preceding card in Slot {node.startSlot - 1}.");
                    }
                }
            }
        }
        private string GetName(CharacterID id)
        {
            if (baselineCharacters.TryGetValue(id, out var character))
            {
                return character.name;
            }
            return id.ToString();
        }
    }
}
