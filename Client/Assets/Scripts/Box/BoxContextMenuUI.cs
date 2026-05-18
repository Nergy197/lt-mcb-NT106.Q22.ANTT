using System;
using TMPro;
using UnityEngine;

namespace PokemonMMO.Box
{
    public class BoxContextMenuUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [Header("Menu items (top → bottom)")]
        public TMP_Text option1Text;
        public TMP_Text option2Text;

        [Header("Cursor")]
        public RectTransform menuCursor;

        private int    _selected;
        private Action<int> _onConfirm;

        private void Awake() => gameObject.SetActive(false);

        public void Show(string opt1, string opt2, Action<int> onConfirm)
        {
            option1Text.text = opt1;
            option2Text.text = opt2;
            _onConfirm  = onConfirm;
            _selected   = 0;
            gameObject.SetActive(true);
            IsOpen = true;
            MoveCursor();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            IsOpen = false;
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W))
                { _selected = 0; MoveCursor(); }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                { _selected = 1; MoveCursor(); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.C))
                Confirm();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X))
                Hide();
        }

        private void Confirm()
        {
            int idx = _selected;
            Hide();
            _onConfirm?.Invoke(idx);
        }

        private void MoveCursor()
        {
            if (menuCursor == null) return;
            var target = _selected == 0 ? option1Text : option2Text;
            if (target == null) return;

            var corners = new Vector3[4];
            target.GetComponent<RectTransform>().GetWorldCorners(corners);
            // corners[0]=bottom-left, corners[1]=top-left, corners[2]=top-right, corners[3]=bottom-right
            menuCursor.position = new Vector3(corners[1].x, corners[1].y, 0);
        }
    }
}
