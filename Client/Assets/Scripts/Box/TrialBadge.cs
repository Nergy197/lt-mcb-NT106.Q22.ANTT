using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    /// <summary>
    /// Nhãn nhỏ "TRIAL" đè lên góc icon Pokémon, báo cho người chơi biết Pokémon này
    /// đang ở dạng dùng thử (IsTrial = true trên server) — nếu gửi vào Box nó sẽ luôn
    /// rơi vào Trial Box, không phải box thường đang xem.
    /// </summary>
    public static class TrialBadge
    {
        public static void Show(ref GameObject badge, Transform parent, bool visible)
        {
            if (badge == null)
            {
                if (!visible) return;
                badge = Create(parent);
            }
            badge.SetActive(visible);
        }

        private static GameObject Create(Transform parent)
        {
            var go = new GameObject("TrialBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot     = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(2f, 2f);
            rect.sizeDelta = new Vector2(38f, 14f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.85f, 0.15f, 0.15f, 0.9f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.text = "TRIAL";
            text.fontSize = 9f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return go;
        }
    }
}
