using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Overlay trong suốt phủ toàn màn hình (gắn vào Canvas gốc), đặt ngay phía sau
    /// nhánh chứa popup, để bấm ra ngoài popup là đóng popup.
    /// Dùng chung cho các popup dạng modal (Rank, Friends, Mail, ...).
    /// </summary>
    public static class ClickOutsideOverlay
    {
        public static void Show(ref GameObject overlay, GameObject popup, UnityAction onClickOutside)
        {
            if (popup == null) return;

            var canvas = popup.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRoot = canvas.transform;

            if (overlay == null)
                overlay = Create(canvasRoot, onClickOutside);

            // Tìm nhánh con trực tiếp của Canvas chứa popup, đặt overlay ngay trước nhánh đó
            // để overlay nằm dưới popup nhưng trên mọi thứ khác trong Canvas.
            var topLevelAncestor = popup.transform;
            while (topLevelAncestor.parent != null && topLevelAncestor.parent != canvasRoot)
                topLevelAncestor = topLevelAncestor.parent;

            if (topLevelAncestor.parent == canvasRoot)
                overlay.transform.SetSiblingIndex(topLevelAncestor.GetSiblingIndex());
            else
                overlay.transform.SetAsLastSibling();

            overlay.SetActive(true);
        }

        public static void Hide(GameObject overlay)
        {
            if (overlay != null) overlay.SetActive(false);
        }

        private static GameObject Create(Transform canvasRoot, UnityAction onClickOutside)
        {
            var go = new GameObject("ClickOutsideOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvasRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // vô hình nhưng vẫn chặn raycast

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClickOutside);

            return go;
        }
    }
}
