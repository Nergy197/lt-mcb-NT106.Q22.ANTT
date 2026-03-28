using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Controls the main menu screen: shows/hides the auth panel
    /// when the user clicks Login or Sign Up.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject authPanel;        // The existing AuthUIManager panel

        [Header("Auth Manager")]
        public AuthUIManager authUIManager;

        private void Start()
        {
            ShowMainMenu();
        }

        /// <summary>Called by the Login button on the main menu.</summary>
        public void OnLoginClicked()
        {
            mainMenuPanel.SetActive(false);
            authPanel.SetActive(true);
            authUIManager?.ShowLoginView();
        }

        /// <summary>Called by the Sign Up button on the main menu.</summary>
        public void OnSignUpClicked()
        {
            mainMenuPanel.SetActive(false);
            authPanel.SetActive(true);
            authUIManager?.ShowSignUpView();
        }

        /// <summary>Go back to the main menu (can be wired to a Back button in auth views).</summary>
        public void ShowMainMenu()
        {
            mainMenuPanel.SetActive(true);
            authPanel.SetActive(false);
        }
    }
}
