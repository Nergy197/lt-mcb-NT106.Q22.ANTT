using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle.Logic;

namespace Game.Battle.UI
{
    /// <summary>
    /// Panel bench picker dùng cho cả voluntary switch và forced switch.
    /// Forced switch: không có nút Back, header chỉ rõ slot bị hạ.
    /// Voluntary switch: có nút Back → quay về Command panel.
    ///
    /// Layout yêu cầu trong scene:
    ///   - "HeaderText"  (TMP)
    ///   - "SlotList"    (parent của 4 slot cards)
    ///   - Mỗi card có: "Name" (TMP), "HP_BG/HP_Fill" (Image fillAmount),
    ///                  "HP_BG/HP_Text" (TMP), "StatusBadge" (TMP), "TypeRow/Type1" (TMP)
    ///   - "BackBtn"     (Button, ẩn khi forced switch)
    /// </summary>
    public class BattlePartyPanel : BasePanel
    {
        [Header("Header")]
        public TextMeshProUGUI headerText;

        [Header("4 Slot Cards (tự tìm nếu để trống)")]
        public Button[]          partyButtons    = new Button[4];
        public TextMeshProUGUI[] slotNames        = new TextMeshProUGUI[4];
        public Image[]           slotHpFills      = new Image[4];
        public TextMeshProUGUI[] slotHpTexts      = new TextMeshProUGUI[4];
        public TextMeshProUGUI[] slotStatusBadges = new TextMeshProUGUI[4];
        public TextMeshProUGUI[] slotType1Texts   = new TextMeshProUGUI[4];

        [Header("Back (chỉ dùng voluntary switch)")]
        public Button backButton;

        private PartyPanelData _data;
        private BattleUIManager _uiManager;

        // ── Màu HP ─────────────────────────────────────────────────────────
        private static readonly Color HpHigh   = new(0.26f, 0.79f, 0.30f);
        private static readonly Color HpMed    = new(0.95f, 0.77f, 0.26f);
        private static readonly Color HpLow    = new(0.90f, 0.32f, 0.16f);

        // ── Màu status badge ────────────────────────────────────────────────
        private static readonly Dictionary<string, Color> StatusColors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "BRN",  new Color(0.90f, 0.40f, 0.10f) },
            { "PSN",  new Color(0.60f, 0.20f, 0.80f) },
            { "TOX",  new Color(0.45f, 0.10f, 0.60f) },
            { "PAR",  new Color(0.90f, 0.80f, 0.10f) },
            { "SLP",  new Color(0.30f, 0.45f, 0.70f) },
            { "FRZ",  new Color(0.55f, 0.85f, 0.95f) },
        };

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            _uiManager = GetComponentInParent<BattleUIManager>();
            AutoDiscoverSlots();

            if (backButton == null)
                backButton = transform.Find("BackBtn")?.GetComponent<Button>();
            if (headerText == null)
                headerText = transform.Find("HeaderText")?.GetComponent<TextMeshProUGUI>();

            backButton?.onClick.AddListener(OnBackClicked);

            // Đăng ký trong Awake (không phải OnEnable) để không bị hủy khi panel ẩn
            BattleEvents.OnPartyPanelOpen += Open;
        }

        private void OnDestroy() => BattleEvents.OnPartyPanelOpen -= Open;

        // ── Public API ──────────────────────────────────────────────────────

        public void Open(PartyPanelData data)
        {
            _data = data;
            base.Show();

            // Header
            if (headerText != null)
            {
                headerText.text = data.IsForcedSwitch
                    ? $"SLOT {(data.ForcedSlot == 0 ? "A" : "B")} BỊ HẠ — GỬI AI VÀO?"
                    : "CHỌN POKEMON ĐỂ ĐỔI:";
            }

            // Back button chỉ hiện khi voluntary switch
            if (backButton != null)
                backButton.gameObject.SetActive(!data.IsForcedSwitch);

            var available = data.AvailableIdxs != null
                ? new HashSet<int>(data.AvailableIdxs)
                : new HashSet<int>();

            for (int i = 0; i < partyButtons.Length; i++)
            {
                if (partyButtons[i] == null) continue;

                bool hasPkmn = data.Pokemon != null && i < data.Pokemon.Length;
                partyButtons[i].gameObject.SetActive(hasPkmn);
                if (!hasPkmn) continue;

                var pkmn    = data.Pokemon[i];
                bool canSel = available.Contains(pkmn.PartyIndex);

                partyButtons[i].interactable = canSel;

                // Name
                if (slotNames[i] != null) slotNames[i].text = pkmn.Name.ToUpper();

                // Icon — load truc tiep vao Image child
                var iconTrans = partyButtons[i].transform.Find("Icon");
                if (iconTrans == null)
                {
                    // Tao Icon child neu chua co
                    var iconGo = new GameObject("Icon", typeof(RectTransform));
                    iconGo.transform.SetParent(partyButtons[i].transform, false);
                    var rt = iconGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0, 0.5f);
                    rt.anchoredPosition = new Vector2(5, 0); rt.sizeDelta = new Vector2(60, 60);
                    var iconImg = iconGo.AddComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconTrans = iconGo.transform;
                }
                var loader = FindObjectOfType<BattleSpriteLoader>();
                var iconImage = iconTrans.GetComponent<Image>();
                loader?.LoadIconDirect(pkmn.Name, iconImage);

                // HP bar
                float pct = pkmn.MaxHp > 0 ? (float)pkmn.CurrentHp / pkmn.MaxHp : 0f;
                if (slotHpFills[i] != null)
                {
                    slotHpFills[i].fillAmount = pct;
                    slotHpFills[i].color = pct >= 0.5f ? HpHigh : pct >= 0.2f ? HpMed : HpLow;
                }
                if (slotHpTexts[i] != null)
                    slotHpTexts[i].text = pkmn.IsFainted ? "FAINTED" : $"{pkmn.CurrentHp}/{pkmn.MaxHp}";

                // Status badge
                string abbr = AbbrevStatus(pkmn.Status);
                if (slotStatusBadges[i] != null)
                {
                    slotStatusBadges[i].gameObject.SetActive(!string.IsNullOrEmpty(abbr));
                    slotStatusBadges[i].text = abbr;
                    if (StatusColors.TryGetValue(abbr, out var sc))
                        slotStatusBadges[i].color = sc;
                }

                // Type1
                if (slotType1Texts[i] != null)
                    slotType1Texts[i].text = pkmn.Type1?.ToUpper() ?? "";

                // Alpha dim khi khong the chon
                float alpha = canSel ? 1f : 0.4f;
                var img = partyButtons[i].GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = alpha; img.color = c; }

                // Register listener (reset truoc)
                partyButtons[i].onClick.RemoveAllListeners();
                int captured = i;
                partyButtons[i].onClick.AddListener(() => OnSlotClicked(captured));
            }
        }

        // ── Slot discovery ──────────────────────────────────────────────────

        private void AutoDiscoverSlots()
        {
            if (partyButtons[0] != null) return;

            Transform listRoot = transform.Find("SlotList") ?? transform;
            int found = 0;
            foreach (Transform child in listRoot)
            {
                if (found >= 4) break;
                var btn = child.GetComponent<Button>();
                if (btn == null) continue;

                partyButtons[found]     = btn;
                // Tìm "Name" trước, fallback "Text" (do Builder tạo child tên "Text")
                var nameTrans = child.Find("Name") ?? child.Find("Text");
                slotNames[found]       = nameTrans?.GetComponent<TextMeshProUGUI>();
                slotHpFills[found]     = child.Find("HP_BG/HP_Fill")?.GetComponent<Image>();
                slotHpTexts[found]     = child.Find("HP_BG/HP_Text")?.GetComponent<TextMeshProUGUI>();
                slotStatusBadges[found]= child.Find("StatusBadge")?.GetComponent<TextMeshProUGUI>();
                slotType1Texts[found]  = child.Find("TypeRow/Type1")?.GetComponent<TextMeshProUGUI>();
                found++;
            }
            Debug.Log($"[PartyPanel] AutoDiscover: found {found} slots, names[0]={slotNames[0] != null}");
        }

        // ── Callbacks ───────────────────────────────────────────────────────

        private void OnSlotClicked(int displayIdx)
        {
            if (_data?.Pokemon == null || displayIdx >= _data.Pokemon.Length) return;
            BattleEvents.OnPartySlotChosen?.Invoke(_data.Pokemon[displayIdx].PartyIndex);
        }

        private void OnBackClicked()
        {
            BattleEvents.OnPartyPanelCancelled?.Invoke();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string AbbrevStatus(string status) => status?.ToUpperInvariant() switch
        {
            "BURN"               => "BRN",
            "POISON"             => "PSN",
            "TOXIC"              => "TOX",
            "PARALYSIS" or "PAR" => "PAR",
            "SLEEP"              => "SLP",
            "FREEZE"             => "FRZ",
            null or ""           => "",
            var s => s.Length > 3 ? s[..3] : s,
        };
    }
}
