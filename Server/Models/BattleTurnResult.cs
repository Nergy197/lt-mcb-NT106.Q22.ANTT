namespace PokemonMMO.Models;

public class BattleTurnResult
{
    public string BattleId { get; set; } = null!;
    public int ResolvedTurnNumber { get; set; }
    public int NextTurnNumber { get; set; }
    public BattleState State { get; set; }
    public string? WinnerPlayerId { get; set; }

    public int ActiveIndex1 { get; set; }
    public int ActiveIndex2 { get; set; }
    public int ActiveHp1 { get; set; }
    public int ActiveHp2 { get; set; }

    public List<string> Events { get; set; } = new();
}
