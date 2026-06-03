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

    /// <summary>
    /// Model representation of a status effect active on a character.
    /// </summary>
    [Serializable]
    public class StatusEffectInstance
    {
        public StatusEffectType effectType;
        public StatusEffectSO statusSO;
        public int duration;
        public int intensity;

        // For backward compatibility and UI display
        public string statusId;

        public StatusEffectInstance(StatusEffectType effectType, int duration, int intensity, StatusEffectSO statusSO = null)
        {
            this.effectType = effectType;
            this.duration = duration;
            this.intensity = intensity;
            this.statusSO = statusSO;
            this.statusId = (statusSO != null) ? statusSO.displayName : effectType.ToString();
        }

        public StatusEffectInstance Clone()
        {
            return new StatusEffectInstance(effectType, duration, intensity, statusSO);
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
        public void TakeDamage(int amount, bool isDirectHit = true)
        {
            if (amount <= 0) return;

            // Vulnerable status check (applies +50% damage multiplier to direct hits)
            if (isDirectHit && statusEffects != null)
            {
                var vul = statusEffects.Find(s => s.effectType == StatusEffectType.Vulnerable);
                if (vul != null && vul.duration > 0)
                {
                    amount = (int)Math.Round(amount * 1.5);
                }
            }

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
        /// Adds a status effect using a scriptable object asset configuration.
        /// </summary>
        public void ApplyStatus(StatusEffectSO statusSO, int duration, int intensity)
        {
            if (statusSO == null) return;
            ApplyStatus(statusSO.effectType, duration, intensity, statusSO);
        }

        /// <summary>
        /// Adds a status effect (e.g. Poison, Weak, Vulnerable).
        /// </summary>
        public void ApplyStatus(StatusEffectType effectType, int duration, int intensity, StatusEffectSO statusSO = null)
        {
            if (effectType == StatusEffectType.None) return;
            if (statusEffects == null) statusEffects = new List<StatusEffectInstance>();

            var existing = statusEffects.Find(s => s.effectType == effectType);
            if (existing != null)
            {
                existing.duration = Math.Max(existing.duration, duration);
                existing.intensity = Math.Max(existing.intensity, intensity);
                if (statusSO != null) existing.statusSO = statusSO;
            }
            else
            {
                statusEffects.Add(new StatusEffectInstance(effectType, duration, intensity, statusSO));
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

                if (status.effectType == StatusEffectType.Poison)
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity, isDirectHit: false);
                    logs.Add($"     * [Poison Tick] {name} takes {status.intensity} poison dmg. (HP: {oldHp}->{currentHp})");
                }
                else if (status.effectType == StatusEffectType.Burn)
                {
                    int oldHp = currentHp;
                    TakeDamage(status.intensity, isDirectHit: false);
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
                string dispName = exp.statusSO != null ? exp.statusSO.displayName : exp.effectType.ToString();
                logs.Add($"     * Status '{dispName}' on {name} has expired.");
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

        [Header("Combo Card Settings")]
        public CardType cardType = CardType.Normal;
        public List<CombatEffect> comboEffects = new List<CombatEffect>();
        public float comboValueMultiplier = 1f;
        public int comboValueBonus = 0;

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
                value = this.value,
                cardType = this.cardType,
                comboValueMultiplier = this.comboValueMultiplier,
                comboValueBonus = this.comboValueBonus
            };
            if (this.effects != null)
            {
                cloned.effects = new List<CombatEffect>(this.effects);
            }
            if (this.comboEffects != null)
            {
                cloned.comboEffects = new List<CombatEffect>(this.comboEffects);
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

                // Process Phase: Delay
                ProcessPhase(slot, EffectPhase.Delay, simulationLog);

                // Check for conflicts: multiple exclusive actions on the same character at the same slot
                CheckSlotConflicts(slot, simulationLog);

                // Process Phase: Defense
                ProcessPhase(slot, EffectPhase.Defense, simulationLog);

                // Resolve Attack Clash (offsetting player and enemy damage values in the same slot)
                ResolveAttackClashes(slot, simulationLog);

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
