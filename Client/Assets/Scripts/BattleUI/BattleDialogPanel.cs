using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Game.Battle.UI
{
    public class BattleDialogPanel : BasePanel
    {
        [Header("UI Components")]
        public TextMeshProUGUI dialogText;
        public float charsPerSecond = 45f;
        private BattleUIManager _uiManager;

        private void Awake()
        {
            _uiManager = GetComponentInParent<BattleUIManager>();
        }

        private Queue<(string message, bool autoClose)> messageQueue = new Queue<(string, bool)>();
        private bool isTyping = false;
        private Coroutine typeRoutine;

        public bool IsBusy => isTyping || messageQueue.Count > 0;

        private void OnEnable()
        {
            if (dialogText != null) dialogText.text = "";
        }

        private void OnDisable()
        {
            messageQueue.Clear();
            isTyping = false;
            // Xóa rác, dừng các hiệu ứng khi đóng giao diện
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }
        }

        // Đc gọi trực tiếp từ UIManager
        public void EnqueueMessage(string message, bool autoClose)
        {
            messageQueue.Enqueue((message, autoClose));
            if (!isTyping)
            {
                typeRoutine = StartCoroutine(ProcessMessageQueue());
            }
        }

        private IEnumerator ProcessMessageQueue()
        {
            isTyping = true;
            bool lastAutoClose = true;
            while (messageQueue.Count > 0)
            {
                var (msg, autoClose) = messageQueue.Dequeue();
                lastAutoClose = autoClose;
                yield return StartCoroutine(TypeMessageCoroutine(msg));
                // Doi ngan giua cac message de nguoi choi doc
                yield return new WaitForSeconds(0.6f);
            }
            
            isTyping = false;

            if (lastAutoClose)
            {
                if (_uiManager != null) _uiManager.SwitchPanel(BattlePanelType.None);
                else this.Hide();
            }
        }

        private IEnumerator TypeMessageCoroutine(string message)
        {
            dialogText.text = message;
            dialogText.ForceMeshUpdate();
            dialogText.maxVisibleCharacters = 0;

            int totalVisibleCharacters = dialogText.textInfo.characterCount;
            int counter = 0;

            while (counter <= totalVisibleCharacters)
            {
                dialogText.maxVisibleCharacters = counter;
                counter++;
                yield return new WaitForSeconds(1f / charsPerSecond);
            }
            
            BattleEvents.OnActionCompleted?.Invoke();
        }
    }
}
