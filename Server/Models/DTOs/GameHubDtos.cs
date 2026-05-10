using PokemonMMO.Models;

namespace PokemonMMO.Models.DTOs;

public class PlayerJoinedEventDto
{
    public string SessionId { get; set; } = null!;
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class PlayerLeftEventDto
{
    public string SessionId { get; set; } = null!;
}

public class PartyPokemonDto
{
    public string Id { get; set; } = null!;
    public int SpeciesId { get; set; }
    public int Level { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
}

public class BattleStartedEventDto
{
    public string BattleId { get; set; } = null!;
    public string Player1Id { get; set; } = null!;
    public string Player2Id { get; set; } = null!;
    public int TurnNumber { get; set; }
    public int TurnTimeoutSeconds { get; set; }
    public DateTime TurnDeadlineUtc { get; set; }
    public string State { get; set; } = null!;
    public int ActiveIndex1 { get; set; }
    public int ActiveIndex2 { get; set; }
}

public class ActionAcceptedEventDto
{
    public string BattleId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public int? MoveSlot { get; set; }
    public int? PartyIndex { get; set; }
}

public class TurnWaitingEventDto
{
    public string BattleId { get; set; } = null!;
    public int TurnNumber { get; set; }
    public bool Ready { get; set; }
    public DateTime TurnDeadlineUtc { get; set; }
    public List<string> SubmittedPlayerIds { get; set; } = new();
}

public class BattleUpdatedEventDto
{
    public string BattleId { get; set; } = null!;
    public int NextTurnNumber { get; set; }
    public DateTime? TurnDeadlineUtc { get; set; }
    public int ActiveIndex1 { get; set; }
    public int ActiveIndex2 { get; set; }
    public int ActiveHp1 { get; set; }
    public int ActiveHp2 { get; set; }
}

public class BattleEndedEventDto
{
    public string BattleId { get; set; } = null!;
    public string? WinnerPlayerId { get; set; }
    public List<string> Events { get; set; } = new();
    public List<BattleEvent> TypedEvents { get; set; } = new();
}

public class VPChangedDto
{
    public int Vp { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = "";
}

// ─── VGC Team Preview ───────────────────────────────────────────────────────

public class TeamPreviewDto
{
    public string BattleId         { get; set; } = null!;
    public string YourPlayerId     { get; set; } = null!;
    public string OpponentPlayerId { get; set; } = null!;
    public List<TeamPreviewPokemonDto> YourTeam     { get; set; } = new();
    public List<TeamPreviewPokemonDto> OpponentTeam { get; set; } = new();
}

public class TeamPreviewPokemonDto
{
    public int     SpeciesId   { get; set; }
    public string  SpeciesName { get; set; } = "";
    public string  Nickname    { get; set; } = "";
    public string  Type1       { get; set; } = "normal";
    public string? Type2       { get; set; }
    public int     Level       { get; set; }
    public int     MaxHp       { get; set; }
}

// ─── VGC Battle Running State ────────────────────────────────────────────────

public class BattleRunningDto
{
    public string   BattleId         { get; set; } = null!;
    public string?  OpponentId       { get; set; } // ID của đối thủ (BOT_PLAYER hoặc playerId thật)
    public int      TurnNumber       { get; set; }
    public DateTime TurnDeadlineUtc  { get; set; }
    public WeatherCondition Weather  { get; set; }
    public int      WeatherTurnsLeft { get; set; }
    public TerrainCondition Terrain  { get; set; }
    public int      TerrainTurnsLeft { get; set; }

    // Active field (2 yours + 2 opponent)
    public FieldPokemonDto? YourSlotA { get; set; }
    public FieldPokemonDto? YourSlotB { get; set; }
    public FieldPokemonDto? OppSlotA  { get; set; }
    public FieldPokemonDto? OppSlotB  { get; set; }

    // Team panel (all 4 brought Pokemon)
    public List<int> YourTeamHp    { get; set; } = new();
    public List<int> YourTeamMaxHp { get; set; } = new();
}

public class FieldPokemonDto
{
    public int     SpeciesId        { get; set; }
    public string  SpeciesName      { get; set; } = "";
    public string  Nickname         { get; set; } = "";
    // Current types (may differ when Terastallized)
    public string  Type1            { get; set; } = "normal";
    public string? Type2            { get; set; }
    // Original types
    public string  OrigType1        { get; set; } = "normal";
    public string? OrigType2        { get; set; }
    // Gen 9 Tera
    public string  TerType          { get; set; } = "normal";
    public bool    IsTerastallized  { get; set; }
    public int     Level            { get; set; }
    public int     CurrentHp        { get; set; }
    public int     MaxHp            { get; set; }
    public bool    IsFainted        { get; set; }
    public string? Status           { get; set; }
    public int[]   StatStages       { get; set; } = new int[7];
    // Moves only sent for the player's own Pokemon
    public List<MoveSummaryDto> Moves { get; set; } = new();
}

public class MoveSummaryDto
{
    public int    MoveId    { get; set; }
    public string Name      { get; set; } = "";
    public string Type      { get; set; } = "";
    public string Category  { get; set; } = "";
    public int    CurrentPp { get; set; }
    public int    MaxPp     { get; set; }
    public int    TargetType{ get; set; }
    public string? Effect   { get; set; }
    public List<MoveStatChangeDto> StatChanges { get; set; } = new();
}

public class MoveStatChangeDto
{
    public string Stat { get; set; } = "";
    public int Stages { get; set; }
}

public class SearchStartedDto
{
    public int CountdownSeconds { get; set; }
}

public class SearchTickDto
{
    public int SecondsLeft { get; set; }
}

public class ForcedSwitchRequiredDto
{
    public string BattleId { get; set; } = null!;
    public string PlayerId { get; set; } = null!;
    public int Slot { get; set; }
    public List<int> AvailableIndices { get; set; } = new();
}

public class ForcedSwitchAcceptedDto
{
    public string BattleId { get; set; } = null!;
    public string PlayerId { get; set; } = null!;
    public int Slot { get; set; }
    public int NewPartyIndex { get; set; }
}
