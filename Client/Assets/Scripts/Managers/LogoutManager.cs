using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Game.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonMMO.UI
{
    public class LogoutManager : MonoBehaviour
    {
        [Header("Scene")]
        public string loginSceneName = "Start menu";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        private string ServerUrl =>
            SignalRClient.Instance != null ? SignalRClient.Instance.serverUrl : "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net";

        // ── Gọi từ nút Đăng xuất ─────────────────────────────────────────
        public void OnLogoutClicked()
        {
            _ = LogoutAsync(loadScene: true);
        }

        // ── Gọi từ nút Thoát game ────────────────────────────────────────
        public void OnQuitClicked()
        {
            _ = LogoutThenQuitAsync();
        }

        // ── Nội bộ ───────────────────────────────────────────────────────

        private async Task LogoutAsync(bool loadScene)
        {
            string token = PlayerPrefs.GetString("jwt_token", "");

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{ServerUrl}/api/auth/logout");
                    req.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    await Http.SendAsync(req);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Logout] Không gọi được API logout: {ex.Message}");
                }
            }

            ClearLocalSession();

            if (loadScene)
            {
                // Ngắt SignalR trước khi đổi scene
                if (SignalRClient.Instance != null)
                    await SignalRClient.Instance.DisconnectAsync();

                SceneManager.LoadScene(loginSceneName);
            }
        }

        private async Task LogoutThenQuitAsync()
        {
            await LogoutAsync(loadScene: false);

            if (SignalRClient.Instance != null)
                await SignalRClient.Instance.DisconnectAsync();

            Application.Quit();
        }

        private static void ClearLocalSession()
        {
            PlayerPrefs.DeleteKey("jwt_token");
            PlayerPrefs.DeleteKey("player_id");
            PlayerPrefs.DeleteKey("username");
            PlayerPrefs.DeleteKey("account_id");
            PlayerPrefs.Save();
            FriendListLoader.ClearAvatarCache();
            Debug.Log("[Logout] Đã xóa session local.");
        }
    }
}
