using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.UI
{
    /// <summary>
    /// Quản lý hiển thị panel: mỗi thời điểm chỉ 1 panel active.
    /// Lắng nghe BattleEvents để tự động chuyển panel đúng lúc.
    /// BattleFieldPanel (HUD luôn hiển thị) không được quản lý ở đây —
    /// gắn thẳng vào scene và tự bật tắt theo event.
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        public List<BasePanel> panels;

        [Tooltip("Root chứa EntityHUD 4 Pokemon — ẩn khi hiện Dialog")]
        public GameObject globalHudRoot;

        private BasePanel _currentPanel;

        // ── Awake ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (globalHudRoot == null)
            {
                var t = transform.Find("GlobalHUDs_Root (Nhóm Quản Lý Tắt Bật Nhanh)");
                if (t != null) globalHudRoot = t.gameObject;
            }

            if (panels == null || panels.Count == 0)
                panels = new List<BasePanel>(GetComponentsInChildren<BasePanel>(true));
        }

        private void Start()
        {
            SwitchPanel(BattlePanelType.None);
        }

        // ── Event subscriptions ───────────────────────────────────────────────

        private void OnEnable()
        {
            BattleEvents.OnPlayerTurnStart  += HandleTurnStart;
            BattleEvents.OnPrintDialog      += HandlePrintDialog;
            BattleEvents.OnTeamPreviewStart += HandleTeamPreviewStart;
            BattleEvents.OnPartyPanelOpen   += HandlePartyPanelOpen;
            BattleEvents.OnBattleResult     += HandleBattleResult;
        }

        private void OnDisable()
        {
            BattleEvents.OnPlayerTurnStart  -= HandleTurnStart;
            BattleEvents.OnPrintDialog      -= HandlePrintDialog;
            BattleEvents.OnTeamPreviewStart -= HandleTeamPreviewStart;
            BattleEvents.OnPartyPanelOpen   -= HandlePartyPanelOpen;
            BattleEvents.OnBattleResult     -= HandleBattleResult;
        }

        private void HandleTeamPreviewStart(PreviewTeamData _) => SwitchPanel(BattlePanelType.TeamPreview);
        private void HandlePartyPanelOpen(PartyPanelData _)    => SwitchPanel(BattlePanelType.Party);
        private void HandleBattleResult(bool _, string _2)      => SwitchPanel(BattlePanelType.Result);

        // ── Public API ────────────────────────────────────────────────────────

        public void SwitchPanel(BattlePanelType newType)
        {
            // HUD luôn hiển thị trừ khi đang hiện Dialog hoặc TeamPreview toàn màn hình
            if (globalHudRoot != null)
                globalHudRoot.SetActive(
                    newType != BattlePanelType.Dialog &&
                    newType != BattlePanelType.TeamPreview &&
                    newType != BattlePanelType.Result);

            _currentPanel = null;

            foreach (var panel in panels)
            {
                if (panel.PanelType == newType)
                {
                    _currentPanel = panel;
                    panel.Show();
                }
                else
                {
                    panel.Hide();
                }
            }

            BattleEvents.OnPanelChanged?.Invoke(newType);
        }

        // ── Private handlers ──────────────────────────────────────────────────

        private void HandleTurnStart()
        {
            SwitchPanel(BattlePanelType.Command);
        }

        private void HandlePrintDialog(string text, bool autoClose)
        {
            // Chi switch panel neu chua la Dialog - tranh Hide/Show lai lam mat queue
            if (_currentPanel == null || _currentPanel.PanelType != BattlePanelType.Dialog)
                SwitchPanel(BattlePanelType.Dialog);

            if (_currentPanel is BattleDialogPanel dialog)
                dialog.EnqueueMessage(text, autoClose);
        }
    }
}
