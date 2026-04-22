using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonMMO.UI
{
    public class MenuSceneManager : MonoBehaviour
    {
        [Header("Scene Names")]
        public string battleSceneName = "Battle scene";
        public string startMenuSceneName = "Start menu";

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
        public void OnPokedexClicked() => Debug.Log("[Menu] Pokedex Clicked");
        public void OnFriendsClicked() => Debug.Log("[Menu] Friends Clicked");
        public void OnRankClicked()    => Debug.Log("[Menu] Rank Clicked");

        public void QuitGame()
        {
            Debug.Log("[Menu] Quitting game...");
            Application.Quit();
        }
    }
}
