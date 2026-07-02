using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Controller duy nhất quản lý trạng thái Pokedex scene.
    /// Luôn active — chỉ nó mới được gọi SetActive trên các panel.
    /// </summary>
    public class PokedexSceneController : MonoBehaviour
    {
        [Header("Menu Script")]
        public PokedexMenuPanel menuPanel;   // kéo PokedexManager vào đây

        [Header("Menu GameObjects (show/hide khi vào/ra National)")]
        public GameObject[] menuObjects;     // BG_Image, MenuContainer, Pokeball, Pokeball(1)

        [Header("National Panel")]
        public GameObject nationalPanelGO;   // NationalPokedex_Panel

        [Header("Scene")]
        public string menuSceneName = "Menu scene";

        private bool  _skipInput;

        private enum State { Menu, National }
        private State _state;

        private void Awake()
        {
            SwitchTo(State.Menu, skipInput: false);
            if (menuPanel != null) menuPanel.OnItemClicked += OnMenuItemClicked;
        }

        private void OnMenuItemClicked(int index)
        {
            if (_state != State.Menu) return;
            menuPanel?.SelectAndConfirm(index, this);
        }

        private void Update()
        {
            if (_skipInput) { _skipInput = false; return; }
            if (_state == State.Menu) HandleMenuInput();
        }

        // ── Menu input ────────────────────────────────────────────────────

        private void HandleMenuInput()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow))               menuPanel?.NavigateDir(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow))            menuPanel?.NavigateDir(-1);
            else if (Input.GetKeyDown(KeyCode.C))                  menuPanel?.Confirm(this);
            else if (Input.GetKeyDown(KeyCode.X) ||
                     Input.GetKeyDown(KeyCode.Escape))             ExitToMenu();
        }

        // ── Public API (các panel gọi vào đây) ───────────────────────────

        public void OpenNationalPokedex()  => SwitchTo(State.National, skipInput: false);
        public void CloseNationalPokedex() => SwitchTo(State.Menu,     skipInput: true);
        public void ExitToMenu()           => SceneManager.LoadScene(menuSceneName);

        // ── Internal ─────────────────────────────────────────────────────

        private void SwitchTo(State newState, bool skipInput)
        {
            _state     = newState;
            _skipInput = skipInput;

            bool showMenu = newState == State.Menu;
            foreach (var go in menuObjects)
                if (go != null) go.SetActive(showMenu);
            if (nationalPanelGO != null) nationalPanelGO.SetActive(!showMenu);
        }
    }
}
