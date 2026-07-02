namespace PokemonMMO.Models.DTOs;

public class BoxSlotDto
{
    public int    Slot      { get; set; }
    public string PokemonId { get; set; } = "";
    public int    SpeciesId { get; set; }
    public string Nickname  { get; set; } = "";
    public int    Level     { get; set; }
    public string IconUrl   { get; set; } = "";
    public bool   IsTrial   { get; set; }
}

public class BoxInfoDto
{
    public int              BoxIndex   { get; set; }
    public int              TotalBoxes { get; set; }
    public string           BoxName    { get; set; } = "";
    public List<BoxSlotDto> Slots      { get; set; } = new();
}

public class RecruitConfirmRequest
{
    public int    SpeciesId   { get; set; }
    public string RecruitType { get; set; } = "trial"; // "trial" | "permanent"
}

public class RecruitConfirmResponse
{
    public bool   Success   { get; set; }
    public string Message   { get; set; } = "";
    public bool   IsInParty { get; set; }
    public string PokemonId { get; set; } = "";
}

public class PartySlotDto
{
    public int    Slot      { get; set; }
    public string PokemonId { get; set; } = "";
    public int    SpeciesId { get; set; }
    public string Nickname  { get; set; } = "";
    public int    Level     { get; set; }
    public int    CurrentHp { get; set; }
    public int    MaxHp     { get; set; }
    public string IconUrl   { get; set; } = "";
    public bool   IsTrial   { get; set; }
}

public class PartyInfoDto
{
    public List<PartySlotDto> Slots { get; set; } = new();
}

public class BoxActionRequest
{
    public string PokemonId { get; set; } = "";
}
