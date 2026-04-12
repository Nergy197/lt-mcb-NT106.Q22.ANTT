using UnityEngine;
using UnityEngine.UI;

namespace Game.Battle.UI
{
    public class BattleCommandPanel : BasePanel
    {
        [Header("eSports Action Menu")]
        public Button fightButton;
        public Button pokemonButton;
        public Button infoButton;
        public Button forfeitButton;

        private BattleUIManager uiManager;

        private void Awake()
        {
            uiManager = GetComponentInParent<BattleUIManager>();

            if (fightButton == null) fightButton = transform.Find("FightBtn")?.GetComponent<Button>();
            if (pokemonButton == null) pokemonButton = transform.Find("PokemonBtn")?.GetComponent<Button>();
            if (infoButton == null) infoButton = transform.Find("InfoBtn")?.GetComponent<Button>();
            
            // Tìm kiếm ForfeitBtn ở phạm vi toàn Scene vì nó được bóc ra ngoài để luôn được nhìn thấy 
            if (forfeitButton == null) 
            {
                var fBtn = GameObject.Find("ForfeitBtn");
                if (fBtn != null) forfeitButton = fBtn.GetComponent<Button>();
            }

            fightButton?.onClick.AddListener(() => uiManager.SwitchPanel(BattlePanelType.Skill));
            
            pokemonButton?.onClick.AddListener(() => {
                uiManager.SwitchPanel(BattlePanelType.None); 
                BattleEvents.OnPrintDialog?.Invoke("[VGC] Menu Switch Pokemon...", true);
                Invoke(nameof(ReopenMenu), 2f);
            });

            infoButton?.onClick.AddListener(() => {
                uiManager.SwitchPanel(BattlePanelType.None); 
                BattleEvents.OnPrintDialog?.Invoke("[BATTLE INFO] Không có hiệu ứng Sân bãi nào hoạt động.", true);
                Invoke(nameof(ReopenMenu), 3f);
            });

            forfeitButton?.onClick.AddListener(() => {
                uiManager.SwitchPanel(BattlePanelType.None); 
                BattleEvents.OnPrintDialog?.Invoke("Đầu hàng ván đấu thành công!", true);
            });
        }

        private void ReopenMenu()
        {
            uiManager.SwitchPanel(BattlePanelType.Command);
        }
    }
}
