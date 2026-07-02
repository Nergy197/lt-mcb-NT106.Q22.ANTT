using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    public class WelcomePanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject panelContent;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI playerNameText;

        [Header("Server")]
        [SerializeField] private string serverUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net";

        private void Start()
        {
            // Ẩn panel mặc định
            if (panelContent != null)
                panelContent.SetActive(false);

            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimButtonClicked);
            
            // Lắng nghe kết quả check từ VPManager
            if (VPManager.Instance != null)
            {
                VPManager.Instance.OnWelcomeCheckComplete += HandleWelcomeCheck;
                // Gọi RefreshFromServer để lấy data VP và trạng thái Welcome mới nhất
                // (Vì VPManager nằm ở DontDestroyOnLoad nên nó không tự động Start lại khi chuyển Scene)
                VPManager.Instance.RefreshFromServer();
            }
        }

        private void OnDestroy()
        {
            if (VPManager.Instance != null)
            {
                VPManager.Instance.OnWelcomeCheckComplete -= HandleWelcomeCheck;
            }
        }

        private void HandleWelcomeCheck(bool welcomeClaimed)
        {
            // Nếu chưa claim thì hiện panel
            if (!welcomeClaimed)
            {
                ShowPanel();
            }
        }

        private void ShowPanel()
        {
            if (panelContent != null)
            {
                panelContent.SetActive(true);
            }

            // Gán tên người chơi
            string playerName = PlayerPrefs.GetString("username", "Huấn luyện viên");
            if (playerNameText != null)
            {
                playerNameText.text = $"Chào mừng {playerName} đến với thế giới Pokémon!";
            }
        }

        private void OnClaimButtonClicked()
        {
            if (claimButton != null)
                claimButton.interactable = false;
            StartCoroutine(ClaimWelcomeBonus());
        }

        private IEnumerator ClaimWelcomeBonus()
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.LogError("[Welcome] Không tìm thấy JWT token.");
                if (claimButton != null) claimButton.interactable = true;
                yield break;
            }

            using (var req = new UnityWebRequest($"{serverUrl}/api/currency/claim-welcome", "POST"))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Welcome] Claim failed: {req.responseCode} {req.error} - {req.downloadHandler.text}");
                    if (claimButton != null) claimButton.interactable = true;
                    yield break;
                }

                // Cập nhật VP mới vào hệ thống
                var response = JsonUtility.FromJson<ClaimResponse>(req.downloadHandler.text);
                if (VPManager.Instance != null)
                {
                    VPManager.Instance.ApplyServerBalance(response.vp);
                }

                // Đóng panel
                if (panelContent != null)
                    panelContent.SetActive(false);
            }
        }

        [System.Serializable]
        private class ClaimResponse
        {
            public int vp;
            public bool welcomeClaimed;
        }
    }
}
