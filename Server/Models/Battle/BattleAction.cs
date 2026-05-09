namespace PokemonMMO.Models;

public enum MoveTargetType
{
    SingleOpponent,  // Mặc định: chọn 1 đối thủ
    SpreadOpponents, // Đòn đánh lan: tất cả đối thủ (75% dmg)
    SingleAlly,      // Nhắm vào đồng đội
    AllExceptUser,   // Toàn bộ sân (trừ bản thân)
    Self,            // Tự thân
    RandomOpponent   // Ngẫu nhiên 1 đối thủ
}

public class BattleAction
{
    public string PlayerId { get; set; } = null!;
    public BattleActionType Type { get; set; }

    // Dùng khi Type = Move (0..3)
    public int? MoveSlot { get; set; }

    // Dùng khi Type = Switch (0..5)
    public int? SwitchIndex { get; set; }

    // ── Các trường cho Đấu Đôi (2v2) ──
    
    // Vị trí Pokemon ra chiêu (0: Slot A, 1: Slot B)
    public int SourceIndex { get; set; } = 0; 
    
    // Vị trí mục tiêu:
    // 0: Slot A đối thủ, 1: Slot B đối thủ
    // 2: Slot A phe ta,  3: Slot B phe ta
    public int TargetSlot { get; set; } = 0;

    /// <summary>Gen 9: Player kích hoạt Terastallization cùng lượt này không?</summary>
    public bool UseTera { get; set; } = false;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}