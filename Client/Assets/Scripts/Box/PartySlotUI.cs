using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    public class PartySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public Image pokemonImage;

        private string _pokemonId = null;
        private bool   _isEmpty   = true;
        private GameObject _trialBadge;

        public bool   IsEmpty   => _isEmpty;
        public string PokemonId => _pokemonId;
        public bool   IsTrial   { get; private set; }

        public void SetEmpty()
        {
            _isEmpty   = true;
            _pokemonId = null;
            if (pokemonImage != null)
            {
                pokemonImage.sprite = null;
                pokemonImage.color  = Color.clear;
            }
            if (_trialBadge != null) _trialBadge.SetActive(false);
        }

        public void SetPokemon(string pokemonId, Sprite sprite)
        {
            _isEmpty   = false;
            _pokemonId = pokemonId;
            if (pokemonImage != null)
            {
                pokemonImage.sprite         = sprite;
                pokemonImage.color          = Color.white;
                pokemonImage.preserveAspect = true;
            }
        }

        public void SetTrialBadge(bool isTrial)
        {
            IsTrial = isTrial;
            TrialBadge.Show(ref _trialBadge, transform, isTrial);
        }

        // ── Kéo-thả (Party → Box = deposit) ──────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isEmpty) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            BoxDragManager.Begin(pokemonImage.sprite, _pokemonId, fromBox: false, eventData, canvas);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (BoxDragManager.IsDragging) BoxDragManager.UpdatePosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => BoxDragManager.End();

        // ── Thả vào đây (Box → Party = withdraw) ─────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            if (!BoxDragManager.IsDragging || !BoxDragManager.FromBox) return;
            string pokemonId = BoxDragManager.PokemonId;
            BoxDragManager.End();
            PokemonBoxPanel.Instance?.RequestWithdraw(pokemonId);
        }
    }
}
