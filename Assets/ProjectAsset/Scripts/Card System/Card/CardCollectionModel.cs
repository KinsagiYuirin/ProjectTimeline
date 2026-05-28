using System;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// Pure C# class managing Player card runtime state piles (Deck, Hand, Discard).
    /// </summary>
    public class CardCollectionModel
    {
        private List<RuntimeCardInstance> deck = new List<RuntimeCardInstance>();
        private List<RuntimeCardInstance> hand = new List<RuntimeCardInstance>();
        private List<RuntimeCardInstance> discardPile = new List<RuntimeCardInstance>();

        // Public read-only accessors
        public IReadOnlyList<RuntimeCardInstance> Deck => deck;
        public IReadOnlyList<RuntimeCardInstance> Hand => hand;
        public IReadOnlyList<RuntimeCardInstance> DiscardPile => discardPile;

        private readonly Random random = new Random();

        /// <summary>
        /// Populates the deck from a starting pool of card blueprints and shuffles it.
        /// </summary>
        public void InitializeDeck(List<CardDataBlueprint> startingPool)
        {
            deck.Clear();
            hand.Clear();
            discardPile.Clear();

            if (startingPool == null) return;

            foreach (var blueprint in startingPool)
            {
                deck.Add(new RuntimeCardInstance(blueprint));
            }

            ShuffleDeck();
        }

        /// <summary>
        /// Shuffles the deck list using the Fisher-Yates algorithm.
        /// </summary>
        public void ShuffleDeck()
        {
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                RuntimeCardInstance value = deck[k];
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        /// <summary>
        /// Draws a specified number of cards into the hand, recycling the discard pile if needed.
        /// </summary>
        public void DrawCards(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                if (deck.Count == 0)
                {
                    RecycleDiscardPile();
                }

                if (deck.Count > 0)
                {
                    // Draw from the top (end of list for efficiency)
                    int topIndex = deck.Count - 1;
                    RuntimeCardInstance drawn = deck[topIndex];
                    deck.RemoveAt(topIndex);
                    hand.Add(drawn);
                }
                else
                {
                    // No cards left in deck or discard pile
                    break;
                }
            }
        }

        /// <summary>
        /// Moves a played card from the hand to the discard pile.
        /// </summary>
        public void MoveToDiscard(RuntimeCardInstance card)
        {
            if (card == null) return;

            if (hand.Remove(card))
            {
                discardPile.Add(card);
            }
            else if (deck.Remove(card))
            {
                discardPile.Add(card);
            }
        }

        /// <summary>
        /// Discards all cards currently in hand.
        /// </summary>
        public void DiscardHand()
        {
            if (hand.Count == 0) return;

            discardPile.AddRange(hand);
            hand.Clear();
        }

        /// <summary>
        /// Recycles the discard pile back into the deck and shuffles it.
        /// </summary>
        private void RecycleDiscardPile()
        {
            if (discardPile.Count == 0) return;

            deck.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }
    }
}
