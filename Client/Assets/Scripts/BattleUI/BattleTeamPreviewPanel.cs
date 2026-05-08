using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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

        private bool _previewActive = false; // Guard: chặn Hide khi đang chọn team

        public override void Show()
        {
            base.Show();
            AutoDiscoverSlots(); 
            SetupSlotListeners();
            UpdateConfirmButton();
        }

        public override void Hide()
        {
            if (_previewActive)
            {
                Debug.LogWarning($"[TeamPreview] BLOCKED Hide() — preview is active! Caller:\n{System.Environment.StackTrace}");
                return; // CHẶN: không cho tắt khi đang chọn team
            }
            base.Hide();
        }

        public void OpenPreview(PreviewTeamData data)
        {
            Debug.Log($"[TeamPreview] OpenPreview called. MyTeam={data?.MyTeam?.Length}, OppTeam={data?.OppTeam?.Length}");
            _data = data;
            if (_data.MyTeam == null || _data.MyTeam.Length == 0)
            {
                Debug.LogError("[TeamPreview] YOUR TEAM IS EMPTY! Check database or seeding logic.");
            }
            _selectedIndices.Clear();
            for (int i = 0; i < 6; i++) _orderOf[i] = -1;
            _previewActive = true; // Bật guard: chặn Hide

            gameObject.SetActive(true);
            
            // Force lên trên cùng trong hierarchy để render và nhận click trước các panel khác
            transform.SetAsLastSibling();
            
            // Yêu cầu UIManager ẩn hết panel khác (Command, Skill, Dialog...)
            var uiMgr = FindObjectOfType<BattleUIManager>();
            if (uiMgr != null) uiMgr.SwitchPanel(BattlePanelType.TeamPreview);

            Show();
            RefreshAllSlots();
            RefreshEnemySlots();
        }

        private void AutoDiscoverSlots()
        {
            Transform pGrid = transform.Find("PokemonGrid");
            Debug.Log($"[TeamPreview] AutoDiscover: PokemonGrid found={pGrid != null}");
            if (pGrid != null)
            {
                for (int i = 0; i < 6 && i < pGrid.childCount; i++)
                {
                    var child = pGrid.GetChild(i);
                    slotButtons[i] = child.GetComponent<Button>();
                    slotIcons[i] = child.Find("Icon")?.GetComponent<Image>();
                    if (slotButtons[i] != null) slotButtons[i].interactable = true;
                }
                Debug.Log($"[TeamPreview] Found {pGrid.childCount} player slots");
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
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                if (slotButtons[i] == null) continue;
                count++;
                slotButtons[i].onClick.RemoveAllListeners();
                int captured = i;
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(captured));
            }
            Debug.Log($"[TeamPreview] SetupListeners: {count} buttons wired");

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        private void OnSlotClicked(int index)
        {
            Debug.Log($"[TeamPreview] Slot {index} clicked! Selected so far: {_selectedIndices.Count}");
            if (_orderOf[index] >= 0)
            {
                // Bỏ chọn
                _selectedIndices.Remove(index);
            }
            else
            {
                // Chọn (tối đa 4)
                if (_selectedIndices.Count >= 4) return;
                _selectedIndices.Add(index);
            }

            // Cập nhật lại mảng order
            for (int i = 0; i < 6; i++) _orderOf[i] = -1;
            for (int i = 0; i < _selectedIndices.Count; i++)
            {
                _orderOf[_selectedIndices[i]] = i;
            }

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

                // Load Icon
                if (slotIcons[i] != null)
                    loader?.LoadSpriteForSlot("", slotButtons[i].name, pkmn.Name.ToLower(), false);

                // UI phản hồi chọn
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
                    loader?.LoadSpriteForSlot("", enemySlots[i].name, pkmn.Name.ToLower(), false);
            }
        }

        private void UpdateConfirmButton()
        {
            if (confirmButton == null) return;
            bool ready = _selectedIndices.Count == 4;
            confirmButton.interactable = ready;
            if (confirmLabel != null)
                confirmLabel.text = ready ? "CONFIRM TEAM" : $"PICK {4 - _selectedIndices.Count} MORE";
        }

        private void OnConfirmClicked()
        {
            if (_selectedIndices.Count != 4) return;
            _previewActive = false; // Tắt guard: cho phép Hide
            BattleEvents.OnTeamOrderConfirmed?.Invoke(_selectedIndices.ToArray());
            Hide();
        }
    }
}
