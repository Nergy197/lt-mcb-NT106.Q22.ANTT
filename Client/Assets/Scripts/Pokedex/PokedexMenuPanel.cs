using UnityEngine;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Chỉ quản lý cursor và logic menu.
    /// KHÔNG tự xử lý input — PokedexSceneController gọi vào.
    /// </summary>
    public class PokedexMenuPanel : MonoBehaviour
    {
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

        // ── Gọi từ PokedexSceneController ────────────────────────────────

        public void NavigateDir(int dir)
        {
            _selectedIndex = (_selectedIndex + dir + 2) % 2;
            MoveCursorTo(_selectedIndex);
        }

        public void Confirm(PokedexSceneController ctrl)
        {
            if (_selectedIndex == 0) ctrl.OpenNationalPokedex();
            else                     ctrl.ExitToMenu();
        }

        // ── Cursor ────────────────────────────────────────────────────────

        private void MoveCursorTo(int index)
        {
            if (cursorImage == null) return;
            var target = index == 0 ? menuItem0 : menuItem1;
            if (target == null) return;
            cursorImage.anchoredPosition = new Vector2(
                cursorImage.anchoredPosition.x,
                target.anchoredPosition.y);
        }
    }
}
