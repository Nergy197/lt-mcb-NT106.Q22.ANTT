using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonMMO.UI
{
    public class MenuSceneManager : MonoBehaviour
    {
        [Header("Scene Names")]
        public string battleSceneName = "Battle scene";
        public string startMenuSceneName = "Start menu";
        [Header("Optional UI")]
        [SerializeField] private SettingsUIController settingsUIController;

        private void Awake()
        {
            if (settingsUIController == null)
            {
                settingsUIController = FindFirstObjectByType<SettingsUIController>();
            }
        }

        private async void Start()
        {
            if (Game.Network.SignalRClient.Instance != null)
            {
                await Game.Network.SignalRClient.Instance.ConnectAsync();
            }
        }

        public void LoadBattleScene()
        {
            var matchmaking = FindFirstObjectByType<Game.Network.MatchmakingManager>();
            if (matchmaking != null)
            {
                Debug.Log("[Menu] Bắt đầu tìm trận qua MatchmakingManager...");
                matchmaking.StartSearching();
            }
            else
            {
                Debug.LogWarning("[Menu] Không tìm thấy MatchmakingManager. Load offline test.");
                SceneManager.LoadScene(battleSceneName);
            }
        }

        public void LoadStartMenu()
        {
            SceneManager.LoadScene(startMenuSceneName);
        }

        public void OnMailClicked()    => Debug.Log("[Menu] Mail Clicked");

        public void OnPokedexClicked() => SceneManager.LoadScene("PokedexScene");
        public void OnRecruitClicked() => SceneManager.LoadScene("RecuitScene");
        public void OnBattleClicked()  => SceneManager.LoadScene("0_BattleScene");
        public void OnBoxClicked()     => SceneManager.LoadScene("BoxScene");
        public void OnFriendsClicked() => Debug.Log("[Menu] Friends Clicked");
        public void OnRankClicked()    => Debug.Log("[Menu] Rank Clicked");
        public void OnSettingsClicked() => OpenSettings();

        public void OpenSettings()
        {
            if (settingsUIController == null)
            {
                settingsUIController = FindFirstObjectByType<SettingsUIController>();
            }

            settingsUIController?.OpenSettings();
        }

        public void CloseSettings()
        {
            if (settingsUIController == null)
            {
                settingsUIController = FindFirstObjectByType<SettingsUIController>();
            }

            settingsUIController?.CloseSettings();
        }

        public void QuitGame()
        {
            Debug.Log("[Menu] Quitting game...");
            var logout = FindFirstObjectByType<LogoutManager>();
            if (logout != null)
                logout.OnQuitClicked();
            else
                Application.Quit();
        }
    }
}
