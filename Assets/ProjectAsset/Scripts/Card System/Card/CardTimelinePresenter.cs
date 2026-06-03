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

            // 3.5 Slot Occupancy and Exclusivity Validation
            bool playingIsExclusive = card.blueprint.actionBlueprint.isExclusive;

            if (playingIsExclusive)
            {
                // Main Action validation: Cannot occupy slot if there's already an exclusive action
                foreach (var existingAction in timeline.playerActions)
                {
                    if (existingAction.startSlot == targetSlot && existingAction.isExclusive)
                    {
                        UnityEngine.Debug.LogWarning($"[Presenter] Cannot play card; Slot {targetSlot} is already occupied.");
                        return false;
                    }
                }
            }
            else
            {
                // Free Action validation: Count existing free actions in this slot
                int existingFreeActionsCount = 0;
                foreach (var existingAction in timeline.playerActions)
                {
                    if (existingAction.startSlot == targetSlot && !existingAction.isExclusive)
                    {
                        existingFreeActionsCount++;
                    }
                }

                if (existingFreeActionsCount >= 1)
                {
                    UnityEngine.Debug.LogWarning($"[Presenter] Slot {targetSlot} already contains a Free Action. Maximum 1 Free Action allowed per slot.");
                    return false;
                }
            }

            // 4. Spend energy resource
            CurrentEnergy -= card.blueprint.energyCost;
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);

            // 5. Convert card blueprint payload to real action node and inject into the model
            ActionNodeData timelineAction = card.blueprint.actionBlueprint.Clone();
            timelineAction.id = card.instanceId; // Bind action node to card instance unique ID
            timelineAction.startSlot = targetSlot;
            timelineAction.effectiveSlot = targetSlot;
            timelineAction.isExclusive = playingIsExclusive; // Retain blueprint's exclusivity status
            timelineAction.sourceId = CharacterID.Player; // Player card action
            timelineAction.cardType = card.blueprint.cardType; // Keep cardType synchronized!

            timeline.playerActions.Add(timelineAction);

            // 6. Move card from hand to discard pile
            Collection.MoveToDiscard(card);
            OnHandChanged?.Invoke(new List<RuntimeCardInstance>(Collection.Hand));

            // 7. Force instant recalculation of timeline preview up to the end
            timeline.SimulateUpTo(TimelineModel.TIMELINE_SLOTS - 1);

            UnityEngine.Debug.Log($"[Presenter] Successfully played '{card.blueprint.cardName}' at Slot {targetSlot}. Energy remaining: {CurrentEnergy}.");
            return true;
        }

        /// <summary>
        /// Recalls a previously played card back from the timeline into the player's hand.
        /// Refunds its energy cost, removes its action node from the model, and
        /// fires the appropriate view-update events.
        /// </summary>
        /// <param name="cardInstanceId">The unique runtime instance ID of the card to recall.</param>
        /// <param name="timeline">The active simulation model to remove the action node from.</param>
        /// <returns>True if the card was found and successfully recalled; otherwise false.</returns>
        public bool TryRecallCardFromTimeline(string cardInstanceId, TimelineModel timeline)
        {
            if (string.IsNullOrEmpty(cardInstanceId) || timeline == null || Collection == null)
                return false;

            // 1. Locate the card in the discard pile (played cards always live here)
            RuntimeCardInstance card = null;
            foreach (var c in Collection.DiscardPile)
            {
                if (c.instanceId == cardInstanceId)
                {
                    card = c;
                    break;
                }
            }

            if (card == null)
            {
                UnityEngine.Debug.LogWarning($"[Presenter] TryRecallCardFromTimeline: card '{cardInstanceId}' not found in DiscardPile.");
                return false;
            }

            // 2. Refund energy – clamp to MaxEnergy so we never exceed the cap
            int refund = card.blueprint.energyCost;
            CurrentEnergy = UnityEngine.Mathf.Min(CurrentEnergy + refund, MaxEnergy);
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);

            // 3. Move card from discard pile back into the hand
            Collection.MoveToHand(card);
            OnHandChanged?.Invoke(new List<RuntimeCardInstance>(Collection.Hand));

            // 4. Remove the corresponding action node from the timeline
            timeline.playerActions.RemoveAll(a => a.id == cardInstanceId);

            // 5. Recalculate the simulation preview so all visuals stay consistent
            timeline.SimulateUpTo(TimelineModel.TIMELINE_SLOTS - 1);

            UnityEngine.Debug.Log($"[Presenter] Recalled '{card.blueprint.cardName}' (id: {cardInstanceId}). Energy refunded: +{refund} → {CurrentEnergy}/{MaxEnergy}.");
            return true;
        }
    }
}
