using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Network;
using PokemonMMO.UI;

namespace PokemonArena.UI
{
    /// <summary>
    /// Owns the list of ModeButtons. Tracks current selection,
    /// handles keyboard nav (Up/Down/Enter), and fires events on confirm.
    /// </summary>
    public class ModeMenu : MonoBehaviour
    {
        [Tooltip("In display order: Ranked, Casual, Private.")]
        public List<ModeButton> buttons = new List<ModeButton>();

        [Tooltip("Selected on Start.")]
        public int defaultIndex = 0;

        public string menuSceneName = "Menu scene";

        [SerializeField] private PrivateRoomPanel privateRoomPanel;

        public System.Action<ModeButton.Mode> OnModeConfirmed;

        int  currentIndex;
        bool _isSearching;
        GameObject _clickOutsideOverlay;

        void Start()
        {
            if (buttons.Count == 0) return;
            currentIndex = Mathf.Clamp(defaultIndex, 0, buttons.Count - 1);
            ApplySelection();

            // Bấm ra ngoài vùng 3 nút mode = quay lại Menu chính (giống phím X/Escape)
            if (buttons[0] != null)
                ClickOutsideOverlay.Show(ref _clickOutsideOverlay, buttons[0].transform.parent.gameObject, OnClickOutside);
        }

        void OnClickOutside()
        {
            if (_isSearching) return;
            SceneManager.LoadScene(menuSceneName);
        }

        void OnEnable()  => MatchmakingManager.OnSearchCancelled += HandleSearchCancelled;
        void OnDisable() => MatchmakingManager.OnSearchCancelled -= HandleSearchCancelled;

        private void HandleSearchCancelled()
        {
            _isSearching = false;
            SetButtonsInteractable(true);
        }

        void Update()
        {
            if (_isSearching || buttons.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentIndex = (currentIndex + 1) % buttons.Count;
                ApplySelection();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentIndex = (currentIndex - 1 + buttons.Count) % buttons.Count;
                ApplySelection();
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                Confirm(buttons[currentIndex]);
            }
            else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        public void Select(ModeButton btn)
        {
            int i = buttons.IndexOf(btn);
            if (i < 0) return;
            currentIndex = i;
            ApplySelection();
        }

        public void Confirm(ModeButton btn)
        {
            Select(btn);
            Debug.Log($"[ModeMenu] Confirmed: {btn.mode}");
            OnModeConfirmed?.Invoke(btn.mode);

            switch (btn.mode)
            {
                case ModeButton.Mode.Ranked:
                    var mm = MatchmakingManager.Instance;
                    if (mm != null)
                    {
                        Debug.Log("[ModeMenu] Bắt đầu tìm trận Ranked qua MatchmakingManager...");
                        _isSearching = true;
                        SetButtonsInteractable(false);
                        mm.StartSearching();
                    }
                    else
                    {
                        Debug.LogWarning("[ModeMenu] Không tìm thấy MatchmakingManager, load trực tiếp.");
                        SceneManager.LoadScene("Battle scene");
                    }
                    break;
                case ModeButton.Mode.Casual:
                    var mmCasual = MatchmakingManager.Instance;
                    if (mmCasual != null)
                    {
                        _isSearching = true;
                        SetButtonsInteractable(false);
                        mmCasual.StartSearchingCasual();
                    }
                    else
                    {
                        SceneManager.LoadScene("Battle scene");
                    }
                    break;
                case ModeButton.Mode.Private:
                    // Tự tìm panel trong scene nếu chưa gán ở Inspector
                    // (ModeSceneAutoSetup dựng panel này lúc runtime).
                    if (privateRoomPanel == null)
                        privateRoomPanel = FindFirstObjectByType<PrivateRoomPanel>(FindObjectsInactive.Include);

                    if (privateRoomPanel != null)
                        privateRoomPanel.Show();
                    else
                        Debug.LogWarning("[ModeMenu] Không tìm thấy PrivateRoomPanel trong scene.");
                    break;
            }
        }

        private void SetButtonsInteractable(bool on)
        {
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var btn = b.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = on;
            }
        }

        void ApplySelection()
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i] != null)
                    buttons[i].SetSelected(i == currentIndex);
        }
    }
}
