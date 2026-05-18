using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    public class BoxSlotUI : MonoBehaviour
    {
        public Image pokemonImage;

        private int    _speciesId = -1;
        private string _pokemonId = null;
        private bool   _isEmpty   = true;

        public bool   IsEmpty   => _isEmpty;
        public int    SpeciesId => _speciesId;
        public string PokemonId => _pokemonId;

        public void SetEmpty()
        {
            _isEmpty   = true;
            _speciesId = -1;
            _pokemonId = null;
            if (pokemonImage != null)
            {
                pokemonImage.sprite = null;
                pokemonImage.color  = Color.clear;
            }
        }

        public void SetPokemon(int speciesId, string pokemonId, Sprite sprite)
        {
            _isEmpty   = false;
            _speciesId = speciesId;
            _pokemonId = pokemonId;
            if (pokemonImage != null)
            {
                pokemonImage.sprite         = sprite;
                pokemonImage.color          = Color.white;
                pokemonImage.preserveAspect = true;
            }
        }
    }
}
