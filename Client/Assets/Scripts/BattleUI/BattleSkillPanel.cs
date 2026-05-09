using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Battle.UI
{
    /// <summary>
    /// Panel 4 chiêu + nút Terastallize (Gen 9).
    /// Layout yêu cầu:
    ///   - 4 Button con với: "MoveName" (TMP), "TypeAccent" (Image), "MetaRow/PP" (TMP)
    ///   - Button "BackBtn"
    ///   - Button "TeraBtn" với child "TeraLabel" (TMP) — ẩn nếu không có trong scene
    /// </summary>
    public class BattleSkillPanel : BasePanel
    {
        [Header("Move Buttons (4 ô)")]
        public Button[] skillButtons;

        [Header("PP text (tuỳ chọn, tìm trong hierarchy nếu null)")]
        public TextMeshProUGUI[] ppTexts;

        [Header("Back")]
        public Button backButton;

        [Header("Tera Button (Gen 9)")]
        public Button          teraButton;
        public TextMeshProUGUI teraLabel;
        public Image           teraButtonImage;

        // ── Tera state ──────────────────────────────────────────────────────
        // IsTeraActive: BattleNetworkController đọc khi submit move
        public bool IsTeraActive { get; private set; }

        private bool _teraAvailable;

        private static readonly Color TeraOnColor  = new(0.85f, 0.25f, 0.85f);
        private static readonly Color TeraOffColor = new(0.30f, 0.30f, 0.40f);

        // ── Type colors ─────────────────────────────────────────────────────
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

        private BattleUIManager _uiManager;

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            _uiManager = GetComponentInParent<BattleUIManager>();
            if (_uiManager == null) _uiManager = FindObjectOfType<BattleUIManager>();

            // Auto-discover BackBtn và TeraBtn trước để loại trừ khi tìm move buttons
            if (backButton == null) backButton = FindBtn("BackBtn");
            if (teraButton == null) teraButton = FindBtn("TeraBtn");

            // Auto-discover 4 move buttons (đệ quy, loại trừ Back/Tera)
            if (skillButtons == null || skillButtons.Length == 0 || skillButtons[0] == null)
            {
                skillButtons = new Button[4];
                int found = 0;
                foreach (var btn in GetComponentsInChildren<Button>(true))
                {
                    if (found >= 4) break;
                    if (btn == backButton || btn == teraButton) continue;
                    skillButtons[found++] = btn;
                }
            }
            if (teraLabel == null && teraButton != null)
                teraLabel = teraButton.transform.Find("TeraLabel")?.GetComponent<TextMeshProUGUI>()
                         ?? teraButton.GetComponentInChildren<TextMeshProUGUI>();
            if (teraButtonImage == null && teraButton != null)
                teraButtonImage = teraButton.GetComponent<Image>();

            backButton?.onClick.AddListener(OnBackClicked);
            teraButton?.onClick.AddListener(OnTeraClicked);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                if (skillButtons[i] == null) continue;
                int idx = i;
                skillButtons[i].onClick.AddListener(() => OnSkillClicked(idx));
            }
        }

        private void OnEnable()  => BattleEvents.OnTeraAvailabilityChanged += SetTeraAvailable;
        private void OnDisable() => BattleEvents.OnTeraAvailabilityChanged -= SetTeraAvailable;

        // ── Public API ──────────────────────────────────────────────────────

        public void SetMove(int slot, string moveName, string typeName = "normal",
            string category = "Special", int pp = 0, int maxPp = 0)
        {
            _isSelectingTarget = false;
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;

            // Tên chiêu — fallback lấy TMP đầu tiên trong button nếu không tìm thấy "MoveName"
            var nameTmp = skillButtons[slot].transform.Find("MoveName")
                          ?.GetComponent<TextMeshProUGUI>()
                       ?? skillButtons[slot].GetComponentInChildren<TextMeshProUGUI>();
            if (nameTmp != null) nameTmp.text = moveName;

            // Màu accent bar theo type
            var accent = skillButtons[slot].transform.Find("TypeAccent")?.GetComponent<Image>();
            if (accent != null && TypeColors.TryGetValue(typeName, out Color tc))
                accent.color = tc;

            // Type badge text
            var typeBadgeTxt = FindTypeBadgeText(slot);
            if (typeBadgeTxt != null) typeBadgeTxt.text = typeName.ToUpper();

            // PP
            UpdatePP(slot, moveName, pp, maxPp);
            
            // Đảm bảo nút interactable (vừa reset từ target selection)
            if (skillButtons[slot] != null) skillButtons[slot].interactable = true;
        }

        public void SetTargetLabel(int slot, string label)
        {
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;
            var nameTmp = skillButtons[slot].transform.Find("MoveName")
                          ?.GetComponent<TextMeshProUGUI>()
                       ?? skillButtons[slot].GetComponentInChildren<TextMeshProUGUI>();
            if (nameTmp != null) nameTmp.text = label;
        }

        public void SetTargetLabels(string oppA, string oppB, string yourA, string yourB)
        {
            _isSelectingTarget = true;
            string[] labels = { oppA, oppB, yourA, yourB };
            for (int i = 0; i < 4 && i < skillButtons.Length; i++)
            {
                SetTargetLabel(i, labels[i]);
                if (skillButtons[i] != null)
                    skillButtons[i].interactable = (labels[i] != "---");
            }
        }

        /// Được gọi bởi NetworkController sau mỗi lần dùng Tera để disable nút.
        public void SetTeraAvailable(bool available)
        {
            _teraAvailable = available;
            if (!available) IsTeraActive = false;

            if (teraButton != null) teraButton.gameObject.SetActive(available);
            RefreshTeraVisual();
        }

        /// Reset về inactive (gọi sau khi submit move có Tera).
        public void ResetTeraToggle()
        {
            IsTeraActive = false;
            RefreshTeraVisual();
        }

        // ── Button callbacks ─────────────────────────────────────────────────

        private bool _isSelectingTarget;

        private void OnSkillClicked(int skillIndex)
        {
            if (_isSelectingTarget)
            {
                _isSelectingTarget = false;
                _uiManager?.SwitchPanel(BattlePanelType.None);
                BattleEvents.OnTargetSelected?.Invoke(skillIndex);
            }
            else
            {
                _uiManager?.SwitchPanel(BattlePanelType.None);
                BattleEvents.OnPlayerUseSkill?.Invoke(skillIndex);
            }
        }

        private void OnBackClicked()
        {
            _uiManager?.SwitchPanel(BattlePanelType.Command);
        }

        private void OnTeraClicked()
        {
            if (!_teraAvailable) return;
            IsTeraActive = !IsTeraActive;
            RefreshTeraVisual();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void RefreshTeraVisual()
        {
            if (teraButtonImage != null)
                teraButtonImage.color = IsTeraActive ? TeraOnColor : TeraOffColor;
            if (teraLabel != null)
                teraLabel.text = IsTeraActive ? "✦ TERA ON" : "◇ TERA";
        }

        private TextMeshProUGUI FindTypeBadgeText(int slot)
        {
            var metaRow = skillButtons[slot].transform.Find("MetaRow");
            if (metaRow == null) return null;
            if (metaRow.childCount > 0)
                return metaRow.GetChild(0).Find("Text")?.GetComponent<TextMeshProUGUI>();
            return null;
        }

        private void UpdatePP(int slot, string moveName, int pp, int maxPp)
        {
            string ppStr = maxPp > 0 ? $"PP {pp}/{maxPp}" : "";

            if (ppTexts != null && slot < ppTexts.Length && ppTexts[slot] != null)
            {
                ppTexts[slot].text = maxPp > 0 ? ppStr : moveName;
                return;
            }

            var ppTmp = skillButtons[slot].transform.Find("MetaRow/PP")
                        ?.GetComponent<TextMeshProUGUI>();
            if (ppTmp != null) ppTmp.text = ppStr;
        }

        Button FindBtn(string btnName)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == btnName) return b;
            return null;
        }
    }
}
