using System;
using System.Collections.Generic;

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
            return new CharacterData(id, name, maxHp, currentHp, shield);
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
        public ActionType actionType;
        public int value; // Damage amount, Shield amount, Delay slots

        public ActionNodeData() { }

        public ActionNodeData(string id, CharacterID sourceId, CharacterID targetId, int startSlot, ActionType actionType, int value, bool isExclusive = true)
        {
            this.id = id;
            this.sourceId = sourceId;
            this.targetId = targetId;
            this.startSlot = startSlot;
            this.effectiveSlot = startSlot; // Default to start slot
            this.isExclusive = isExclusive;
            this.actionType = actionType;
            this.value = value;
        }

        /// <summary>
        /// Deep copies the action node configuration.
        /// </summary>
        public ActionNodeData Clone()
        {
            return new ActionNodeData(id, sourceId, targetId, startSlot, actionType, value, isExclusive)
            {
                effectiveSlot = this.effectiveSlot
            };
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

            // 3. Chronological slot evaluation loop
            for (int slot = 0; slot <= upToSlot; slot++)
            {
                simulationLog.Add($"[Slot {slot}] Processing actions...");

                // A. Delay Phase (modify effective slots of target's future actions)
                List<SimulatedNode> delays = simNodes.FindAll(n => 
                    !n.processed && n.effectiveSlot == slot && n.node.actionType == ActionType.Delay
                );

                foreach (var delay in delays)
                {
                    delay.processed = true;
                    string src = GetName(delay.node.sourceId);
                    string tgt = GetName(delay.node.targetId);
                    simulationLog.Add($"  -> [DELAY] {src} delays {tgt} by {delay.node.value} slots.");

                    foreach (var other in simNodes)
                    {
                        if (!other.processed && 
                            other.node.sourceId == delay.node.targetId && 
                            other.effectiveSlot >= slot)
                        {
                            int oldSlot = other.effectiveSlot;
                            other.effectiveSlot = Math.Min(TIMELINE_SLOTS, other.effectiveSlot + delay.node.value);
                            other.node.effectiveSlot = other.effectiveSlot;
                            simulationLog.Add($"     * Action '{other.node.actionType}' shifted: Slot {oldSlot} -> Slot {other.effectiveSlot}");
                        }
                    }
                }

                // Check for conflicts: multiple exclusive actions on the same character at the same slot (Option A: Log only)
                List<SimulatedNode> slotActions = simNodes.FindAll(n => !n.processed && n.effectiveSlot == slot);
                HashSet<CharacterID> exclusiveUsers = new HashSet<CharacterID>();
                HashSet<CharacterID> flaggedUsers = new HashSet<CharacterID>();
                foreach (var node in slotActions)
                {
                    if (node.node.isExclusive)
                    {
                        if (!exclusiveUsers.Add(node.node.sourceId))
                        {
                            if (flaggedUsers.Add(node.node.sourceId))
                            {
                                string sourceName = GetName(node.node.sourceId);
                                simulationLog.Add($"[Conflict Slot {slot}] {sourceName} cannot execute multiple exclusive actions!");
                            }
                        }
                    }
                }

                // B. Defensive Phase (Apply shields)
                List<SimulatedNode> defends = simNodes.FindAll(n => 
                    !n.processed && n.effectiveSlot == slot && n.node.actionType == ActionType.Defend
                );

                foreach (var def in defends)
                {
                    def.processed = true;
                    if (simulatedCharacters.TryGetValue(def.node.sourceId, out var src) &&
                        simulatedCharacters.TryGetValue(def.node.targetId, out var tgt))
                    {
                        tgt.AddShield(def.node.value);
                        simulationLog.Add($"  -> [DEFEND] {src.name} adds {def.node.value} shield to {tgt.name} (Shield: {tgt.shield})");
                    }
                }

                // C. Attack Phase (Apply damage)
                List<SimulatedNode> attacks = simNodes.FindAll(n => 
                    !n.processed && n.effectiveSlot == slot && n.node.actionType == ActionType.Attack
                );

                foreach (var atk in attacks)
                {
                    atk.processed = true;
                    if (simulatedCharacters.TryGetValue(atk.node.sourceId, out var src) &&
                        simulatedCharacters.TryGetValue(atk.node.targetId, out var tgt))
                    {
                        int oldHp = tgt.currentHp;
                        int oldShield = tgt.shield;
                        tgt.TakeDamage(atk.node.value);
                        simulationLog.Add($"  -> [ATTACK] {src.name} attacks {tgt.name} for {atk.node.value} dmg. (Shield: {oldShield}->{tgt.shield}, HP: {oldHp}->{tgt.currentHp})");
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
