using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// UI Component attached to a Card Prefab.
    /// Binds runtime card instances to visual components and handles drag-and-drop
    /// and hover interaction events for tactical timeline placement.
    /// </summary>
    public class GameplayCardItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Visual Bindings")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;

        [Header("Hover Feedback Settings")]
        [SerializeField] [Tooltip("How much to scale up the card on mouse hover.")]
        private float hoverScaleFactor = 1.15f;
        [SerializeField] [Tooltip("The speed at which the card scales on hover.")]
        private float scaleLerpSpeed = 12f;

        // Reference to the active card runtime state represented by this UI element
        public RuntimeCardInstance CardInstance { get; private set; }

        // Flags if the card has been successfully played to the timeline
        public bool IsPlayed { get; set; }

        // Layout memory variables to restore position if drag fails
        private Transform originalParent;
        private int originalSiblingIndex;
        private Vector3 originalScale;
        private Vector3 targetScale;

        // Component references
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Canvas mainCanvas;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            // Add a CanvasGroup if missing so we can toggle blocksRaycasts
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            originalScale = transform.localScale;
            targetScale = originalScale;

            // Find the parent Canvas to handle coordinate translation during dragging
            mainCanvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            // Smoothly animate hover scale scaling
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
        }

        /// <summary>
        /// Populates visual UI elements using runtime card data blueprints.
        /// </summary>
        public void Setup(RuntimeCardInstance instance)
        {
            this.CardInstance = instance;

            if (instance == null) return;

            if (nameText != null)
            {
                nameText.text = instance.blueprint.cardName;
            }

            if (costText != null)
            {
                costText.text = instance.blueprint.energyCost.ToString();
            }

            if (descriptionText != null)
            {
                descriptionText.text = instance.blueprint.cardDescription;
            }

            if (iconImage != null && instance.blueprint.icon != null)
            {
                iconImage.sprite = instance.blueprint.icon;
            }
        }

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Remember parent hand layout group and position
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            // Set parent to canvas to float freely above layouts
            if (mainCanvas != null)
            {
                transform.SetParent(mainCanvas.transform, true);
            }
            else if (originalParent != null && originalParent.parent != null)
            {
                transform.SetParent(originalParent.parent, true);
            }

            // Disable raycasts on this object so drop handlers behind it can receive drops
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0.75f;
            }

            targetScale = originalScale * hoverScaleFactor;
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
            if (IsPlayed) return;

            // Restore raycast capability
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            targetScale = originalScale;

            // If the card wasn't accepted (still has Canvas as parent), return it to the hand panel
            if ((mainCanvas != null && transform.parent == mainCanvas.transform) || transform.parent != originalParent)
            {
                ReturnToHand();
            }
        }

        /// <summary>
        /// Restores the card back to its original layout index in the hand panel.
        /// </summary>
        public void ReturnToHand()
        {
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);
                transform.SetSiblingIndex(originalSiblingIndex);
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }

        #endregion

        #region Hover Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = originalScale * hoverScaleFactor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = originalScale;
        }

        #endregion
    }
}
