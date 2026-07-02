using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace PokemonArena.UI
{
    /// <summary>
    /// A single battle-mode button. Lives on the root GameObject of each
    /// ModeButton prefab. Hover or click selects it (and tells the parent
    /// ModeMenu to deselect siblings).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ModeButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public enum Mode { Ranked, Casual, Private }

        [Header("Identity")]
        public Mode mode = Mode.Ranked;

        [Header("Sprites")]
        public Sprite normalSprite;     // btn_purple
        public Sprite selectedSprite;   // btn_green

        [Header("Child references")]
        public Image background;
        public GameObject arrow;
        public TextMeshProUGUI label;

        [Header("Label colors")]
        public Color normalLabelColor = Color.white;
        public Color selectedLabelColor = new Color(0.10f, 0.23f, 0.05f);

        [Header("Hover slide (px)")]
        public float restX = -60f;
        public float hoverX = -72f;
        public float lerpSpeed = 16f;

        ModeMenu menu;
        RectTransform rt;
        float targetX;

        void Awake()
        {
            rt = (RectTransform)transform;
            menu = GetComponentInParent<ModeMenu>();
            targetX = restX;
        }

        void Update()
        {
            // Smooth slide
            var p = rt.anchoredPosition;
            p.x = Mathf.Lerp(p.x, targetX, Time.unscaledDeltaTime * lerpSpeed);
            rt.anchoredPosition = p;
        }

        public void SetSelected(bool on)
        {
            if (background != null) background.sprite = on ? selectedSprite : normalSprite;
            if (label != null)      label.color       = on ? selectedLabelColor : normalLabelColor;
            if (arrow != null)      arrow.SetActive(on);
            targetX = on ? hoverX : restX;
        }

        // ── Chuột ─────────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData) => menu?.Select(this);

        public void OnPointerClick(PointerEventData eventData) => menu?.Confirm(this);
    }
}
