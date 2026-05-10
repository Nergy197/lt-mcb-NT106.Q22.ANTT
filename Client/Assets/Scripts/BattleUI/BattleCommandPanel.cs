using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

            if (forfeitButton == null)
            {
                var obj = GameObject.Find("ForfeitBtn");
                if (obj != null) forfeitButton = obj.GetComponent<Button>();
            }

            // Tự tạo nút nếu vẫn không tìm thấy (scene cũ không có)
            if (forfeitButton == null)
                forfeitButton = CreateForfeitButton();

            fightButton?.onClick.AddListener(() =>
                _uiManager?.SwitchPanel(BattlePanelType.Skill));

            pokemonButton?.onClick.AddListener(OnPokemonClicked);

            infoButton?.onClick.AddListener(() =>
                BattleEvents.OnPrintDialog?.Invoke("Đang kiểm tra tình trạng sân...", true));

            forfeitButton?.onClick.AddListener(() =>
            {
                BattleEvents.OnPlayerSurrender?.Invoke();
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

        Button CreateForfeitButton()
        {
            var go = new GameObject("ForfeitBtn", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.6f, 0.1f, 0.1f, 0.85f);

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.highlightedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
            btn.colors = cb;

            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = "FORFEIT";
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // Đặt dưới InfoBtn (offset -260 so với top-right)
            var btnRt = go.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 1);
            btnRt.anchorMax = new Vector2(1, 1);
            btnRt.pivot     = new Vector2(1, 1);
            btnRt.anchoredPosition = new Vector2(0, -260);
            btnRt.sizeDelta = new Vector2(140, 60);

            return btn;
        }
    }
}
