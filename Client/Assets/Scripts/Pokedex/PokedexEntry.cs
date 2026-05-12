using System.Collections.Generic;

namespace PokemonMMO.Pokedex
{
    [System.Serializable]
    public class PokedexEntry
    {
        public int id;
        public string name;
        public List<string> types;
        public string sprite_url;

        public string DisplayName =>
            string.IsNullOrEmpty(name) ? "" : char.ToUpper(name[0]) + name.Substring(1);

        public string DisplayId => id.ToString("000");
    }
}
