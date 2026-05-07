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

        [Header("Core UI (tự tìm nếu bỏ trống)")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI hpText;
        public Image hpFillImage;

        [Header("Extended UI (Champions Style)")]
        public TextMeshProUGUI levelText;
        public Image iconImage;
        public Image type1BadgeImage;
        public Image type2BadgeImage;
        public TextMeshProUGUI type1BadgeText;
        public TextMeshProUGUI type2BadgeText;

        [Header("Hiển thị HP")]
        public bool showExactHp = true;

        [Header("Màu HP theo %")]
        public Color highHpColor   = new Color(0.463f, 0.839f, 0.471f);
        public Color mediumHpColor = new Color(0.957f, 0.773f, 0.263f);
        public Color lowHpColor    = new Color(0.902f, 0.322f, 0.165f);

        // Type colors dictionary (mirror của BattleSceneSetupTool)
        static readonly System.Collections.Generic.Dictionary<string, Color> TypeColors
            = new System.Collections.Generic.Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "fire",     new Color(0.863f, 0.447f, 0.220f) },
            { "water",    new Color(0.282f, 0.580f, 0.863f) },
            { "grass",    new Color(0.427f, 0.820f, 0.420f) },
            { "electric", new Color(0.941f, 0.851f, 0.220f) },
            { "ice",      new Color(0.663f, 0.878f, 0.929f) },
            { "fighting", new Color(0.722f, 0.302f, 0.180f) },
            { "poison",   new Color(0.600f, 0.200f, 0.600f) },
            { "ground",   new Color(0.753f, 0.647f, 0.337f) },
            { "flying",   new Color(0.522f, 0.522f, 0.859f) },
            { "psychic",  new Color(0.882f, 0.278f, 0.416f) },
            { "bug",      new Color(0.620f, 0.780f, 0.220f) },
            { "rock",     new Color(0.580f, 0.537f, 0.361f) },
            { "ghost",    new Color(0.478f, 0.298f, 0.600f) },
            { "dragon",   new Color(0.361f, 0.220f, 0.780f) },
            { "dark",     new Color(0.416f, 0.376f, 0.298f) },
            { "steel",    new Color(0.600f, 0.620f, 0.722f) },
            { "fairy",    new Color(0.922f, 0.682f, 0.780f) },
            { "normal",   new Color(0.698f, 0.678f, 0.620f) },
        };

        private void Awake()
        {
            if (nameText    == null) nameText    = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (hpText      == null) hpText      = transform.Find("HP_Fill_BG/HP_Value")?.GetComponent<TextMeshProUGUI>();
            if (hpFillImage == null) hpFillImage = transform.Find("HP_Fill_BG/HP_Fill_Image")?.GetComponent<Image>();
            if (levelText   == null) levelText   = transform.Find("Level")?.GetComponent<TextMeshProUGUI>();
            if (iconImage   == null) iconImage   = transform.Find("Avatar_Box/Icon")?.GetComponent<Image>();
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
            if (nameText != null) nameText.text = eName.ToUpper();
            UpdateHealthUIInstant(currentHp, maxHp);
        }

        public void SetLevel(int level)
        {
            if (levelText != null) levelText.text = $"LV {level}";
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage == null) return;
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
        }

        public void SetTypes(string type1, string type2 = null)
        {
            ApplyType(type1BadgeImage, type1BadgeText, type1);
            if (!string.IsNullOrEmpty(type2))
            {
                ApplyType(type2BadgeImage, type2BadgeText, type2);
                if (type2BadgeImage != null) type2BadgeImage.gameObject.SetActive(true);
            }
            else
            {
                if (type2BadgeImage != null) type2BadgeImage.gameObject.SetActive(false);
            }
        }

        void ApplyType(Image badge, TextMeshProUGUI label, string typeName)
        {
            if (badge == null || label == null || string.IsNullOrEmpty(typeName)) return;
            badge.gameObject.SetActive(true);
            if (TypeColors.TryGetValue(typeName, out Color c)) badge.color = c;
            label.text = typeName.ToUpper();
        }

        private void HandleHealthChanged(string id, int currentHp, int maxHp)
        {
            if (id == entityId)
                StartCoroutine(SmoothHealthChange(currentHp, maxHp));
        }

        private void UpdateHealthUIInstant(int currentHp, int maxHp)
        {
            float pct = (float)currentHp / Mathf.Max(1, maxHp);
            if (hpText != null)
                hpText.text = showExactHp ? $"{currentHp} / {maxHp}" : $"{Mathf.RoundToInt(pct * 100)}%";
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = pct;
                UpdateHpColor(pct);
            }
        }

        private IEnumerator SmoothHealthChange(int targetHp, int maxHp)
        {
            if (hpFillImage == null || hpText == null) yield break;

            float startFill = hpFillImage.fillAmount;
            float targetFill = (float)targetHp / Mathf.Max(1, maxHp);
            float duration = 0.8f, elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // ease-out
                t = 1 - (1 - t) * (1 - t);
                float fill = Mathf.Lerp(startFill, targetFill, t);
                hpFillImage.fillAmount = fill;
                int displayHp = Mathf.RoundToInt(fill * maxHp);
                if (hpText != null)
                    hpText.text = showExactHp ? $"{displayHp} / {maxHp}" : $"{Mathf.RoundToInt(fill * 100)}%";
                UpdateHpColor(fill);
                yield return null;
            }

            UpdateHealthUIInstant(targetHp, maxHp);
        }

        private void UpdateHpColor(float pct)
        {
            if (hpFillImage == null) return;
            if (pct >= 0.5f)      hpFillImage.color = highHpColor;
            else if (pct >= 0.2f) hpFillImage.color = mediumHpColor;
            else                  hpFillImage.color = lowHpColor;
        }
    }
}
