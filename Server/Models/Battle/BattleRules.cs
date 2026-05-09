namespace PokemonMMO.Models;

public static class BattleRules
{
    public const int MaxPartySize = 6;
    public const int DefaultBattleLevel = 50;

    // Damage random modifier
    public const double DamageRandomMin = 0.85;
    public const double DamageRandomMax = 1.00;

    // Optional: timeout nếu sau này muốn auto action
    public const int TurnTimeoutSeconds = 30;
}