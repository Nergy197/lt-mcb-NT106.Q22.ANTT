using System.Collections;
using System.Collections.Generic;
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
        public Image           hpFillImage;

        [Header("Extended UI")]
        public TextMeshProUGUI levelText;
        public Image           iconImage;
        public Image           type1BadgeImage;
        public Image           type2BadgeImage;
        public TextMeshProUGUI type1BadgeText;
        public TextMeshProUGUI type2BadgeText;

        [Header("Status Badge (BRN / PSN / PAR / SLP / FRZ / TOX)")]
        public TextMeshProUGUI statusBadgeText;
        public Image           statusBadgeImage;

        [Header("Tera Badge (hiện khi đã Terastallize)")]
        public TextMeshProUGUI teraBadgeText;
        public Image           teraBadgeImage;

        [Header("Hiển thị HP")]
        public bool showExactHp = true;

        [Header("Màu HP theo %")]
        public Color highHpColor   = new(0.463f, 0.839f, 0.471f);
        public Color mediumHpColor = new(0.957f, 0.773f, 0.263f);
        public Color lowHpColor    = new(0.902f, 0.322f, 0.165f);

        // ── Type color dictionary ────────────────────────────────────────────
        static readonly Dictionary<string, Color> TypeColors
            = new(System.StringComparer.OrdinalIgnoreCase)
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

        // ── Status colors ────────────────────────────────────────────────────
        static readonly Dictionary<string, Color> StatusColors
            = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "BRN", new Color(0.90f, 0.40f, 0.10f) },
            { "PSN", new Color(0.60f, 0.20f, 0.80f) },
            { "TOX", new Color(0.45f, 0.10f, 0.60f) },
            { "PAR", new Color(0.90f, 0.80f, 0.10f) },
            { "SLP", new Color(0.30f, 0.45f, 0.70f) },
            { "FRZ", new Color(0.55f, 0.85f, 0.95f) },
        };

        // ── Awake ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (nameText    == null) nameText    = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (hpText      == null) hpText      = transform.Find("HP_Fill_BG/HP_Value")?.GetComponent<TextMeshProUGUI>();
            if (hpFillImage == null) hpFillImage = transform.Find("HP_Fill_BG/HP_Fill_Image")?.GetComponent<Image>();
            if (levelText   == null) levelText   = transform.Find("Level")?.GetComponent<TextMeshProUGUI>();
            if (iconImage   == null) iconImage   = transform.Find("Avatar_Box/Icon")?.GetComponent<Image>();

            if (statusBadgeText  == null) statusBadgeText  = transform.Find("StatusBadge")?.GetComponent<TextMeshProUGUI>();
            if (statusBadgeImage == null) statusBadgeImage = transform.Find("StatusBadge")?.GetComponent<Image>();
            if (teraBadgeText    == null) teraBadgeText    = transform.Find("TeraBadge")?.GetComponent<TextMeshProUGUI>();
            if (teraBadgeImage   == null) teraBadgeImage   = transform.Find("TeraBadge")?.GetComponent<Image>();
        }

        private void OnEnable()  => BattleEvents.OnHealthChanged += HandleHealthChanged;
        private void OnDisable()
        {
            BattleEvents.OnHealthChanged -= HandleHealthChanged;
            StopAllCoroutines();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void SetupEntity(string newId, string eName, int currentHp, int maxHp)
        {
            entityId = newId;
            if (nameText != null) nameText.text = eName.ToUpper();
            UpdateHealthUIInstant(currentHp, maxHp);
        }

        public void SetLevel(int level)
        {
            if (levelText != null) levelText.text = $"Lv{level}";
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage == null) return;
            iconImage.sprite = sprite;
            iconImage.color  = Color.white;
        }

        public void SetTypes(string type1, string type2 = null)
        {
            ApplyType(type1BadgeImage, type1BadgeText, type1);
            bool hasT2 = !string.IsNullOrEmpty(type2);
            if (type2BadgeImage != null) type2BadgeImage.gameObject.SetActive(hasT2);
            if (hasT2) ApplyType(type2BadgeImage, type2BadgeText, type2);
        }

        /// Hiển thị status condition: "burn", "paralysis", "sleep", "freeze", "poison", "toxic" hoặc null để xoá.
        public void SetStatus(string statusRaw)
        {
            string abbr = AbbrevStatus(statusRaw);
            bool   has  = !string.IsNullOrEmpty(abbr);

            if (statusBadgeText != null)
            {
                statusBadgeText.gameObject.SetActive(has);
                if (has)
                {
                    statusBadgeText.text = abbr;
                    if (StatusColors.TryGetValue(abbr, out var sc)) statusBadgeText.color = sc;
                }
            }
            if (statusBadgeImage != null)
            {
                statusBadgeImage.gameObject.SetActive(has);
                if (has && StatusColors.TryGetValue(abbr, out var sc))
                    statusBadgeImage.color = sc;
            }
        }

        /// Hiển thị Tera type badge khi Pokemon đã Terastallize.
        public void SetTeraType(string teraType, bool isTerastallized)
        {
            bool has = isTerastallized && !string.IsNullOrEmpty(teraType);
            if (teraBadgeText != null)
            {
                teraBadgeText.gameObject.SetActive(has);
                if (has)
                {
                    teraBadgeText.text = $"TERA/{teraType.ToUpper()}";
                    if (TypeColors.TryGetValue(teraType, out var tc)) teraBadgeText.color = tc;
                }
            }
            if (teraBadgeImage != null)
            {
                teraBadgeImage.gameObject.SetActive(has);
                if (has && TypeColors.TryGetValue(teraType, out var tc)) teraBadgeImage.color = tc;
            }
        }

        // ── HP event listener ─────────────────────────────────────────────────

        private void HandleHealthChanged(string id, int currentHp, int maxHp)
        {
            if (id == entityId)
                StartCoroutine(SmoothHealthChange(currentHp, maxHp));
        }

        // ── HP rendering ──────────────────────────────────────────────────────

        private void UpdateHealthUIInstant(int currentHp, int maxHp)
        {
            float pct = (float)currentHp / Mathf.Max(1, maxHp);
            if (hpText != null)
                hpText.text = showExactHp ? $"{currentHp}/{maxHp}" : $"{Mathf.RoundToInt(pct * 100)}%";
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = pct;
                UpdateHpColor(pct);
            }
        }

        private IEnumerator SmoothHealthChange(int targetHp, int maxHp)
        {
            if (hpFillImage == null || hpText == null) yield break;

            float startFill  = hpFillImage.fillAmount;
            float targetFill = (float)targetHp / Mathf.Max(1, maxHp);
            float elapsed    = 0f;
            const float dur  = 0.5f; // Tăng tốc độ trừ máu

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t    = elapsed / dur;
                t          = 1 - (1 - t) * (1 - t); // ease-out
                float fill = Mathf.Lerp(startFill, targetFill, t);
                hpFillImage.fillAmount = fill;

                int display = Mathf.RoundToInt(fill * maxHp);
                hpText.text = showExactHp ? $"{display}/{maxHp}" : $"{Mathf.RoundToInt(fill * 100)}%";
                UpdateHpColor(fill);
                yield return null;
            }

            UpdateHealthUIInstant(targetHp, maxHp);
        }

        private void UpdateHpColor(float pct)
        {
            if (hpFillImage == null) return;
            hpFillImage.color = pct >= 0.5f ? highHpColor
                              : pct >= 0.2f ? mediumHpColor
                              :               lowHpColor;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void ApplyType(Image badge, TextMeshProUGUI label, string typeName)
        {
            if (badge == null || label == null || string.IsNullOrEmpty(typeName)) return;
            badge.gameObject.SetActive(true);
            if (TypeColors.TryGetValue(typeName, out Color c)) badge.color = c;
            label.text = typeName.ToUpper();
        }

        private static string AbbrevStatus(string s) => s?.ToUpperInvariant() switch
        {
            "BURN"               => "BRN",
            "POISON"             => "PSN",
            "TOXIC"              => "TOX",
            "PARALYSIS" or "PAR" => "PAR",
            "SLEEP"              => "SLP",
            "FREEZE"             => "FRZ",
            null or ""           => "",
            var v => v.Length > 3 ? v[..3] : v,
        };
    }
}
