using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.Pokedex
{
    public class PokedexItemUI : MonoBehaviour
    {
        public Image highlightBorder;
        public TMP_Text nameLabel;

        private int _id;
        private Action<int> _onClicked;

        public void SetData(int id, string displayName, Action<int> onClicked)
        {
            _id      = id;
            _onClicked = onClicked;
            nameLabel.text = $"{id:000} {displayName}";
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (highlightBorder != null)
                highlightBorder.enabled = selected;
        }

        // Gán vào Button.onClick trong prefab
        public void OnClick() => _onClicked?.Invoke(_id);
    }
}
