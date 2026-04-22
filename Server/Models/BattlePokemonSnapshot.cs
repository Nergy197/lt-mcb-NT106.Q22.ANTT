namespace PokemonMMO.Models;

public class BattlePokemonSnapshot
{
    public string InstanceId { get; set; } = null!;
    public int SpeciesId { get; set; }
    public string SpeciesName { get; set; } = "";
    public string Nickname { get; set; } = "";

    public int Level { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    
    // Core Calculated Stats (IV + EV + Base + Nature)
    public int Atk { get; set; }
    public int Def { get; set; }
    public int SpAtk { get; set; }
    public int SpDef { get; set; }
    public int Spd { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────
    /// <summary>Non-volatile status. Replaces the old string field.</summary>
    public PokemonStatusCondition NonVolatileStatus { get; set; } = PokemonStatusCondition.None;

    /// <summary>Turns remaining while asleep (randomised 1–3 on inflict).</summary>
    public int SleepTurnsLeft { get; set; } = 0;

    /// <summary>
    /// Toxic counter: how many turns this pokemon has been badly poisoned.
    /// Damage = ToxicCounter/16 * MaxHp (capped at 15/16).
    /// </summary>
    public int ToxicCounter { get; set; } = 0;

    // ── Volatile status ───────────────────────────────────────────────────────
    public bool IsConfused { get; set; } = false;
    public int ConfusionTurnsLeft { get; set; } = 0;

    // ── Stat stages (-6 to +6) ─────────────────────────────────────────────
    /// <summary>
    /// Stat stage modifiers indexed by StatIndex enum.
    /// Order: ATK=0, DEF=1, SPA=2, SPD=3, SPE=4, ACC=5, EVA=6.
    /// Mirrors pbs-unity's stage system.
    /// </summary>
    public int[] StatStages { get; set; } = new int[7];

    public List<PokemonMove> Moves { get; set; } = new();

    public bool IsFainted => CurrentHp <= 0;

    /// <summary>Returns the stat stage for the given stat.</summary>
    public int GetStage(StatIndex stat) => StatStages[(int)stat];

    /// <summary>
    /// Applies stat multiplier from stage. Gen 3+ formula:
    /// positive: (2+n)/2, negative: 2/(2-n).
    /// </summary>
    public double GetStageMultiplier(StatIndex stat)
    {
        var stage = Math.Clamp(StatStages[(int)stat], -6, 6);
        return stage >= 0
            ? (2.0 + stage) / 2.0
            : 2.0 / (2.0 - stage);
    }
}
