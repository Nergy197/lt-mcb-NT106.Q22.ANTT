using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    public class OptionsPopupController : MonoBehaviour
    {
        [Header("Các nút trong popup (theo thứ tự trên xuống)")]
        public Button[] buttons;

        [Header("Viền highlight")]
        public Color borderColor = Color.yellow;
        public float borderSize  = 6f;

        private int           _selectedIndex = 0;
        private RectTransform _borderRt;
        private GameObject    _border;

        private void Awake()
        {
            // Container render trên cùng, phần giữa trong suốt — 4 dải con tạo viền
            _border = new GameObject("_SelectBorder");
            _border.transform.SetParent(transform, false);
            _border.transform.SetAsLastSibling();

            // Image trong suốt để Unity sinh RectTransform
            var bg = _border.AddComponent<Image>();
            bg.color         = Color.clear;
            bg.raycastTarget = false;

            _borderRt = _border.GetComponent<RectTransform>();

            // 4 dải viền nằm sát cạnh TRONG của container (container lớn hơn button borderSize mỗi phía)
            MakeLine("Top",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, borderSize), new Vector2(0, -borderSize / 2));
            MakeLine("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, borderSize), new Vector2(0,  borderSize / 2));
            MakeLine("Left",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(borderSize, 0), new Vector2( borderSize / 2, 0));
            MakeLine("Right",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(borderSize, 0), new Vector2(-borderSize / 2, 0));

            _border.SetActive(false);
        }

        private void MakeLine(string lineName, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            var go  = new GameObject(lineName);
            go.transform.SetParent(_border.transform, false);
            var img = go.AddComponent<Image>();
            img.color         = borderColor;
            img.raycastTarget = false;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.sizeDelta        = sizeDelta;
            rt.anchoredPosition = anchoredPos;
        }

        private void OnEnable()
        {
            _selectedIndex = 0;
            UpdateHighlight();
        }

        private void OnDisable()
        {
            if (_border != null) _border.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
                UpdateHighlight();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _selectedIndex = Mathf.Min(buttons.Length - 1, _selectedIndex + 1);
                UpdateHighlight();
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                buttons[_selectedIndex]?.onClick.Invoke();
            }
            else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
            {
                gameObject.SetActive(false);
            }
        }

        private void UpdateHighlight()
        {
            if (_selectedIndex < 0 || _selectedIndex >= buttons.Length)
            {
                _border.SetActive(false);
                return;
            }

            var targetRt = buttons[_selectedIndex].GetComponent<RectTransform>();
            _borderRt.anchorMin        = targetRt.anchorMin;
            _borderRt.anchorMax        = targetRt.anchorMax;
            _borderRt.anchoredPosition = targetRt.anchoredPosition;
            // Container lớn hơn button borderSize mỗi phía → viền bao trọn button
            _borderRt.sizeDelta        = targetRt.sizeDelta + new Vector2(borderSize * 2, borderSize * 2);
            _border.SetActive(true);
        }
    }
}
