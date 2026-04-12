using UnityEngine;
using UnityEngine.UI;

namespace Game.Battle.UI
{
    public class BattleSkillPanel : BasePanel
    {
        public Button backButton;
        public Button[] skillButtons;
        
        private BattleUIManager uiManager;

        private void Awake()
        {
            uiManager = GetComponentInParent<BattleUIManager>();
            
            // [RESILIENCY UPDATE] Chống lỗi Null tham chiếu y hệt như CommandPanel
            if (skillButtons == null || skillButtons.Length == 0 || skillButtons[0] == null)
            {
                skillButtons = new Button[4];
                // Lấy 4 nút đầu
                for (int i = 0; i < 4; i++) 
                {
                    skillButtons[i] = transform.GetChild(i).GetComponent<Button>();
                }
            }

            // Nút Back luôn được sinh ra ở vị trí số 4 (index 4)
            if (backButton == null) 
            {
                backButton = transform.GetChild(4).GetComponent<Button>();
            }

            backButton?.onClick.AddListener(OnBackClicked);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i; 
                if (skillButtons[i] != null)
                {
                    skillButtons[i].onClick.AddListener(() => OnSkillClicked(index));
                }
            }
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
