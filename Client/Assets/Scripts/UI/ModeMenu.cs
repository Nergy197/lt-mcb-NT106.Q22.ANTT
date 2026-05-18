using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public System.Action<ModeButton.Mode> OnModeConfirmed;

        int currentIndex;

        void Start()
        {
            if (buttons.Count == 0) return;
            currentIndex = Mathf.Clamp(defaultIndex, 0, buttons.Count - 1);
            ApplySelection();
        }

        void Update()
        {
            if (buttons.Count == 0) return;

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
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Battle scene");
                    break;
                case ModeButton.Mode.Casual:
                    Debug.LogWarning("[ModeMenu] Casual mode chưa có scene.");
                    break;
                case ModeButton.Mode.Private:
                    Debug.LogWarning("[ModeMenu] Private mode chưa có scene.");
                    break;
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
