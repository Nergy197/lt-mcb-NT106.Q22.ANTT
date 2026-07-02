using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    /// <summary>
    /// Quản lý trạng thái kéo-thả Pokemon giữa lưới Box và lưới Party.
    /// Chỉ hỗ trợ kéo Box↔Party (tái dùng API deposit/withdraw có sẵn) —
    /// không hỗ trợ đổi vị trí trong cùng 1 lưới vì server chưa có API nhận vị trí đích.
    /// </summary>
    public static class BoxDragManager
    {
        public static bool   IsDragging { get; private set; }
        public static bool   FromBox    { get; private set; }
        public static string PokemonId  { get; private set; }

        private static GameObject _ghost;
        private static Image      _ghostImage;

        public static void Begin(Sprite sprite, string pokemonId, bool fromBox, PointerEventData eventData, Canvas canvas)
        {
            IsDragging = true;
            FromBox    = fromBox;
            PokemonId  = pokemonId;
            EnsureGhost(canvas);
            _ghostImage.sprite = sprite;
            _ghostImage.color  = Color.white;
            _ghost.SetActive(true);
            UpdatePosition(eventData);
        }

        public static void UpdatePosition(PointerEventData eventData)
        {
            if (_ghost == null || !_ghost.activeSelf) return;
            _ghost.transform.position = eventData.position;
        }

        public static void End()
        {
            IsDragging = false;
            PokemonId  = null;
            if (_ghost != null) _ghost.SetActive(false);
        }

        private static void EnsureGhost(Canvas canvas)
        {
            if (_ghost != null) return;

            _ghost = new GameObject("BoxDragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _ghost.transform.SetParent(canvas.transform, false);
            _ghost.transform.SetAsLastSibling();

            var rect = _ghost.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(64, 64);

            _ghostImage = _ghost.GetComponent<Image>();
            _ghostImage.preserveAspect = true;
            _ghostImage.raycastTarget  = false; // không được chặn OnDrop của ô bên dưới con trỏ

            var group = _ghost.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            _ghost.SetActive(false);
        }
    }
}
