namespace PokemonMMO.Models;

public class BattlePokemonSnapshot
{
    public string InstanceId { get; set; } = null!;
    public int SpeciesId { get; set; }
    public string Nickname { get; set; } = "";

    public int Level { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public string StatusCondition { get; set; } = "NONE";

    public List<PokemonMove> Moves { get; set; } = new();

    public bool IsFainted => CurrentHp <= 0;
}