using System;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Pure C# ViewModel acting as the state manager and bridge.
    /// Exposes bindable events to push simulation changes to the View or Inspector.
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
    }
}
