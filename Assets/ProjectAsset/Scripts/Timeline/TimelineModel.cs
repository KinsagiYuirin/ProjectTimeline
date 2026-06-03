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

    /// <summary>
    /// Model representation of a combatant's attributes.
    /// Marked serializable so it can be exposed and configured directly inside the Unity Inspector.
    /// </summary>
    [Serializable]
    public class StatusEffectInstance
    {
        public string statusId;
        public int duration;
        public int intensity;

        public StatusEffectInstance(string statusId, int duration, int intensity)
        {
            this.statusId = statusId;
            this.duration = duration;
            this.intensity = intensity;
        }

        public StatusEffectInstance Clone()
        {
            return new StatusEffectInstance(statusId, duration, intensity);
        }
    }

    /// <summary>
    /// Model representation of a combatant's attributes.
    /// Marked serializable so it can be exposed and configured directly inside the Unity Inspector.
    /// </summary>
    [Serializable]
    public class CharacterData
    {
        public string id;
        public string name;
        public int maxHp;
        public int currentHp;
        public int shield;
        public List<StatusEffectInstance> statusEffects = new List<StatusEffectInstance>();

        public CharacterData() { }

        public CharacterData(string id, string name, int maxHp, int currentHp, int shield)
        {
            this.id = id;
            this.name = name;
            this.maxHp = maxHp;
            this.currentHp = currentHp;
            this.shield = shield;
        }

        /// <summary>
        /// Creates a deep copy of the CharacterData for simulation previews.
        /// </summary>
        public CharacterData Clone()
        {
            var cloned = new CharacterData(id, name, maxHp, currentHp, shield);
            if (statusEffects != null)
            {
                foreach (var status in statusEffects)
                {
                    cloned.statusEffects.Add(status.Clone());
                }
            }
            return cloned;
        }

        /// <summary>
        /// Applies damage, taking active shields into account first.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;

            if (shield > 0)
            {
                if (shield >= amount)
                {
                    shield -= amount;
                    amount = 0;
                }
                else
                {
                    amount -= shield;
                    shield = 0;
                }
            }

            currentHp = Math.Max(0, currentHp - amount);
        }

        /// <summary>
        /// Adds shield to protect the character.
        /// </summary>
        public void AddShield(int amount)
        {
            if (amount <= 0) return;
            shield += amount;
        }

        /// <summary>
        /// Adds a status effect (e.g. Poison, Weak, Vulnerable).
        /// </summary>
        public void ApplyStatus(string statusId, int duration, int intensity)
        {
            if (statusEffects == null) statusEffects = new List<StatusEffectInstance>();

            var existing = statusEffects.Find(s => s.statusId.Equals(statusId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.duration = Math.Max(existing.duration, duration);
                existing.intensity = Math.Max(existing.intensity, intensity);
            }
            else
            {
                statusEffects.Add(new StatusEffectInstance(statusId, duration, intensity));
            }
        }

        /// <summary>
        /// Resolves ticking status effects at the start of a slot.
        /// </summary>
        public void TickStatusEffects(List<string> logs)
        {
            if (statusEffects == null || statusEffects.Count == 0) return;

            List<StatusEffectInstance> expired = new List<StatusEffectInstance>();

            for (int i = 0; i < statusEffects.Count; i++)
            {
                var status = statusEffects[i];
                if (status.duration <= 0)
                {
                    expired.Add(status);
                    continue;
                }

                if (status.statusId.Equals("Poison", StringComparison.OrdinalIgnoreCase))
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity);
                    logs.Add($"     * [Poison Tick] {name} takes {status.intensity} poison dmg. (HP: {oldHp}->{currentHp})");
                }
                else if (status.statusId.Equals("Burn", StringComparison.OrdinalIgnoreCase))
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity);
                    logs.Add($"     * [Burn Tick] {name} takes {status.intensity} burn dmg. (HP: {oldHp}->{currentHp})");
                }

                status.duration--;
                if (status.duration <= 0)
                {
                    expired.Add(status);
                }
            }

            foreach (var exp in expired)
            {
                statusEffects.Remove(exp);
                logs.Add($"     * Status '{exp.statusId}' on {name} has expired.");
            }
        }
    }

    /// <summary>
    /// Model representation of a timeline action segment.
    /// Marked serializable so it can be exposed and configured directly inside the Unity Inspector.
    /// </summary>
    [Serializable]
    public class ActionNodeData
    {
        public string id;
        public CharacterID sourceId;
        public CharacterID targetId;
        public int startSlot; // Index of slot (0 to 4)
        public int effectiveSlot; // Where the action actually executes after delays (dynamic)
        public bool isExclusive; // True: Main Action (cannot overlap), False: Free Action (can overlap)
        public int value; // Damage amount, Shield amount, Delay slots

        [Header("Modular Action Effects")]
        public List<CombatEffect> effects = new List<CombatEffect>();

        /// <summary>
        /// Derived ActionType based on custom effects for legacy compatibility.
        /// </summary>
        public ActionType actionType
        {
            get
            {
                if (effects != null && effects.Count > 0)
                {
                    foreach (var effect in effects)
                    {
                        if (effect is DamageEffect) return ActionType.Attack;
                        if (effect is ShieldEffect) return ActionType.Defend;
                        if (effect is DelayEffect) return ActionType.Delay;
                    }
                }
                return ActionType.Attack; // Default fallback
            }
        }

        public ActionNodeData() { }

        public ActionNodeData(string id, CharacterID sourceId, CharacterID targetId, int startSlot, ActionType legacyType, int value, bool isExclusive = true)
        {
            this.id = id;
            this.sourceId = sourceId;
            this.targetId = targetId;
            this.startSlot = startSlot;
            this.effectiveSlot = startSlot; // Default to start slot
            this.isExclusive = isExclusive;
            this.value = value;

            // Automatically populate modular effects for programmatically created actions
            switch (legacyType)
            {
                case ActionType.Attack:
                    effects.Add(ScriptableObject.CreateInstance<DamageEffect>());
                    break;
                case ActionType.Defend:
                    effects.Add(ScriptableObject.CreateInstance<ShieldEffect>());
                    break;
                case ActionType.Delay:
                    var delayEffect = ScriptableObject.CreateInstance<DelayEffect>();
                    delayEffect.isTargetedDelay = false;
                    effects.Add(delayEffect);
                    break;
            }
        }

        /// <summary>
        /// Deep copies the action node configuration.
        /// </summary>
        public ActionNodeData Clone()
        {
            var cloned = new ActionNodeData
            {
                id = this.id,
                sourceId = this.sourceId,
                targetId = this.targetId,
                startSlot = this.startSlot,
                effectiveSlot = this.effectiveSlot,
                isExclusive = this.isExclusive,
                value = this.value
            };
            if (this.effects != null)
            {
                cloned.effects = new List<CombatEffect>(this.effects);
            }
            return cloned;
        }
    }

    /// <summary>
    /// Pure C# Model that implements the Instant Recalculation pattern.
    /// Evaluates the combat outcomes for timeline slots from 0 to upToSlot.
    /// </summary>
    public class TimelineModel
    {
        public const int TIMELINE_SLOTS = 5;

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

            // 3. Chronological slot evaluation loop
            for (int slot = 0; slot <= upToSlot; slot++)
            {
                simulationLog.Add($"[Slot {slot}] Processing actions...");

                // Tick Status Effects at start of slot for active characters
                foreach (var character in simulatedCharacters.Values)
                {
                    character.TickStatusEffects(simulationLog);
                }

                // Process Phase: Delay
                ProcessPhase(slot, EffectPhase.Delay, simulationLog);

                // Check for conflicts: multiple exclusive actions on the same character at the same slot
                CheckSlotConflicts(slot, simulationLog);

                // Process Phase: Defense
                ProcessPhase(slot, EffectPhase.Defense, simulationLog);

                // Process Phase: Attack
                ProcessPhase(slot, EffectPhase.Attack, simulationLog);

                // Process Phase: Utility
                ProcessPhase(slot, EffectPhase.Utility, simulationLog);

                // Mark all actions executed in this slot as processed
                foreach (var node in activeSimNodes)
                {
                    if (node.effectiveSlot == slot)
                    {
                        node.processed = true;
                    }
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
