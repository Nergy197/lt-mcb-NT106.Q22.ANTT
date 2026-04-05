using System.Collections.Concurrent;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

public class BattleService
{
    private readonly MongoDbContext _db;
    private static readonly ConcurrentDictionary<string, BattleSession> _battles = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _battleLocks = new();
    private static readonly Random _rng = new();

    public BattleService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<BattleSession> CreateBattle(string player1Id, string player2Id)
    {
        if (player1Id == player2Id)
            throw new Exception("A player cannot battle themselves.");

        var team1 = await LoadParty(player1Id);
        var team2 = await LoadParty(player2Id);

        if (team1.Count == 0 || team2.Count == 0)
            throw new Exception("Both players must have at least 1 Pokemon in party.");

        var snapshots1 = ToSnapshots(team1);
        var snapshots2 = ToSnapshots(team2);
        var session = new BattleSession
        {
            BattleId = Guid.NewGuid().ToString("N"),
            State = BattleState.Running,
            TurnNumber = 1,
            Player1Id = player1Id,
            Player2Id = player2Id,
            Team1 = snapshots1,
            Team2 = snapshots2,
            ActiveIndex1 = FindFirstAliveIndex(snapshots1),
            ActiveIndex2 = FindFirstAliveIndex(snapshots2)
        };

        if (session.ActiveIndex1 < 0 || session.ActiveIndex2 < 0)
            throw new Exception("Both players must have at least 1 non-fainted Pokemon in party.");

        _battles[session.BattleId] = session;
        return session;
    }

    public BattleSession? GetBattle(string battleId)
    {
        _battles.TryGetValue(battleId, out var battle);
        return battle;
    }

    public async Task SubmitActionAsync(string battleId, BattleAction action)
    {
        if (!_battles.TryGetValue(battleId, out var battle))
            throw new Exception("Battle not found.");

        if (battle.State != BattleState.Running)
            throw new Exception("Battle is not running.");

        if (action.PlayerId != battle.Player1Id && action.PlayerId != battle.Player2Id)
            throw new Exception("Player is not part of this battle.");

        ValidateAction(action);

        var gate = GetBattleGate(battleId);
        await gate.WaitAsync();
        try
        {
            if (battle.PendingActions.ContainsKey(action.PlayerId))
                throw new Exception("Action already submitted for this turn.");

            if (!battle.PendingActions.TryAdd(action.PlayerId, action))
                throw new Exception("Failed to submit action.");
        }
        finally
        {
            gate.Release();
        }
    }

    public bool IsTurnReady(string battleId)
    {
        if (!_battles.TryGetValue(battleId, out var battle))
            return false;

        return battle.PendingActions.ContainsKey(battle.Player1Id)
            && battle.PendingActions.ContainsKey(battle.Player2Id);
    }

    public async Task<BattleTurnResult?> ResolveTurnIfReadyAsync(string battleId)
    {
        if (!_battles.TryGetValue(battleId, out var battle))
            return null;

        var gate = GetBattleGate(battleId);
        await gate.WaitAsync();
        try
        {
            if (battle.State != BattleState.Running)
                return null;

            if (!battle.PendingActions.TryGetValue(battle.Player1Id, out var player1Action)
                || !battle.PendingActions.TryGetValue(battle.Player2Id, out var player2Action))
            {
                return null;
            }

            var resolvedTurnNumber = battle.TurnNumber;
            var result = new BattleTurnResult
            {
                BattleId = battle.BattleId,
                ResolvedTurnNumber = resolvedTurnNumber
            };

            var orderedActions = await BuildOrderedActionsAsync(battle, player1Action, player2Action);
            foreach (var ordered in orderedActions)
            {
                if (battle.State != BattleState.Running)
                    break;

                await ApplyActionAsync(battle, ordered.Action, result.Events);
                UpdateBattleEndState(battle, result.Events);
            }

            battle.PendingActions.Clear();
            if (battle.State == BattleState.Running)
                battle.TurnNumber++;

            result.NextTurnNumber = battle.TurnNumber;
            PopulateResultSnapshot(result, battle);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<PokemonInstance>> LoadParty(string playerId)
    {
        var filter = Builders<PokemonInstance>.Filter.And(
            Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, playerId),
            Builders<PokemonInstance>.Filter.Eq(p => p.IsInParty, true));

        return await _db.PokemonInstances
            .Find(filter)
            .SortBy(p => p.PartySlot)
            .Limit(BattleRules.MaxPartySize)
            .ToListAsync();
    }

    private static List<BattlePokemonSnapshot> ToSnapshots(List<PokemonInstance> team)
    {
        return team.Select(p => new BattlePokemonSnapshot
        {
            InstanceId = p.Id,
            SpeciesId = p.SpeciesId,
            Nickname = p.Nickname,
            Level = p.Level,
            CurrentHp = p.CurrentHp,
            MaxHp = p.MaxHp,
            StatusCondition = p.StatusCondition,
            Moves = p.Moves.Select(m => new PokemonMove
            {
                MoveId = m.MoveId,
                CurrentPp = m.CurrentPp
            }).ToList()
        }).ToList();
    }

    private async Task<List<OrderedAction>> BuildOrderedActionsAsync(
        BattleSession battle,
        BattleAction action1,
        BattleAction action2)
    {
        var orderedActions = new List<OrderedAction>
        {
            new(
                action1,
                await GetActionPriorityAsync(battle, action1),
                await GetActionSpeedAsync(battle, action1),
                _rng.Next()),
            new(
                action2,
                await GetActionPriorityAsync(battle, action2),
                await GetActionSpeedAsync(battle, action2),
                _rng.Next())
        };

        return orderedActions
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.Speed)
            .ThenByDescending(a => a.Tiebreaker)
            .ToList();
    }

    private async Task<int> GetActionPriorityAsync(BattleSession battle, BattleAction action)
    {
        if (action.Type == BattleActionType.Switch)
            return 6;

        var active = GetActivePokemon(battle, action.PlayerId);
        if (active == null || active.IsFainted || action.MoveSlot is null)
            return -1;

        var slot = action.MoveSlot.Value;
        if (slot < 0 || slot >= active.Moves.Count)
            return -1;

        var moveId = active.Moves[slot].MoveId;
        var move = await _db.Moves.Find(m => m.Id == moveId).FirstOrDefaultAsync();
        return move?.Priority ?? 0;
    }

    private async Task<int> GetActionSpeedAsync(BattleSession battle, BattleAction action)
    {
        var active = GetActivePokemon(battle, action.PlayerId);
        if (active == null)
            return 0;

        var entry = await _db.Pokedex.Find(p => p.Id == active.SpeciesId).FirstOrDefaultAsync();
        var baseSpeed = 50;
        if (entry?.BaseStats is not null && entry.BaseStats.TryGetValue("spd", out var spd))
            baseSpeed = spd;

        return Math.Max(1, baseSpeed + active.Level);
    }

    private async Task ApplyActionAsync(BattleSession battle, BattleAction action, List<string> events)
    {
        if (action.Type == BattleActionType.Switch)
        {
            ApplySwitchAction(battle, action, events);
            return;
        }

        await ApplyMoveActionAsync(battle, action, events);
    }

    private static void ApplySwitchAction(BattleSession battle, BattleAction action, List<string> events)
    {
        if (action.SwitchIndex is null)
        {
            events.Add($"[{action.PlayerId}] switch failed: missing target index.");
            return;
        }

        var team = GetTeam(battle, action.PlayerId);
        var currentIndex = GetActiveIndex(battle, action.PlayerId);
        var targetIndex = action.SwitchIndex.Value;

        if (targetIndex < 0 || targetIndex >= team.Count)
        {
            events.Add($"[{action.PlayerId}] switch failed: index out of range.");
            return;
        }

        if (currentIndex == targetIndex)
        {
            events.Add($"[{action.PlayerId}] switch ignored: pokemon already active.");
            return;
        }

        if (team[targetIndex].IsFainted)
        {
            events.Add($"[{action.PlayerId}] switch failed: selected pokemon is fainted.");
            return;
        }

        SetActiveIndex(battle, action.PlayerId, targetIndex);
        events.Add($"[{action.PlayerId}] switched to {GetDisplayName(team[targetIndex])}.");
    }

    private async Task ApplyMoveActionAsync(BattleSession battle, BattleAction action, List<string> events)
    {
        var attacker = GetActivePokemon(battle, action.PlayerId);
        if (attacker == null)
        {
            events.Add($"[{action.PlayerId}] move failed: no active pokemon.");
            return;
        }

        if (attacker.IsFainted)
        {
            events.Add($"[{action.PlayerId}] move failed: active pokemon is fainted.");
            return;
        }

        if (action.MoveSlot is null)
        {
            events.Add($"[{action.PlayerId}] move failed: missing move slot.");
            return;
        }

        var slot = action.MoveSlot.Value;
        if (slot < 0 || slot >= attacker.Moves.Count)
        {
            events.Add($"[{action.PlayerId}] move failed: move slot out of range.");
            return;
        }

        var selectedMove = attacker.Moves[slot];
        if (selectedMove.CurrentPp <= 0)
        {
            events.Add($"[{GetDisplayName(attacker)}] cannot act: no PP left.");
            return;
        }

        selectedMove.CurrentPp = Math.Max(0, selectedMove.CurrentPp - 1);
        var move = await _db.Moves.Find(m => m.Id == selectedMove.MoveId).FirstOrDefaultAsync();

        var moveName = move?.Name ?? $"Move#{selectedMove.MoveId}";
        var accuracy = Math.Clamp(move?.Accuracy ?? 100, 1, 100);
        var roll = _rng.Next(1, 101);

        if (roll > accuracy)
        {
            events.Add($"[{GetDisplayName(attacker)}] used {moveName} but missed.");
            return;
        }

        var defenderPlayerId = GetOpponentPlayerId(battle, action.PlayerId);
        var defender = GetActivePokemon(battle, defenderPlayerId);
        if (defender == null || defender.IsFainted)
        {
            events.Add($"[{GetDisplayName(attacker)}] used {moveName}, but target is unavailable.");
            return;
        }

        var power = Math.Max(0, move?.Power ?? 40);
        if (power == 0)
        {
            events.Add($"[{GetDisplayName(attacker)}] used {moveName}. (No direct damage)");
            return;
        }

        var damage = CalculateDamage(attacker.Level, power);
        defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);
        events.Add(
            $"[{GetDisplayName(attacker)}] used {moveName} on [{GetDisplayName(defender)}] for {damage} damage.");

        if (!defender.IsFainted)
            return;

        events.Add($"[{GetDisplayName(defender)}] fainted.");
        TryAutoSwitch(battle, defenderPlayerId, events);
    }

    private static int CalculateDamage(int attackerLevel, int movePower)
    {
        var baseDamage = ((2 * Math.Max(1, attackerLevel) / 5.0) + 2) * Math.Max(1, movePower) / 10.0;
        var randomFactor = BattleRules.DamageRandomMin
            + (_rng.NextDouble() * (BattleRules.DamageRandomMax - BattleRules.DamageRandomMin));
        return Math.Max(1, (int)Math.Round((baseDamage + 2) * randomFactor));
    }

    private static void TryAutoSwitch(BattleSession battle, string playerId, List<string> events)
    {
        var team = GetTeam(battle, playerId);
        var currentIndex = GetActiveIndex(battle, playerId);

        if (currentIndex >= 0 && currentIndex < team.Count && !team[currentIndex].IsFainted)
            return;

        var next = FindFirstAliveIndex(team);
        if (next < 0)
            return;

        SetActiveIndex(battle, playerId, next);
        events.Add($"[{playerId}] auto-switched to {GetDisplayName(team[next])}.");
    }

    private static void UpdateBattleEndState(BattleSession battle, List<string> events)
    {
        var player1Defeated = battle.Team1.All(p => p.IsFainted);
        var player2Defeated = battle.Team2.All(p => p.IsFainted);

        if (!player1Defeated && !player2Defeated)
            return;

        battle.State = BattleState.Ended;

        if (player1Defeated && player2Defeated)
        {
            battle.WinnerPlayerId = null;
            events.Add("Battle ended in a draw.");
            return;
        }

        battle.WinnerPlayerId = player2Defeated ? battle.Player1Id : battle.Player2Id;
        events.Add($"Battle ended. Winner: {battle.WinnerPlayerId}");
    }

    private static void PopulateResultSnapshot(BattleTurnResult result, BattleSession battle)
    {
        result.State = battle.State;
        result.WinnerPlayerId = battle.WinnerPlayerId;
        result.ActiveIndex1 = battle.ActiveIndex1;
        result.ActiveIndex2 = battle.ActiveIndex2;
        result.ActiveHp1 = GetActivePokemonHp(battle.Team1, battle.ActiveIndex1);
        result.ActiveHp2 = GetActivePokemonHp(battle.Team2, battle.ActiveIndex2);
    }

    private static int GetActivePokemonHp(List<BattlePokemonSnapshot> team, int index)
    {
        if (index < 0 || index >= team.Count)
            return 0;

        return team[index].CurrentHp;
    }

    private static SemaphoreSlim GetBattleGate(string battleId)
        => _battleLocks.GetOrAdd(battleId, _ => new SemaphoreSlim(1, 1));

    private static string GetOpponentPlayerId(BattleSession battle, string playerId)
        => playerId == battle.Player1Id ? battle.Player2Id : battle.Player1Id;

    private static List<BattlePokemonSnapshot> GetTeam(BattleSession battle, string playerId)
        => playerId == battle.Player1Id ? battle.Team1 : battle.Team2;

    private static int GetActiveIndex(BattleSession battle, string playerId)
        => playerId == battle.Player1Id ? battle.ActiveIndex1 : battle.ActiveIndex2;

    private static void SetActiveIndex(BattleSession battle, string playerId, int index)
    {
        if (playerId == battle.Player1Id)
            battle.ActiveIndex1 = index;
        else
            battle.ActiveIndex2 = index;
    }

    private static BattlePokemonSnapshot? GetActivePokemon(BattleSession battle, string playerId)
    {
        var team = GetTeam(battle, playerId);
        var activeIndex = GetActiveIndex(battle, playerId);
        if (activeIndex < 0 || activeIndex >= team.Count)
            return null;

        return team[activeIndex];
    }

    private static int FindFirstAliveIndex(List<BattlePokemonSnapshot> team)
        => team.FindIndex(p => !p.IsFainted);

    private static string GetDisplayName(BattlePokemonSnapshot pokemon)
        => string.IsNullOrWhiteSpace(pokemon.Nickname)
            ? $"Pokemon#{pokemon.SpeciesId}"
            : pokemon.Nickname;

    private static void ValidateAction(BattleAction action)
    {
        if (action.Type == BattleActionType.Move)
        {
            if (action.MoveSlot is null || action.MoveSlot < 0 || action.MoveSlot > 3)
                throw new Exception("Invalid move slot.");
        }
        else if (action.Type == BattleActionType.Switch)
        {
            if (action.SwitchIndex is null || action.SwitchIndex < 0 || action.SwitchIndex >= BattleRules.MaxPartySize)
                throw new Exception("Invalid switch index.");
        }
        else
        {
            throw new Exception("Unsupported action type.");
        }
    }

    private record OrderedAction(BattleAction Action, int Priority, int Speed, int Tiebreaker);
}
