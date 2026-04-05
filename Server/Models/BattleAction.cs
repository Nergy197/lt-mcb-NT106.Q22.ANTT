namespace PokemonMMO.Models;

public class BattleAction
{
    public string PlayerId { get; set; } = null!;
    public BattleActionType Type { get; set; }

    // Dùng khi Type = Move (0..3)
    public int? MoveSlot { get; set; }

    // Dùng khi Type = Switch (0..5)
    public int? SwitchIndex { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}