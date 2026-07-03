namespace PokemonMMO.Options;

/// <summary>
/// Runtime-tunable battle settings loaded from configuration.
/// </summary>
public class BattleOptions
{
    public int MaxPartySize { get; set; } = 6;
    public int DefaultBattleLevel { get; set; } = 50;

    public double DamageRandomMin { get; set; } = 0.85;
    public double DamageRandomMax { get; set; } = 1.00;

    public int TurnTimeoutSeconds { get; set; } = 30;
    public int SwitchActionPriority { get; set; } = 6;

    public int WinnerMmrGain { get; set; } = 25;
    public int LoserMmrLoss { get; set; } = 20;
    public int WinnerVpGain { get; set; } = 10;
    public int BotFallbackSeconds { get; set; } = 20;

    // Thời gian ân hạn khi một người chơi mất kết nối giữa trận: đối thủ thấy đếm ngược,
    // nếu không vào lại kịp trong khoảng này thì xử thua người mất kết nối.
    public int DisconnectGraceSeconds { get; set; } = 30;
}
