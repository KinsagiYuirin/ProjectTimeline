using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace ProjectTimeline.Timeline
{
    [RequireComponent(typeof(RectTransform))]
    public class HandCardLayoutManager : MonoBehaviour
    {
        [SerializeField] private float cardWidth = 240f;
        [SerializeField] private float maxSpacing = 140f; 
        [SerializeField] private float tweenDuration = 0.3f;
        [SerializeField] private Ease tweenEase = Ease.OutCubic;

        // ย้ายมาประกาศตรงนี้เพื่อลดการ Allocate ขยะในหน่วยความจำ
        private readonly List<GameplayCardItem> currentActiveCards = new List<GameplayCardItem>();
        private readonly HashSet<string> seenInstanceIds = new HashSet<string>();
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateLayout();
        }

        /// <summary>
        /// คำนวณพิกัดและสไลด์การ์ดบนมืออย่างแม่นยำ พร้อมระบบป้องกันการซ้อนทับอัจฉริยะ
        /// </summary>
        public void UpdateLayout()
        {
            if (rectTransform == null) return;

            currentActiveCards.Clear();
            seenInstanceIds.Clear();

            // 🔥 Safety Guard ชั้นที่ 1: กรองเฉพาะการ์ดที่ใช้งานได้จริง และกำจัดไพ่ผี/ไพ่ซ้ำทันที
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;

                GameplayCardItem card = child.GetComponent<GameplayCardItem>();
                
                if (card != null && !card.IsPlayed)
                {
                    // ป้องกันการ์ดที่ยังไม่พร้อมใช้งาน หรือไม่มีข้อมูลตัวตน
                    if (card.CardInstance == null) continue;

                    // ถ้าระบบตรวจเจอว่ามี "การ์ดรหัสซ้ำ (Duplicate ID)" งอกเข้ามาในระบบ Hierarchy
                    if (seenInstanceIds.Contains(card.CardInstance.instanceId))
                    {
                        child.SetParent(null); // ตัดขาด Parent ทันที
                        Destroy(child.gameObject); // ทำลายทิ้งเพื่อความปลอดภัยของระบบ
                        continue;
                    }

                    seenInstanceIds.Add(card.CardInstance.instanceId);
                    currentActiveCards.Add(card);
                }
            }

            int cardCount = currentActiveCards.Count;
            if (cardCount == 0) return;

            // 🔥 Safety Guard ชั้นที่ 2: ดักจับขนาด Hand Panel ห้ามเป็น 0 หรือติดลบเด็ดขาด
            float totalPanelWidth = rectTransform.rect.width;
            if (totalPanelWidth <= 0f) totalPanelWidth = 800f; // กำหนดค่า Default ป้องกันสูตรคณิตศาสตร์พัง

            // 2. ตรรกะคำนวณระยะห่างกระจายจากจุดศูนย์กลาง
            float spacing = maxSpacing;
            if (cardCount > 1)
            {
                float preferredWidth = ((cardCount - 1) * maxSpacing) + cardWidth;
                if (preferredWidth > totalPanelWidth)
                {
                    spacing = (totalPanelWidth - cardWidth) / (cardCount - 1);
                    
                    // 🔥 Safety Guard ชั้นที่ 3: ห้ามระยะห่างบีบตัวจนต่ำกว่า 30 พิกเซล (ดักจับเอฟเฟกต์ติดลบ)
                    spacing = Mathf.Max(30f, spacing); 
                }
            }

            float startX = -((cardCount - 1) * spacing) / 2f;

            // 3. สั่งยิงแอนิเมชัน DOTween ส่งไพ่ร่อนเข้าตำแหน่งพิกัดที่แท้จริง
            for (int i = 0; i < cardCount; i++)
            {
                GameplayCardItem card = currentActiveCards[i];
                float targetX = startX + (i * spacing);

                RectTransform cardRect = card.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    // เคลียร์ทวีนเก่าออกให้สะอาด ป้องกันการแย่งพิกัดสลับไปมาในเฟรมเดียวกัน
                    cardRect.DOKill();
                    
                    // บังคับวิ่งเข้าหาพิกัด Target X และล็อกค่าพิกัด Y ไว้ที่ 0f เสมอเพื่อความเสถียร
                    cardRect.DOAnchorPos(new Vector2(targetX, 0f), tweenDuration).SetEase(tweenEase);
                }
            }
        }
    }
}