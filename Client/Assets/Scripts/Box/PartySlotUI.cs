using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    public class PartySlotUI : MonoBehaviour
    {
        public Image pokemonImage;

        private string _pokemonId = null;
        private bool   _isEmpty   = true;

        public bool   IsEmpty   => _isEmpty;
        public string PokemonId => _pokemonId;

        public void SetEmpty()
        {
            _isEmpty   = true;
            _pokemonId = null;
            if (pokemonImage == null) return;
            pokemonImage.sprite = null;
            pokemonImage.color  = Color.clear;
        }

        public void SetPokemon(string pokemonId, Sprite sprite)
        {
            _isEmpty   = false;
            _pokemonId = pokemonId;
            if (pokemonImage == null) return;
            pokemonImage.sprite         = sprite;
            pokemonImage.color          = Color.white;
            pokemonImage.preserveAspect = true;
        }
    }
}
