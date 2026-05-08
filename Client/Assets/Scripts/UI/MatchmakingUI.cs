using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Network;

namespace PokemonMMO.UI
{
    public class MatchmakingUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text countdownLabel;
        [SerializeField] private GameObject matchmakingPanel;

        private void OnEnable()
        {
            MatchmakingManager.OnCountdownTick += UpdateCountdown;
        }

        private void OnDisable()
        {
            MatchmakingManager.OnCountdownTick -= UpdateCountdown;
        }

        private void UpdateCountdown(int secondsLeft)
        {
            if (matchmakingPanel != null && !matchmakingPanel.activeSelf)
            {
                matchmakingPanel.SetActive(true);
            }

            if (countdownLabel != null)
            {
                if (secondsLeft > 0)
                {
                    countdownLabel.text = $"Đang tìm đối thủ... ({secondsLeft}s)";
                }
                else
                {
                    countdownLabel.text = "Không tìm thấy đối thủ — đang ghép với Bot...";
                }
            }
        }

        public void HidePanel()
        {
            if (matchmakingPanel != null)
            {
                matchmakingPanel.SetActive(false);
            }
        }
    }
}
