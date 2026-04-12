using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.UI
{
    public class BattleUIManager : MonoBehaviour
    {
        public List<BasePanel> panels;
        public GameObject globalHudRoot; 
        private BasePanel currentPanel;

        private void Awake()
        {
            if (globalHudRoot == null)
            {
                Transform t = transform.Find("GlobalHUDs_Root (Nhóm Quản Lý Tắt Bật Nhanh)");
                if (t != null) globalHudRoot = t.gameObject;
            }
            if (panels == null || panels.Count == 0)
            {
                panels = new List<BasePanel>(GetComponentsInChildren<BasePanel>(true));
            }
        }

        private void Start()
        {
            SwitchPanel(BattlePanelType.None);
        }

        public void SwitchPanel(BattlePanelType newPanelType)
        {
            if (globalHudRoot != null)
            {
                globalHudRoot.SetActive(newPanelType != BattlePanelType.Dialog);
            }

            currentPanel = null;

            // Xóa sổ tất cả Panel đang chồng chéo lên nhau, chỉ giữ Panel được gọi
            foreach (var panel in panels)
            {
                if (panel.PanelType == newPanelType)
                {
                    currentPanel = panel;
                    panel.Show();
                }
                else
                {
                    panel.Hide();
                }
            }
        }

        private void OnEnable()
        {
            BattleEvents.OnPlayerTurnStart += HandlePlayerTurnStart;
            BattleEvents.OnPrintDialog += HandlePrintDialogRequest;
        }

        private void OnDisable()
        {
            BattleEvents.OnPlayerTurnStart -= HandlePlayerTurnStart;
            BattleEvents.OnPrintDialog -= HandlePrintDialogRequest;
        }

        private void HandlePlayerTurnStart()
        {
            SwitchPanel(BattlePanelType.Command);
        }

        private void HandlePrintDialogRequest(string text, bool autoClose)
        {
            SwitchPanel(BattlePanelType.Dialog);
            
            // Giám đốc chuyển thẳng data Log thoại vào Dialog thay vì đợi nó tự chụp Event
            if (currentPanel != null && currentPanel is BattleDialogPanel dialog)
            {
                dialog.EnqueueMessage(text, autoClose);
            }
        }
    }
}
