using System;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Pure C# ViewModel acting as the state manager and bridge.
    /// Exposes bindable events to push simulation changes to the View or Inspector.
    /// Handles data-transformation, cloning, and model execution trigger.
    /// </summary>
    public class TimelineViewModel
    {
        // Reference to the pure C# Model layer
        public TimelineModel Model { get; }

        // Event fired whenever simulation parameters change, pushing simulated character states and logs to the View
        public event Action<Dictionary<CharacterID, CharacterData>, List<string>> OnTimelineUpdated;

        public TimelineViewModel()
        {
            Model = new TimelineModel();
        }

        public TimelineViewModel(TimelineModel model)
        {
            Model = model;
        }

        /// <summary>
        /// Commands the model to run simulation up to the designated slot,
        /// then notifies subscribers (Views) of the updated state.
        /// </summary>
        /// <param name="slot">The playhead target slot (0 to 4).</param>
        public void ScrubToSlot(int slot)
        {
            if (Model == null) return;

            // Trigger instant recalculation in the model layer
            Model.SimulateUpTo(slot);

            // Notify observer layers (Views, Inspector bridges, etc.)
            OnTimelineUpdated?.Invoke(Model.simulatedCharacters, Model.simulationLog);
        }

        /// <summary>
        /// ดึงรายการ Action ที่ผ่านการคำนวณจำลองแล้วใน Slot และฝั่งที่กำหนด (Strict MVVM Data Exposure)
        /// </summary>
        public List<ActionNodeData> GetSimulatedActionsForSlot(int slotIndex, CharacterID sourceId, bool exactMatch = true)
        {
            if (Model == null || Model.simulatedActions == null) return new List<ActionNodeData>();

            // ใช้ลอจิกคัดกรองข้อมูลจาก Model แต่ออกรับหน้าแทน View[cite: 6]
            return Model.simulatedActions.FindAll(action => 
                action.effectiveSlot == slotIndex && 
                (exactMatch ? action.sourceId == sourceId : action.sourceId != sourceId)
            );
        }
        
        /// <summary>
        /// Accepts raw inputs from the Inspector/UI, clones/processes them, distributes them
        /// into Player and Enemy actions, executes the simulation, and notifies observers.
        /// </summary>
        public void ProcessAndSimulate(List<CharacterData> baselineInputs, List<TimelineActionSetup> actionSetups, int slot)
        {
            if (Model == null) return;

            // 1. Sync and clone baseline characters to avoid modifying Inspector assets directly
            Model.baselineCharacters.Clear();
            if (baselineInputs != null)
            {
                foreach (var character in baselineInputs)
                {
                    if (character != null && !string.IsNullOrEmpty(character.id))
                    {
                        if (System.Enum.TryParse<CharacterID>(character.id, true, out CharacterID characterEnum))
                        {
                            Model.baselineCharacters[characterEnum] = character.Clone();
                        }
                    }
                }
            }

            // 2. Clone and distribute actions to Player vs Enemy pools
            Model.enemyActions.Clear();
            Model.playerActions.Clear();
            if (actionSetups != null)
            {
                for (int i = 0; i < actionSetups.Count; i++)
                {
                    var setup = actionSetups[i];
                    if (setup == null || setup.cardBlueprint == null) continue;

                    // Deep copy/clone the action node payload
                    ActionNodeData clonedNode = setup.cardBlueprint.actionBlueprint.Clone();

                    // Ensure a valid ID exists
                    clonedNode.id = string.IsNullOrEmpty(clonedNode.id) ? setup.cardBlueprint.cardId : clonedNode.id;

                    // Inject runtime timeline slot parameters
                    clonedNode.startSlot = setup.startSlot;
                    clonedNode.effectiveSlot = setup.startSlot;
                    clonedNode.cardType = setup.cardBlueprint.cardType;
                    clonedNode.cardSpeed = setup.cardBlueprint.cardSpeed;

                    // Generate a unique instance ID if none exists yet
                    if (string.IsNullOrEmpty(setup.runtimeInstanceId))
                    {
                        setup.runtimeInstanceId = clonedNode.id + "_" + i + "_" + System.Guid.NewGuid().ToString().Substring(0, 8);
                    }
                    clonedNode.id = setup.runtimeInstanceId;

                    // Sort and distribute based on sourceId
                    if (clonedNode.sourceId == CharacterID.Player)
                    {
                        Model.playerActions.Add(clonedNode);
                    }
                    else
                    {
                        Model.enemyActions.Add(clonedNode);
                    }
                }
            }

            // 3. Command the Model to recalculate up to the target slot
            Model.SimulateUpTo(slot);

            // 4. Notify UI components
            OnTimelineUpdated?.Invoke(Model.simulatedCharacters, Model.simulationLog);
        }
    }
}
