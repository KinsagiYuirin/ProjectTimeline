using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// View component (MVP) managing the visual player Hand interface.
    /// Spawns card prefab instances, displays active energy levels, 
    /// and delegates card play interactions to the CardTimelinePresenter broker.
    /// </summary>
    public class GameplayCardViewManager : MonoBehaviour
    {
        [Header("Deck Configuration")]
        [Tooltip("The starting list of card blueprints in the player's deck.")]
        public List<CardDataBlueprint> startingDeck = new List<CardDataBlueprint>();

        [Tooltip("Max energy/AP available to the player at turn start.")]
        public int maxEnergy = 3;

        [Tooltip("Number of cards to draw at the start of the turn.")]
        public int startingDrawCount = 3;

        [Header("UI Component Bindings")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform handPanel;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TimelineInspectorBridge timelineBridge;

        // Core MVP references
        private CardCollectionModel collection;
        private CardTimelinePresenter presenter;

        public CardCollectionModel Collection => collection;
        public CardTimelinePresenter Presenter => presenter;

        private void Start()
        {
            InitializeMVP();
        }

        /// <summary>
        /// Instantiates state models, configures the presenter, and sets up event bindings.
        /// </summary>
        public void InitializeMVP()
        {
            collection = new CardCollectionModel();
            presenter = new CardTimelinePresenter(collection, maxEnergy);

            // Subscribe view updates to the presenter events
            presenter.OnHandChanged += OnHandChanged;
            presenter.OnEnergyChanged += OnEnergyChanged;

            // Initialize the deck from the config list
            collection.InitializeDeck(startingDeck);
            presenter.ResetEnergy();

            // Perform initial draw
            presenter.DrawCards(startingDrawCount);
        }

        private void OnDestroy()
        {
            if (presenter != null)
            {
                presenter.OnHandChanged -= OnHandChanged;
                presenter.OnEnergyChanged -= OnEnergyChanged;
            }
        }

        /// <summary>
        /// Event listener to rebuild hand visual prefabs when the hand collection changes.
        /// </summary>
        private void OnHandChanged(List<RuntimeCardInstance> currentHand)
        {
            if (handPanel != null)
            {
                // Clear existing visual card objects in the hand
                foreach (Transform child in handPanel)
                {
                    Destroy(child.gameObject);
                }

                // Spawn a new visual prefab for each card instance in hand
                foreach (var cardInstance in currentHand)
                {
                    if (cardPrefab != null)
                    {
                        GameObject cardObj = Instantiate(cardPrefab, handPanel);
                        GameplayCardItem cardItem = cardObj.GetComponent<GameplayCardItem>();
                        if (cardItem != null)
                        {
                            cardItem.Setup(cardInstance);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Event listener to update the energy/AP cost display.
        /// </summary>
        private void OnEnergyChanged(int current, int max)
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {current} / {max}";
            }
        }

        /// <summary>
        /// Public command to request playing a card from hand into a specific timeline slot.
        /// </summary>
        /// <param name="cardItem">The visual card item requesting play.</param>
        /// <param name="slot">The target slot index (0 to 4).</param>
        /// <returns>True if the play action is valid and executed; otherwise, false.</returns>
        public bool PlayCardToSlot(GameplayCardItem cardItem, int slot)
        {
            if (cardItem == null || timelineBridge == null) return false;

            // Access the exposed public Model property on the timeline bridge
            TimelineModel model = timelineBridge.Model;

            // Delegate play and validation logic to the presenter
            bool success = presenter.TryPlayCardToTimeline(cardItem.CardInstance, slot, model);
            if (success)
            {
                cardItem.IsPlayed = true;

                // Synchronize the played card's action back to the timeline bridge's list 
                // so that it persists and remains editable directly in the Unity Inspector
                var playedAction = model.playerActions.Find(a => a.id == cardItem.CardInstance.instanceId);
                if (playedAction != null)
                {
                    timelineBridge.initialTimelineActions.Add(new TimelineActionSetup
                    {
                        cardBlueprint = cardItem.CardInstance.blueprint,
                        startSlot = slot,
                        runtimeInstanceId = playedAction.id
                    });
                }

                // Refresh the bridge's simulation, running instant recalculation
                timelineBridge.SyncAndRunSimulation();

                // Destroy the visual card element as it is now played
                Destroy(cardItem.gameObject);
            }

            return success;
        }
    }
}
