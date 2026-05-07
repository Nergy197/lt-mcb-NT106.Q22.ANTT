using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Chat
{
    public class MessageItemUI : MonoBehaviour
    {
        [Header("UI")]
        public TextMeshProUGUI senderLabel;
        public TextMeshProUGUI contentLabel;
        public TextMeshProUGUI timeLabel;
        public Image bubble;

        [Header("Màu bong bóng")]
        public Color myColor    = new Color(0.55f, 0.85f, 1f);
        public Color theirColor = new Color(0.9f,  0.9f,  0.9f);

        public void SetData(string senderName, string content, System.DateTime time, bool isMine)
        {
            if (senderLabel  != null) senderLabel.text  = isMine ? "Bạn" : senderName;
            if (contentLabel != null) contentLabel.text = content;
            if (timeLabel    != null) timeLabel.text    = time.ToLocalTime().ToString("HH:mm");
            if (bubble       != null) bubble.color      = isMine ? myColor : theirColor;
        }
    }
}
