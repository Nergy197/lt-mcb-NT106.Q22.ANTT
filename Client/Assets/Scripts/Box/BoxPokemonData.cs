using System.Collections.Generic;

namespace PokemonMMO.Box
{
    [System.Serializable]
    public class BoxSlotData
    {
        public int    Slot;
        public string PokemonId;
        public int    SpeciesId;
        public string Nickname;
        public int    Level;
        public string IconUrl;
    }

    [System.Serializable]
    public class BoxInfoData
    {
        public int               BoxIndex;
        public int               TotalBoxes;
        public string            BoxName;
        public List<BoxSlotData> Slots;
    }

    [System.Serializable]
    public class PartySlotData
    {
        public int    Slot;
        public string PokemonId;
        public int    SpeciesId;
        public string Nickname;
        public int    Level;
        public int    CurrentHp;
        public int    MaxHp;
    }

    [System.Serializable]
    public class PartyInfoData
    {
        public List<PartySlotData> Slots;
    }
}
