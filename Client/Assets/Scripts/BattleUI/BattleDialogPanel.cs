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
        public float charsPerSecond = 50f;

        private Queue<string> messageQueue = new Queue<string>();
        private bool isTyping = false;
        private Coroutine typeRoutine;

        private void OnEnable()
        {
            if (dialogText != null) dialogText.text = "";
            // CHÚ Ý: Đã cấm tự lắng nghe sự kiện OnPrintDialog ở đây để chống treo Game
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
            messageQueue.Enqueue(message);
            if (!isTyping)
            {
                typeRoutine = StartCoroutine(ProcessMessageQueue(autoClose));
            }
        }

        private IEnumerator ProcessMessageQueue(bool autoCloseLast)
        {
            isTyping = true;
            while (messageQueue.Count > 0)
            {
                string msg = messageQueue.Dequeue();
                yield return StartCoroutine(TypeMessageCoroutine(msg));
                yield return new WaitForSeconds(1.5f);
            }
            
            isTyping = false;

            if(autoCloseLast)
            {
                BattleUIManager uiManager = GetComponentInParent<BattleUIManager>();
                if (uiManager != null) uiManager.SwitchPanel(BattlePanelType.None);
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
