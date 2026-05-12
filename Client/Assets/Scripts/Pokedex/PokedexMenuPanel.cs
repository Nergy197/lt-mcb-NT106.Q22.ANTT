using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonMMO.UI
{
    public class PokedexMenuPanel : MonoBehaviour
    {
        [Header("Scene Names")]
        public string menuSceneName = "Menu scene";

        [Header("Panels")]
        public GameObject nationalPokedexPanel;

        [Header("Cursor")]
        public RectTransform cursorImage;
        public RectTransform menuItem0;   // National Pokédex
        public RectTransform menuItem1;   // Thoát

        private int _selectedIndex = 0;

        private void OnEnable()
        {
            _selectedIndex = 0;
            MoveCursorTo(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                Navigate(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                Navigate(-1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z))
                Confirm();
        }

        private void Navigate(int dir)
        {
            _selectedIndex = (_selectedIndex + dir + 2) % 2;
            MoveCursorTo(_selectedIndex);
        }

        private void MoveCursorTo(int index)
        {
            if (cursorImage == null) return;
            RectTransform target = index == 0 ? menuItem0 : menuItem1;
            if (target == null) return;
            cursorImage.anchoredPosition = new Vector2(
                cursorImage.anchoredPosition.x,
                target.anchoredPosition.y
            );
        }

        private void Confirm()
        {
            if (_selectedIndex == 0) OnNationalPokedexClicked();
            else OnExitClicked();
        }

        public void OnNationalPokedexClicked()
        {
            if (nationalPokedexPanel != null)
            {
                gameObject.SetActive(false);
                nationalPokedexPanel.SetActive(true);
            }
        }

        public void OnExitClicked()
        {
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
