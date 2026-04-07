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
}
