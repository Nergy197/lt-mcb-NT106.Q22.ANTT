using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_DialogBox : MonoBehaviour
    {
        [Header("UI Components")]
        public Image background;
        public Text dialogText;

        [Header("Typing Settings")]
        public float charPerSec = 60f;
        public float autoAdvanceDelay = 1.5f; // Thời gian chờ trước khi tự động qua thông báo tiếp theo

        private bool _isTyping = false;
        private bool _skipTyping = false;

        public IEnumerator DrawTextAndWait(string text, bool autoAdvance = true)
        {
            if (dialogText == null) yield break;

            gameObject.SetActive(true);
            dialogText.text = "";
            _isTyping = true;
            _skipTyping = false;

            float waitTime = 1f / charPerSec;

            foreach (char c in text)
            {
                if (_skipTyping)
                {
                    dialogText.text = text;
                    break;
                }

                dialogText.text += c;
                yield return new WaitForSeconds(waitTime);
            }

            _isTyping = false;

            if (autoAdvance)
            {
                yield return new WaitForSeconds(autoAdvanceDelay);
            }
        }

        public void SkipTyping()
        {
            if (_isTyping)
            {
                _skipTyping = true;
            }
        }

        public void SetTextInstant(string text)
        {
            gameObject.SetActive(true);
            _isTyping = false;
            
            if (dialogText != null) 
            {
                dialogText.text = text;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (dialogText != null) 
            {
                dialogText.text = "";
            }
        }

        void Update()
        {
            // Cho phép nhấn chuột trái hoặc phím Space/Enter để skip typing
            if (_isTyping && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                SkipTyping();
            }
        }
    }
}
