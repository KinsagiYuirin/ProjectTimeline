using TMPro;
using UnityEngine;

namespace ProjectAsset.Scripts.Util
{
    public class DamagePopup : MonoBehaviour
    {
        private TMP_Text textMesh;
        private float disappearTimer = 0.5f;
        private Color textColor;

        public void Setup(int damageAmount)
        {
            textMesh = GetComponent<TMP_Text>();
            textMesh.text = damageAmount.ToString();
            textColor = textMesh.color;
        }

        private void Update()
        {
            transform.Translate(Vector3.up * 2f * Time.deltaTime);

            disappearTimer -= Time.deltaTime;
            if (disappearTimer < 0)
            {
                textColor.a -= 5f * Time.deltaTime; // Fade out alpha
                textMesh.color = textColor;
                if (textColor.a <= 0) Destroy(gameObject);
            }
        }
    }
}
