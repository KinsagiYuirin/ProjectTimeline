using System;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Pure C# Presenter acting as the broker between the Card Collection and the Timeline Model.
    /// Manages AP/Energy resources and handles the mechanics of placing cards onto the timeline.
    /// </summary>
    public class CardTimelinePresenter
    {
        public CardCollectionModel Collection { get; private set; }

        public int MaxEnergy { get; private set; }
        public int CurrentEnergy { get; private set; }

        // Events triggered to notify visual views
        public event Action<List<RuntimeCardInstance>> OnHandChanged;
        public event Action<int, int> OnEnergyChanged; // Current, Max

        public CardTimelinePresenter() { }

        public CardTimelinePresenter(CardCollectionModel collection, int maxEnergy)
        {
            Configure(collection, maxEnergy);
        }

        /// <summary>
        /// Configures the presenter with its dependencies.
        /// </summary>
        public void Configure(CardCollectionModel collection, int maxEnergy)
        {
            this.Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            this.MaxEnergy = maxEnergy;
            this.CurrentEnergy = maxEnergy;
        }

        /// <summary>
        /// Resets the current energy back to its maximum value (usually called at turn start).
        /// </summary>
        public void ResetEnergy()
        {
            CurrentEnergy = MaxEnergy;
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        }

        /// <summary>
        /// Commands the collection model to draw cards and triggers view refresh events.
        /// </summary>
        public void DrawCards(int amount)
        {
            if (Collection == null) return;

            Collection.DrawCards(amount);
            OnHandChanged?.Invoke(new List<RuntimeCardInstance>(Collection.Hand));
        }

        /// <summary>
        /// Discards the entire hand and notifies the views.
        /// </summary>
        public void DiscardHand()
        {
            if (Collection == null) return;

            Collection.DiscardHand();
            OnHandChanged?.Invoke(new List<RuntimeCardInstance>(Collection.Hand));
        }

        /// <summary>
        /// Validates play conditions and executes the card placement onto the timeline model.
        /// </summary>
        /// <param name="card">The card instance to play.</param>
        /// <param name="targetSlot">The slot index (0 to 4).</param>
        /// <param name="timeline">The active simulation model.</param>
        /// <returns>True if the card was successfully played; otherwise, false.</returns>
        public bool TryPlayCardToTimeline(RuntimeCardInstance card, int targetSlot, TimelineModel timeline)
        {
            if (card == null || timeline == null) return false;

            // 1. Validate that the card exists in the player's hand
            bool cardInHand = false;
            foreach (var handCard in Collection.Hand)
            {
                if (handCard.instanceId == card.instanceId)
                {
                    cardInHand = true;
                    break;
                }
            }

            if (!cardInHand)
            {
                UnityEngine.Debug.LogWarning($"[Presenter] Cannot play '{card.blueprint.cardName}'; it is not in the player's Hand.");
                return false;
            }

            // 2. Validate energy cost
            if (CurrentEnergy < card.blueprint.energyCost)
            {
                UnityEngine.Debug.LogWarning($"[Presenter] Not enough Energy to play '{card.blueprint.cardName}' (Cost: {card.blueprint.energyCost}, Available: {CurrentEnergy}).");
                return false;
            }

            // 3. Validate target slot boundary
            if (targetSlot < 0 || targetSlot >= TimelineModel.TIMELINE_SLOTS)
            {
                UnityEngine.Debug.LogWarning($"[Presenter] Invalid target slot {targetSlot}. Must be 0 to {TimelineModel.TIMELINE_SLOTS - 1}.");
                return false;
            }

            // 4. Spend energy resource
            CurrentEnergy -= card.blueprint.energyCost;
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);

            // 5. Convert card blueprint payload to real action node and inject into the model
            ActionNodeData timelineAction = card.blueprint.actionBlueprint.Clone();
            timelineAction.id = card.instanceId; // Bind action node to card instance unique ID
            timelineAction.startSlot = targetSlot;
            timelineAction.effectiveSlot = targetSlot;
            timelineAction.sourceId = "player"; // Player card action

            timeline.playerActions.Add(timelineAction);

            // 6. Move card from hand to discard pile
            Collection.MoveToDiscard(card);
            OnHandChanged?.Invoke(new List<RuntimeCardInstance>(Collection.Hand));

            // 7. Force instant recalculation of timeline preview up to the end
            timeline.SimulateUpTo(TimelineModel.TIMELINE_SLOTS - 1);

            UnityEngine.Debug.Log($"[Presenter] Successfully played '{card.blueprint.cardName}' at Slot {targetSlot}. Energy remaining: {CurrentEnergy}.");
            return true;
        }
    }
}
