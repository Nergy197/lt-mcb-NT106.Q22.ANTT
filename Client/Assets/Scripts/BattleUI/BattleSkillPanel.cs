using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Battle.UI
{
    public class BattleSkillPanel : BasePanel
    {
        [Header("Các nút chiêu (4 ô)")]
        public Button[] skillButtons;

        [Header("PP text tương ứng (tuỳ chọn)")]
        public TextMeshProUGUI[] ppTexts;

        [Header("Nút Back")]
        public Button backButton;

        private BattleUIManager uiManager;

        // Type color map (giống EntityHUD)
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
            uiManager = GetComponentInParent<BattleUIManager>();

            if (skillButtons == null || skillButtons.Length == 0 || skillButtons[0] == null)
            {
                skillButtons = new Button[4];
                for (int i = 0; i < 4 && i < transform.childCount; i++)
                    skillButtons[i] = transform.GetChild(i).GetComponent<Button>();
            }

            if (backButton == null && transform.childCount > 4)
                backButton = transform.GetChild(4).GetComponent<Button>();

            backButton?.onClick.AddListener(OnBackClicked);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                int idx = i;
                skillButtons[i]?.onClick.AddListener(() => OnSkillClicked(idx));
            }
        }

        // ── Public API: BattleNetworkController gọi để nạp dữ liệu chiêu ──
        public void SetMove(int slot, string moveName, string typeName = "normal",
            string category = "Special", int pp = 0, int maxPp = 0)
        {
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;

            // Cập nhật tên chiêu
            var nameTmp = skillButtons[slot].transform.Find("MoveName")?.GetComponent<TextMeshProUGUI>();
            if (nameTmp != null) nameTmp.text = moveName;

            // Cập nhật màu accent bar theo type
            var accent = skillButtons[slot].transform.Find("TypeAccent")?.GetComponent<Image>();
            if (accent != null && TypeColors.TryGetValue(typeName, out Color tc))
                accent.color = tc;

            // Cập nhật type badge text trong MetaRow
            var typeBadgeTxt = skillButtons[slot].transform.Find("MetaRow/TypeBadge_" + typeName + "/Text")
                               ?.GetComponent<TextMeshProUGUI>();
            if (typeBadgeTxt == null)
            {
                // Fallback: tìm badge đầu tiên trong MetaRow
                var metaRow = skillButtons[slot].transform.Find("MetaRow");
                if (metaRow != null && metaRow.childCount > 0)
                    typeBadgeTxt = metaRow.GetChild(0).Find("Text")?.GetComponent<TextMeshProUGUI>();
            }
            if (typeBadgeTxt != null) typeBadgeTxt.text = typeName.ToUpper();

            // Cập nhật PP
            if (ppTexts != null && slot < ppTexts.Length && ppTexts[slot] != null)
            {
                ppTexts[slot].text = maxPp > 0 ? $"PP {pp}/{maxPp}" : moveName; // fallback plain name
            }
            else
            {
                // Tìm trong hierarchy
                var ppTmp = skillButtons[slot].transform.Find("MetaRow/PP")?.GetComponent<TextMeshProUGUI>();
                if (ppTmp != null) ppTmp.text = maxPp > 0 ? $"PP {pp}/{maxPp}" : "";
            }
        }

        // Được gọi từ NetworkController khi vào phase chọn mục tiêu (reuse slot 0–1 làm nút target)
        public void SetTargetLabel(int slot, string label)
        {
            if (slot < 0 || slot >= skillButtons.Length || skillButtons[slot] == null) return;
            var nameTmp = skillButtons[slot].transform.Find("MoveName")?.GetComponent<TextMeshProUGUI>();
            if (nameTmp != null) nameTmp.text = label;
        }

        private void OnBackClicked()
        {
            uiManager.SwitchPanel(BattlePanelType.Command);
        }

        private void OnSkillClicked(int skillIndex)
        {
            uiManager.SwitchPanel(BattlePanelType.None);
            BattleEvents.OnPlayerUseSkill?.Invoke(skillIndex);
        }
    }
}
