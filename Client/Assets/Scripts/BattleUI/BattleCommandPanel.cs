using UnityEngine;
using UnityEngine.UI;

namespace Game.Battle.UI
{
    /// <summary>
    /// Panel 4 nút chính trong lượt: Fight, Pokemon, Info, Forfeit.
    /// Nút Pokemon giờ mở Party Panel để đổi Pokemon thực sự.
    /// </summary>
    public class BattleCommandPanel : BasePanel
    {
        [Header("Buttons")]
        public Button fightButton;
        public Button pokemonButton;
        public Button infoButton;
        public Button forfeitButton;

        // Slot hiện tại đang chọn hành động (0 = Slot A, 1 = Slot B)
        // Được NetworkController cập nhật qua SetCurrentSlot()
        public int CurrentSrcSlot { get; set; }

        private BattleUIManager _uiManager;

        private void Awake()
        {
            _uiManager = GetComponentInParent<BattleUIManager>();
            if (_uiManager == null) _uiManager = FindObjectOfType<BattleUIManager>();

            // Tìm đệ quy (hỗ trợ button nằm trong Grid hoặc container con)
            if (fightButton   == null) fightButton   = FindBtn("FightBtn");
            if (pokemonButton == null) pokemonButton = FindBtn("PokemonBtn");
            if (infoButton    == null) infoButton    = FindBtn("InfoBtn");

            // ForfeitBtn thường bóc ra ngoài Panel để luôn hiển thị
            if (forfeitButton == null)
            {
                var obj = GameObject.Find("ForfeitBtn");
                if (obj != null) forfeitButton = obj.GetComponent<Button>();
            }

            fightButton?.onClick.AddListener(() =>
                _uiManager?.SwitchPanel(BattlePanelType.Skill));

            pokemonButton?.onClick.AddListener(OnPokemonClicked);

            infoButton?.onClick.AddListener(() =>
                BattleEvents.OnPrintDialog?.Invoke("Đang kiểm tra tình trạng sân...", true));

            forfeitButton?.onClick.AddListener(() =>
            {
                _uiManager?.SwitchPanel(BattlePanelType.Dialog);
                BattleEvents.OnPrintDialog?.Invoke("Đầu hàng!", false);
            });
        }

        private void OnPokemonClicked()
        {
            // Yêu cầu NetworkController mở Party Panel để chọn switch tự nguyện
            BattleEvents.OnVoluntarySwitchRequested?.Invoke(CurrentSrcSlot);
        }

        Button FindBtn(string btnName)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == btnName) return b;
            return null;
        }
    }
}
