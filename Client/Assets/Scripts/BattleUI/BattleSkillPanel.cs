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
            { "normal",   new Color(0.659f, 0.659f, 0.475f) },
            { "fire",     new Color(0.937f, 0.502f, 0.188f) },
            { "water",    new Color(0.404f, 0.537f, 0.937f) },
            { "grass",    new Color(0.486f, 0.776f, 0.314f) },
            { "electric", new Color(0.973f, 0.820f, 0.125f) },
            { "ice",      new Color(0.596f, 0.847f, 0.847f) },
            { "fighting", new Color(0.753f, 0.188f, 0.157f) },
            { "poison",   new Color(0.627f, 0.251f, 0.627f) },
            { "ground",   new Color(0.886f, 0.749f, 0.376f) },
            { "flying",   new Color(0.659f, 0.565f, 0.937f) },
            { "psychic",  new Color(0.973f, 0.345f, 0.533f) },
            { "bug",      new Color(0.659f, 0.722f, 0.125f) },
            { "rock",     new Color(0.722f, 0.627f, 0.220f) },
            { "ghost",    new Color(0.439f, 0.345f, 0.596f) },
            { "dragon",   new Color(0.439f, 0.220f, 0.973f) },
            { "dark",     new Color(0.439f, 0.345f, 0.282f) },
            { "steel",    new Color(0.722f, 0.722f, 0.816f) },
            { "fairy",    new Color(0.839f, 0.518f, 0.627f) },
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

        public void SetMove(int slot, MoveSlot move)
        {
            _isSelectingTarget = false;
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;

            string moveName = move != null ? (move.Name ?? "???") : "---";
            string typeName = move != null ? (move.Type ?? "normal") : "normal";

            // Tên chiêu — fallback lấy TMP đầu tiên trong button nếu không tìm thấy "MoveName"
            var nameTmp = skillButtons[slot].transform.Find("MoveName")
                          ?.GetComponent<TextMeshProUGUI>()
                       ?? skillButtons[slot].GetComponentInChildren<TextMeshProUGUI>();
            if (nameTmp != null)
            {
                nameTmp.text = moveName;
                nameTmp.color = Color.white; // Chữ trắng cho chiêu thức
            }

            // Set color based on type
            var accent = skillButtons[slot].transform.Find("TypeAccent")?.GetComponent<Image>();
            var btnImg = skillButtons[slot].GetComponent<Image>();

            if (TypeColors.TryGetValue(typeName, out Color tc))
            {
                if (accent != null)
                {
                    accent.gameObject.SetActive(true);
                    accent.color = tc;
                }
                if (btnImg != null)
                {
                    // Lighten the button color slightly but keep it identifiable
                    btnImg.color = new Color(tc.r, tc.g, tc.b, 0.7f);
                }
            }
            else
            {
                if (accent != null) accent.gameObject.SetActive(false);
                if (btnImg != null) btnImg.color = Color.white;
            }

            // Type badge text
            var typeBadgeTxt = FindTypeBadgeText(slot);
            if (typeBadgeTxt != null) typeBadgeTxt.text = typeName.ToUpper();

            // PP
            UpdatePP(slot, move);
            
            // Đảm bảo nút interactable (vừa reset từ target selection)
            if (skillButtons[slot] != null) skillButtons[slot].interactable = true;
        }

        public void SetTargetLabel(int slot, string label)
        {
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;
            var nameTmp = skillButtons[slot].transform.Find("MoveName")
                          ?.GetComponent<TextMeshProUGUI>()
                       ?? skillButtons[slot].GetComponentInChildren<TextMeshProUGUI>();
            if (nameTmp != null)
            {
                nameTmp.text = label.ToUpper();
                nameTmp.color = Color.black; // Chữ đen trên nền trắng
            }
        }

        public void SetTargetLabels(string oppA, string oppB, string yourA, string yourB)
        {
            _isSelectingTarget = true;
            string[] labels = { oppA, oppB, yourA, yourB };
            for (int i = 0; i < 4 && i < skillButtons.Length; i++)
            {
                SetTargetLabel(i, labels[i]);
                if (skillButtons[i] != null)
                {
                    skillButtons[i].interactable = (labels[i] != "---");
                    
                    // Reset màu sắc khi chọn mục tiêu
                    var btnImg = skillButtons[i].GetComponent<Image>();
                    if (btnImg != null) btnImg.color = Color.white;

                    var accent = skillButtons[i].transform.Find("TypeAccent")?.GetComponent<Image>();
                    if (accent != null) accent.gameObject.SetActive(false);

                    var badge = FindTypeBadgeText(i);
                    if (badge != null) badge.text = "";

                    var ppTmp = skillButtons[i].transform.Find("MetaRow/PP")?.GetComponent<TextMeshProUGUI>()
                             ?? skillButtons[i].transform.Find("PP")?.GetComponent<TextMeshProUGUI>();
                    if (ppTmp != null) ppTmp.text = "";
                }
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
            if (_isSelectingTarget)
            {
                BattleEvents.OnSkillPanelCancelled?.Invoke();
            }
            else
            {
                _uiManager?.SwitchPanel(BattlePanelType.Command);
            }
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

        private void UpdatePP(int slot, MoveSlot move)
        {
            if (move == null || move.MaxPp <= 0)
            {
                if (ppTexts != null && slot < ppTexts.Length && ppTexts[slot] != null)
                {
                    ppTexts[slot].text = move?.Name ?? "---";
                }
                else
                {
                    var tmp = skillButtons[slot].transform.Find("MetaRow/PP")?.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "";
                }
                return;
            }

            string ppStr = $"PP {move.CurrentPp}/{move.MaxPp}";

            List<string> tags = new List<string>();

            // Target type indicator
            switch (move.TargetType)
            {
                case 1: tags.Add("Spread"); break;
                case 3: tags.Add("Ally");   break;
                case 4: tags.Add("All");    break;
                case 5: tags.Add("Self");   break;
                case 6: tags.Add("Rnd");    break;
            }

            // Weather / terrain indicator
            string weatherTag = move.Effect switch
            {
                "sun"              => "☀Sun",
                "rain"             => "🌧Rain",
                "sandstorm"        => "🌪Sand",
                "snow"             => "❄Snow",
                "hail"             => "❄Hail",
                "grassy-terrain"   => "🌿Grass",
                "electric-terrain" => "⚡Elec",
                "psychic-terrain"  => "✦Psych",
                "misty-terrain"    => "☁Mist",
                _ => null
            };
            if (weatherTag != null) tags.Add(weatherTag);

            // Stat changes
            if (move.StatChanges != null && move.StatChanges.Count > 0)
            {
                foreach (var sc in move.StatChanges)
                {
                    string abbrev = sc.Stat?.ToLower() switch
                    {
                        "atk" => "ATK", "def" => "DEF", "spa" => "SpA",
                        "spd" => "SpD", "spe" => "SPE", "acc" => "ACC", "eva" => "EVA",
                        _ => sc.Stat?.ToUpper() ?? "?"
                    };
                    string sign = sc.Stages >= 0 ? "+" : "";
                    tags.Add($"{abbrev}{sign}{sc.Stages}");
                }
            }

            if (tags.Count > 0) ppStr += $" | {string.Join(" ", tags)}";

            if (ppTexts != null && slot < ppTexts.Length && ppTexts[slot] != null)
            {
                ppTexts[slot].text = ppStr;
                return;
            }

            var ppTmp = skillButtons[slot].transform.Find("MetaRow/PP")?.GetComponent<TextMeshProUGUI>();
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
