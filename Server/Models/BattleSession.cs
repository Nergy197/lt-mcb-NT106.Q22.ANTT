using System.Collections.Concurrent;

namespace PokemonMMO.Models;

public class BattleSession
{
    public string BattleId { get; set; } = Guid.NewGuid().ToString("N");
    public BattleState State { get; set; } = BattleState.Waiting;
    public int TurnNumber { get; set; } = 1;

    public string Player1Id { get; set; } = null!;
    public string Player2Id { get; set; } = null!;

    public List<BattlePokemonSnapshot> Team1 { get; set; } = new();
    public List<BattlePokemonSnapshot> Team2 { get; set; } = new();

    public int ActiveIndex1 { get; set; } = 0;
    public int ActiveIndex2 { get; set; } = 0;

    // Key = playerId
    public ConcurrentDictionary<string, BattleAction> PendingActions { get; set; } = new();

    public string? WinnerPlayerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}