using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening; // เรียกใช้งานขุมพลัง DOTween

namespace ProjectTimeline.Timeline
{
    public class GameplayCardItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Visual Bindings")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text speedText;

        [Header("Hover Feedback Settings")]
        [SerializeField] private float hoverScaleFactor = 1.15f;
        [SerializeField] private float hoverYOffset = 40f; // ระยะที่การ์ดจะยกลอยขึ้นเมื่อโดนชี้
        [SerializeField] private float tweenDuration = 0.15f;

        public RuntimeCardInstance CardInstance { get; private set; }
        public bool IsPlayed { get; set; }
        public bool IsDragging { get; private set; } // เปิดตัวแปรไว้รองรับตัว Layout Manager

        private Transform originalParent;
        private int originalSiblingIndex;
        private Vector3 originalScale;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Canvas mainCanvas;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            originalScale = transform.localScale;
            mainCanvas = GetComponentInParent<Canvas>();
        }

        // 🔥 ลบฟังก์ชัน Update() ตัวเก่าทิ้งไปเลยค่ะ! เราจะไม่รันลูป Lerp ทุกเฟรมให้เปลืองพลังงานเครื่องอีกแล้ว

        public void Setup(RuntimeCardInstance instance)
        {
            this.CardInstance = instance;
            if (instance == null) return;

            if (nameText != null) nameText.text = instance.blueprint.cardName;
            if (costText != null) costText.text = instance.blueprint.energyCost.ToString();
            if (descriptionText != null) descriptionText.text = instance.blueprint.cardDescription;
            if (iconImage != null && instance.blueprint.icon != null) iconImage.sprite = instance.blueprint.icon;
            if (speedText != null) speedText.text = instance.blueprint.cardSpeed.ToString();
        }

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            IsDragging = true;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            if (mainCanvas != null)
            {
                transform.SetParent(mainCanvas.transform, true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0.75f;
            }

            // ตอนดึงการ์ดลาก ให้ขยายขนาดค้างไว้แบบสมูท
            transform.DOKill();
            transform.DOScale(originalScale * hoverScaleFactor, tweenDuration);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform != null && mainCanvas != null)
            {
                RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
                if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    transform.position = eventData.position;
                }
                else
                {
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out var worldPos))
                    {
                        transform.position = worldPos;
                    }
                }
            }
            else
            {
                transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            IsDragging = false;
            if (IsPlayed) return;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            ForceResetVisuals(); // หดขนาดกลับสู่ความจริง

            if ((mainCanvas != null && transform.parent == mainCanvas.transform) || transform.parent != originalParent)
            {
                ReturnToHand();
            }
        }

        public void ReturnToHand()
        {
            if (originalParent != null)
            {
                transform.SetParent(originalParent, false);
                transform.SetSiblingIndex(originalSiblingIndex);

                var layoutManager = originalParent.GetComponent<HandCardLayoutManager>();
                if (layoutManager != null)
                {
                    layoutManager.UpdateLayout();
                }
            }
        }

        #endregion

        #region Hover Handlers (เวอร์ชันแก้ไขบั๊กหลอดเหลนแบบถาวร)

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsPlayed || IsDragging) return;

            // 🔥 ไม้ตาย: สั่งเด้งเคลียร์การ์ดใบอื่นๆ บนมือให้หดกลับปกติทันที ป้องกันปัญหารูดเมาส์ไวแล้วค้างคู่
            ResetOtherCardsInHand();

            // รันแอนิเมชันยกลอยขึ้นแกน Y + ขยายขนาดแบบสปริงนุ่มๆ ด้วย DOTween
            transform.DOKill();
            transform.DOScale(originalScale * hoverScaleFactor, tweenDuration).SetEase(Ease.OutQuad);
            
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(hoverYOffset, tweenDuration).SetEase(Ease.OutQuad);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsPlayed || IsDragging) return;

            // หดกลับลงพิกัดปกติอย่างนุ่มนวล
            ForceResetVisuals();
        }

        /// <summary>
        /// บังคับให้การ์ดใบนี้เคลียร์แอนิเมชันค้างทั้งหมดและหดกลับสู่สภาพปกติ
        /// </summary>
        public void ForceResetVisuals()
        {
            transform.DOKill();
            transform.DOScale(originalScale, tweenDuration).SetEase(Ease.OutQuad);

            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOAnchorPosY(0f, tweenDuration).SetEase(Ease.OutQuad);
            }
        }

        private void ResetOtherCardsInHand()
        {
            if (transform.parent == null) return;

            // ลูปสั่งการลูกทุกคนใน Hand Panel ยกเว้นตัวมันเอง ให้หดตัวกลับทันที
            foreach (Transform child in transform.parent)
            {
                if (child != null && child != transform)
                {
                    GameplayCardItem otherCard = child.GetComponent<GameplayCardItem>();
                    if (otherCard != null && !otherCard.IsPlayed)
                    {
                        otherCard.ForceResetVisuals();
                    }
                }
            }
        }

        #endregion
    }
}