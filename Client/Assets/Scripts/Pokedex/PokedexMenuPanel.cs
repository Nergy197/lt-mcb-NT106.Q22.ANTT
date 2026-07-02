using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Chỉ quản lý cursor và logic menu.
    /// KHÔNG tự xử lý input — PokedexSceneController gọi vào (bàn phím và click chuột).
    /// </summary>
    public class PokedexMenuPanel : MonoBehaviour
    {
        [Header("Cursor")]
        public RectTransform cursorImage;
        public RectTransform menuItem0;   // National Pokédex
        public RectTransform menuItem1;   // Thoát

        // Bắn ra khi người chơi click chuột vào 1 mục menu (0 hoặc 1)
        public event System.Action<int> OnItemClicked;

        private int _selectedIndex = 0;

        private void Awake()
        {
            AddClickHandler(menuItem0, 0);
            AddClickHandler(menuItem1, 1);
        }

        private void AddClickHandler(RectTransform rt, int index)
        {
            if (rt == null) return;
            var btn = rt.GetComponent<Button>();
            if (btn == null) btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => OnItemClicked?.Invoke(index));
        }

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

        // Click chuột: chọn mục rồi xác nhận ngay (giống nhấn C sau khi di chuyển cursor tới đó)
        public void SelectAndConfirm(int index, PokedexSceneController ctrl)
        {
            _selectedIndex = index;
            MoveCursorTo(index);
            Confirm(ctrl);
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
