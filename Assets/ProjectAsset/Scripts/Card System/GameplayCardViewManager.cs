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

        private void OnHandChanged(List<RuntimeCardInstance> currentHand)
        {
            if (handPanel == null) return;

            // 1. สแกนและจับคู่การ์ด UI ปัจจุบันบนหน้าจอ เก็บลง Dictionary โดยใช้ instanceId เป็นคีย์
            Dictionary<string, GameplayCardItem> existingUI = new Dictionary<string, GameplayCardItem>();
            foreach (Transform child in handPanel)
            {
                if (child != null)
                {
                    GameplayCardItem cardItem = child.GetComponent<GameplayCardItem>();
                    if (cardItem != null && cardItem.CardInstance != null)
                    {
                        existingUI[cardItem.CardInstance.instanceId] = cardItem;
                    }
                }
            }

            // สร้าง HashSet ของรหัสการ์ดในข้อมูลปัจจุบันเพื่อความรวดเร็วในการค้นหา
            HashSet<string> currentHandIds = new HashSet<string>();
            foreach (var card in currentHand)
            {
                currentHandIds.Add(card.instanceId);
            }

            // 2. ตรวจสอบและทำลายเฉพาะการ์ด UI ที่ถูกเล่นไปแล้ว (ไม่อยู่ในคอลเลกชัน currentHand)
            List<string> idsToRemove = new List<string>();
            foreach (var kvp in existingUI)
            {
                if (!currentHandIds.Contains(kvp.Key))
                {
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (var id in idsToRemove)
            {
                GameplayCardItem itemToDestroy = existingUI[id];
                itemToDestroy.transform.SetParent(null); // ปลดแม่เพื่อป้องกันปัญหานับจำนวนลูกพลาดในเฟรมนี้
                Destroy(itemToDestroy.gameObject);
                existingUI.Remove(id); // ลบออกจาก Dictionary คลังชั่วคราว
            }

            // 3. ลูปจัดเรียงการ์ดที่เหลือ และสร้างใหม่เฉพาะการ์ดที่ยังไม่มีตัวตน
            for (int i = 0; i < currentHand.Count; i++)
            {
                var cardInstance = currentHand[i];

                // ถ้าการ์ดใบนี้มีตัวตนอยู่บน UI อยู่แล้ว
                if (existingUI.TryGetValue(cardInstance.instanceId, out GameplayCardItem existingCard))
                {
                    // ไม่ต้องทำลาย ไม่ต้องสร้างใหม่! แค่ปรับลำดับ Index ให้ตรงตามแถวปัจจุบันพอค่ะ
                    existingCard.transform.SetSiblingIndex(i);
                }
                else
                {
                    // ถ้าเป็นการ์ดใบใหม่เอี่ยม (เพิ่งจั่วได้ หรือเพิ่งดึง Recall กลับมา) ค่อยสร้างพรีแฟบขึ้นมาค่ะ
                    if (cardPrefab != null)
                    {
                        GameObject cardObj = Instantiate(cardPrefab, handPanel);
                        GameplayCardItem newCardItem = cardObj.GetComponent<GameplayCardItem>();
                        if (newCardItem != null)
                        {
                            newCardItem.Setup(cardInstance);
                            newCardItem.transform.SetSiblingIndex(i); // จัดคิวให้อยู่ในตำแหน่งที่ถูกต้อง
                        }
                    }
                }
            }

            // 4. สั่งสั่งการระบบ Custom Layout สไลด์การ์ดขยับเข้าที่อย่างนุ่มนวล
            var layoutManager = handPanel.GetComponent<HandCardLayoutManager>();
            if (layoutManager != null)
            {
                layoutManager.UpdateLayout();
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

        /// <summary>
        /// Recalls a played card from the timeline back into the player's hand during the Planning phase.
        /// Delegates state mutation to the presenter, removes the corresponding inspector action entry,
        /// and commands the bridge to redraw all slot visuals.
        /// </summary>
        /// <param name="cardInstanceId">The unique runtime instance ID of the card icon that was clicked.</param>
        public void RecallCardFromSlot(string cardInstanceId)
        {
            if (string.IsNullOrEmpty(cardInstanceId))
            {
                Debug.LogWarning("[GameplayCardViewManager] RecallCardFromSlot called with a null/empty instance ID.");
                return;
            }

            if (presenter == null || timelineBridge == null)
            {
                Debug.LogWarning("[GameplayCardViewManager] RecallCardFromSlot aborted – presenter or timelineBridge is null.");
                return;
            }

            // 1. Delegate all card-state changes to the presenter layer
            bool success = presenter.TryRecallCardFromTimeline(cardInstanceId, timelineBridge.Model);

            if (!success)
            {
                Debug.LogWarning($"[GameplayCardViewManager] Recall failed for card id '{cardInstanceId}'. Card not found in discard pile.");
                return;
            }

            // 2. Remove the matching entry from the inspector action list
            //    so the slot doesn't re-appear after the next SyncAndRunSimulation call
            timelineBridge.initialTimelineActions.RemoveAll(a => a.runtimeInstanceId == cardInstanceId);

            // 3. Trigger a full bridge refresh to redraw slot visuals, health bars, and logs
            timelineBridge.SyncAndRunSimulation();

            Debug.Log($"[GameplayCardViewManager] Card '{cardInstanceId}' successfully recalled to hand.");
        }
    }
}
