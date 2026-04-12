using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Battle.UI
{
    public class EntityHUD : MonoBehaviour
    {
        [Header("Định danh")]
        public string entityId;
        
        [Header("Các thành phần giao diện")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI hpText;
        public Image hpFillImage; 

        [Header("Màu sắc tương ứng % Máu")]
        public Color highHpColor = Color.green;
        public Color mediumHpColor = Color.yellow;
        public Color lowHpColor = Color.red;

        // [RESILIENCY UPDATE] Khắc phục triệt để lỗi NullReferenceException do quên Save Scene
        private void Awake()
        {
            if (nameText == null) nameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            
            // Tìm con lồng nhau dựa theo đúng Tool sinh Cảnh mới nhất
            if (hpText == null) hpText = transform.Find("HP_Fill_BG/HP_Value")?.GetComponent<TextMeshProUGUI>();
            if (hpFillImage == null) hpFillImage = transform.Find("HP_Fill_BG/HP_Fill_Image")?.GetComponent<Image>();
        }

        private void OnEnable()
        {
            BattleEvents.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            BattleEvents.OnHealthChanged -= HandleHealthChanged;
            StopAllCoroutines(); 
        }

        public void SetupEntity(string newId, string eName, int currentHp, int maxHp)
        {
            entityId = newId;
            if (nameText != null) nameText.text = eName;
            UpdateHealthUIInstant(currentHp, maxHp);
        }

        private void HandleHealthChanged(string id, int currentHp, int maxHp)
        {
            if (id == entityId)
            {
                StartCoroutine(SmoothHealthChange(currentHp, maxHp));
            }
        }

        private void UpdateHealthUIInstant(int currentHp, int maxHp)
        {
            float hpPercent = (float)currentHp / maxHp;
            if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = hpPercent;
                UpdateHpColor(hpPercent);
            }
        }

        private IEnumerator SmoothHealthChange(int targetHp, int maxHp)
        {
            if (hpFillImage == null || hpText == null) yield break;

            float preValue = hpFillImage.fillAmount;
            float targetValue = (float)targetHp / maxHp;
            
            float duration = 1.0f; 
            float timePassed = 0f;

            while (timePassed < duration)
            {
                timePassed += Time.deltaTime;
                float fill = Mathf.Lerp(preValue, targetValue, timePassed / duration);
                hpFillImage.fillAmount = fill;
                
                int displayHp = Mathf.FloorToInt(fill * maxHp);
                hpText.text = $"{displayHp} / {maxHp}";
                
                UpdateHpColor(fill);
                
                yield return null;
            }

            UpdateHealthUIInstant(targetHp, maxHp);
        }

        private void UpdateHpColor(float hpPercent)
        {
            if (hpPercent >= 0.5f) hpFillImage.color = highHpColor;
            else if (hpPercent >= 0.2f) hpFillImage.color = mediumHpColor;
            else hpFillImage.color = lowHpColor; 
        }
    }
}
