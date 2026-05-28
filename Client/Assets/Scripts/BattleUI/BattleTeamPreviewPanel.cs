using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using PokemonMMO.Audio;

namespace Game.Battle.UI
{
    public class BattleTeamPreviewPanel : BasePanel
    {
        [Header("UI Elements")]
        public TextMeshProUGUI timerText;
        public Button          confirmButton;
        public TextMeshProUGUI confirmLabel;

        [Header("6 Slot Buttons")]
        public Button[]          slotButtons = new Button[6];
        public Image[]           slotIcons   = new Image[6];

        [Header("Enemy Team Display")]
        public GameObject[]      enemySlots = new GameObject[6];
        public Image[]           enemyIcons = new Image[6];

        [Header("SFX")]
        [SerializeField] private AudioClip sfxClick;
        [SerializeField] private AudioClip sfxConfirm;

        private static readonly Color ColorUnselected = new(0.15f, 0.15f, 0.20f, 0.90f);
        private static readonly Color ColorSelected   = new(0.15f, 0.40f, 0.20f, 1.00f);

        private PreviewTeamData _data;
        private readonly List<int> _selectedIndices = new();
        private readonly int[]     _orderOf         = { -1, -1, -1, -1, -1, -1 };

        private void Awake()
        {
            AutoDiscoverSlots();
            BattleEvents.OnTeamPreviewStart += OpenPreview;
        }

        private void OnDestroy()
        {
            BattleEvents.OnTeamPreviewStart -= OpenPreview;
        }

        private bool _previewActive = false;

        public override void Show()
        {
            base.Show();
            AutoDiscoverSlots(); 
            SetupSlotListeners();
            UpdateConfirmButton();
        }

        public override void Hide()
        {
            if (_previewActive) return;
            base.Hide();
        }

        public void OpenPreview(PreviewTeamData data)
        {
            Debug.Log($"[TeamPreview] OpenPreview called. My={data?.MyTeam?.Length}, Opp={data?.OppTeam?.Length}");
            _data = data;
            _selectedIndices.Clear();
            for (int i = 0; i < 6; i++) _orderOf[i] = -1;
            _previewActive = true;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            
            var uiMgr = FindObjectOfType<BattleUIManager>();
            if (uiMgr != null) uiMgr.SwitchPanel(BattlePanelType.TeamPreview);

            Show();
            RefreshAllSlots();
            RefreshEnemySlots();
        }

        private void AutoDiscoverSlots()
        {
            Transform pGrid = transform.Find("PokemonGrid");
            if (pGrid != null)
            {
                for (int i = 0; i < 6 && i < pGrid.childCount; i++)
                {
                    var child = pGrid.GetChild(i);
                    slotButtons[i] = child.GetComponent<Button>();
                    slotIcons[i] = child.Find("Icon")?.GetComponent<Image>();
                    if (slotButtons[i] != null) slotButtons[i].interactable = true;
                }
            }

            Transform eGrid = transform.Find("EnemyGrid");
            if (eGrid != null)
            {
                for (int i = 0; i < 6 && i < eGrid.childCount; i++)
                {
                    var child = eGrid.GetChild(i);
                    enemySlots[i] = child.gameObject;
                    enemyIcons[i] = child.Find("Icon")?.GetComponent<Image>();
                }
            }
        }

        private void SetupSlotListeners()
        {
            for (int i = 0; i < 6; i++)
            {
                if (slotButtons[i] == null) continue;
                slotButtons[i].onClick.RemoveAllListeners();
                int captured = i;
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(captured));
            }
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        private void OnSlotClicked(int index)
        {
            AudioManager.Instance?.PlaySFX(sfxClick != null ? sfxClick : AudioManager.Instance.DefaultClick);
            if (_orderOf[index] >= 0) _selectedIndices.Remove(index);
            else { if (_selectedIndices.Count >= 4) return; _selectedIndices.Add(index); }

            for (int i = 0; i < 6; i++) _orderOf[i] = -1;
            for (int i = 0; i < _selectedIndices.Count; i++) _orderOf[_selectedIndices[i]] = i;

            RefreshAllSlots();
            UpdateConfirmButton();
        }

        private void RefreshAllSlots()
        {
            if (_data?.MyTeam == null) return;
            var loader = FindObjectOfType<Game.Battle.Logic.BattleSpriteLoader>();

            for (int i = 0; i < 6; i++)
            {
                if (slotButtons[i] == null) continue;
                bool has = i < _data.MyTeam.Length;
                slotButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                var pkmn = _data.MyTeam[i];
                var nameTxt = slotButtons[i].transform.Find("PokemonName")?.GetComponent<TextMeshProUGUI>();
                if (nameTxt != null) nameTxt.text = pkmn.Name.ToUpper();

                if (slotIcons[i] != null)
                    loader?.LoadIconDirect(pkmn.Name, slotIcons[i]);

                int order = _orderOf[i];
                var bg = slotButtons[i].GetComponent<Image>();
                if (bg != null) bg.color = (order >= 0) ? ColorSelected : ColorUnselected;

                var badge = slotButtons[i].transform.Find("OrderBadge")?.GetComponent<TextMeshProUGUI>();
                if (badge != null) badge.text = (order >= 0) ? (order + 1).ToString() : "";
            }
        }

        private void RefreshEnemySlots()
        {
            if (_data?.OppTeam == null) return;
            var loader = FindObjectOfType<Game.Battle.Logic.BattleSpriteLoader>();

            for (int i = 0; i < 6; i++)
            {
                if (enemySlots[i] == null) continue;
                bool has = i < _data.OppTeam.Length;
                enemySlots[i].SetActive(has);
                if (!has) continue;

                var pkmn = _data.OppTeam[i];
                var nameTxt = enemySlots[i].GetComponentInChildren<TextMeshProUGUI>();
                if (nameTxt != null) nameTxt.text = pkmn.Name.ToUpper();

                if (enemyIcons[i] != null)
                    loader?.LoadIconDirect(pkmn.Name, enemyIcons[i]);
            }
        }

        private void UpdateConfirmButton()
        {
            if (confirmButton == null) return;
            bool ready = _selectedIndices.Count == 4;
            confirmButton.interactable = ready;
            if (confirmLabel != null) confirmLabel.text = ready ? "CONFIRM" : $"PICK {4 - _selectedIndices.Count}";
        }

        private void OnConfirmClicked()
        {
            if (_selectedIndices.Count != 4) return;
            AudioManager.Instance?.PlaySFX(sfxConfirm != null ? sfxConfirm : AudioManager.Instance.DefaultClick);
            _previewActive = false;
            BattleEvents.OnTeamOrderConfirmed?.Invoke(_selectedIndices.ToArray());
            Hide();
        }
    }
}
