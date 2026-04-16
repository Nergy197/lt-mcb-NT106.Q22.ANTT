namespace PokemonMMO.Models;

public class BattleAction
{
    public string PlayerId { get; set; } = null!;
    public BattleActionType Type { get; set; }

    // Dùng khi Type = Move (0..3)
    public int? MoveSlot { get; set; }

    // Dùng khi Type = Switch (0..5)
    public int? SwitchIndex { get; set; }

    // Thêm các trường cho Đấu Đôi (2v2)
    public int SourceIndex { get; set; } = 0; // 0: Slot A, 1: Slot B
    public int TargetSlot { get; set; } = 0;  // 0: Đối thủ Slot A, 1: Đối thủ Slot B

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}