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
    private const int WinnerMmrGain = 25;
    private const int LoserMmrLoss = 20;
    private const int WinnerVpGain = 10;

    // Only store non-1.0 multipliers.
    private static readonly Dictionary<string, Dictionary<string, double>> TypeEffectiveness =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["rock"] = 0.5, ["ghost"] = 0.0, ["steel"] = 0.5
            },
            ["fire"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["water"] = 0.5, ["grass"] = 2.0, ["ice"] = 2.0,
                ["bug"] = 2.0, ["rock"] = 0.5, ["dragon"] = 0.5, ["steel"] = 2.0
            },
            ["water"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 2.0, ["water"] = 0.5, ["grass"] = 0.5, ["ground"] = 2.0,
                ["rock"] = 2.0, ["dragon"] = 0.5
            },
            ["electric"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["water"] = 2.0, ["electric"] = 0.5, ["grass"] = 0.5,
                ["ground"] = 0.0, ["flying"] = 2.0, ["dragon"] = 0.5
            },
            ["grass"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["water"] = 2.0, ["grass"] = 0.5, ["poison"] = 0.5,
                ["ground"] = 2.0, ["flying"] = 0.5, ["bug"] = 0.5, ["rock"] = 2.0,
                ["dragon"] = 0.5, ["steel"] = 0.5
            },
            ["ice"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["water"] = 0.5, ["grass"] = 2.0, ["ground"] = 2.0,
                ["flying"] = 2.0, ["dragon"] = 2.0, ["steel"] = 0.5, ["ice"] = 0.5
            },
            ["fighting"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["normal"] = 2.0, ["ice"] = 2.0, ["poison"] = 0.5, ["flying"] = 0.5,
                ["psychic"] = 0.5, ["bug"] = 0.5, ["rock"] = 2.0, ["ghost"] = 0.0,
                ["dark"] = 2.0, ["steel"] = 2.0, ["fairy"] = 0.5
            },
            ["poison"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["grass"] = 2.0, ["poison"] = 0.5, ["ground"] = 0.5, ["rock"] = 0.5,
                ["ghost"] = 0.5, ["steel"] = 0.0, ["fairy"] = 2.0
            },
            ["ground"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 2.0, ["electric"] = 2.0, ["grass"] = 0.5, ["poison"] = 2.0,
                ["flying"] = 0.0, ["bug"] = 0.5, ["rock"] = 2.0, ["steel"] = 2.0
            },
            ["flying"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["electric"] = 0.5, ["grass"] = 2.0, ["fighting"] = 2.0,
                ["bug"] = 2.0, ["rock"] = 0.5, ["steel"] = 0.5
            },
            ["psychic"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fighting"] = 2.0, ["poison"] = 2.0, ["psychic"] = 0.5,
                ["dark"] = 0.0, ["steel"] = 0.5
            },
            ["bug"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["grass"] = 2.0, ["fighting"] = 0.5, ["poison"] = 0.5,
                ["flying"] = 0.5, ["psychic"] = 2.0, ["ghost"] = 0.5, ["dark"] = 2.0,
                ["steel"] = 0.5, ["fairy"] = 0.5
            },
            ["rock"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 2.0, ["ice"] = 2.0, ["fighting"] = 0.5, ["ground"] = 0.5,
                ["flying"] = 2.0, ["bug"] = 2.0, ["steel"] = 0.5
            },
            ["ghost"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["normal"] = 0.0, ["psychic"] = 2.0, ["ghost"] = 2.0, ["dark"] = 0.5
            },
            ["dragon"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["dragon"] = 2.0, ["steel"] = 0.5, ["fairy"] = 0.0
            },
            ["dark"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fighting"] = 0.5, ["psychic"] = 2.0, ["ghost"] = 2.0,
                ["dark"] = 0.5, ["fairy"] = 0.5
            },
            ["steel"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["water"] = 0.5, ["electric"] = 0.5, ["ice"] = 2.0,
                ["rock"] = 2.0, ["fairy"] = 2.0, ["steel"] = 0.5
            },
            ["fairy"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.5, ["fighting"] = 2.0, ["poison"] = 0.5,
                ["dragon"] = 2.0, ["dark"] = 2.0, ["steel"] = 0.5
            }
        };

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
            else
            {
                try
                {
                    await PersistBattleOutcomeAsync(battle, result.Events);
                }
                catch (Exception ex)
                {
                    result.Events.Add($"Failed to persist battle result: {ex.Message}");
                }
            }

            result.NextTurnNumber = battle.TurnNumber;
            PopulateResultSnapshot(result, battle);

            if (battle.State == BattleState.Ended)
            {
                _battles.TryRemove(battle.BattleId, out _);
                _battleLocks.TryRemove(battle.BattleId, out _);
            }

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
        var baseSpeed = GetBaseStat(entry, "spd", "speed");
        return CalculateBattleStat(baseSpeed, active.Level);
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
        var move = await _db.Moves.Find(m => m.Id == selectedMove.MoveId).FirstOrDefaultAsync()
            ?? new MoveEntry
            {
                Id = selectedMove.MoveId,
                Name = $"Move#{selectedMove.MoveId}",
                Power = 40,
                Accuracy = 100,
                Type = "normal",
                Priority = 0,
                Category = "Physical",
                PP = 10
            };

        var moveName = move.Name;
        var accuracy = Math.Clamp(move.Accuracy, 1, 100);
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

        var category = NormalizeCategory(move.Category);
        var power = Math.Max(0, move.Power);
        if (category == "status" || power == 0)
        {
            events.Add($"[{GetDisplayName(attacker)}] used {moveName}. (No direct damage)");
            return;
        }

        var damageOutcome = await CalculateDamageAsync(attacker, defender, move);
        var damage = damageOutcome.Damage;
        defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);

        events.Add(
            $"[{GetDisplayName(attacker)}] used {moveName} on [{GetDisplayName(defender)}] for {damage} damage.");

        if (damageOutcome.TypeMultiplier == 0)
            events.Add("It had no effect.");
        else if (damageOutcome.TypeMultiplier >= 2)
            events.Add("It's super effective!");
        else if (damageOutcome.TypeMultiplier > 0 && damageOutcome.TypeMultiplier < 1)
            events.Add("It's not very effective...");

        if (!defender.IsFainted)
            return;

        events.Add($"[{GetDisplayName(defender)}] fainted.");
        TryAutoSwitch(battle, defenderPlayerId, events);
    }

    private async Task<DamageOutcome> CalculateDamageAsync(
        BattlePokemonSnapshot attacker,
        BattlePokemonSnapshot defender,
        MoveEntry move)
    {
        var attackerEntry = await _db.Pokedex.Find(p => p.Id == attacker.SpeciesId).FirstOrDefaultAsync();
        var defenderEntry = await _db.Pokedex.Find(p => p.Id == defender.SpeciesId).FirstOrDefaultAsync();

        var moveType = NormalizeType(move.Type);
        var attackerTypes = attackerEntry?.Types ?? new List<string>();
        var defenderTypes = defenderEntry?.Types ?? new List<string>();

        var typeMultiplier = GetTypeMultiplier(moveType, defenderTypes);
        if (typeMultiplier <= 0)
            return new DamageOutcome(0, 0, 1, typeMultiplier);

        var category = NormalizeCategory(move.Category);

        var attackBaseStat = category == "special"
            ? GetBaseStat(attackerEntry, "spatk", "special-attack", "special_attack")
            : GetBaseStat(attackerEntry, "atk", "attack");

        var defenseBaseStat = category == "special"
            ? GetBaseStat(defenderEntry, "spdef", "special-defense", "special_defense")
            : GetBaseStat(defenderEntry, "def", "defense");

        var attackStat = CalculateBattleStat(attackBaseStat, attacker.Level);
        var defenseStat = CalculateBattleStat(defenseBaseStat, defender.Level);
        var level = Math.Max(1, attacker.Level);
        var power = Math.Max(1, move.Power);

        var baseDamage = (((2d * level / 5d) + 2d) * power * attackStat / Math.Max(1, defenseStat)) / 50d + 2d;
        var hasStab = attackerTypes.Any(t => NormalizeType(t) == moveType);
        var stab = hasStab ? 1.5 : 1.0;
        var randomFactor = BattleRules.DamageRandomMin
            + (_rng.NextDouble() * (BattleRules.DamageRandomMax - BattleRules.DamageRandomMin));

        var modifier = stab * typeMultiplier * randomFactor;
        var damage = Math.Max(1, (int)Math.Floor(baseDamage * modifier));
        return new DamageOutcome(damage, attackStat, defenseStat, typeMultiplier);
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

    private async Task PersistBattleOutcomeAsync(BattleSession battle, List<string> events)
    {
        var player1Filter = Builders<Player>.Filter.Eq(p => p.Id, battle.Player1Id);
        var player2Filter = Builders<Player>.Filter.Eq(p => p.Id, battle.Player2Id);

        if (string.IsNullOrWhiteSpace(battle.WinnerPlayerId))
        {
            var drawUpdate = Builders<Player>.Update.Inc(p => p.RankedMatches, 1);
            await _db.Players.UpdateOneAsync(player1Filter, drawUpdate);
            await _db.Players.UpdateOneAsync(player2Filter, drawUpdate);
            events.Add("Ranked result persisted (draw).");
            return;
        }

        var winnerId = battle.WinnerPlayerId;
        var loserId = winnerId == battle.Player1Id ? battle.Player2Id : battle.Player1Id;

        var winnerFilter = Builders<Player>.Filter.Eq(p => p.Id, winnerId);
        var loserFilter = Builders<Player>.Filter.Eq(p => p.Id, loserId);

        var winnerUpdate = Builders<Player>.Update
            .Inc(p => p.RankedMatches, 1)
            .Inc(p => p.RankedWins, 1)
            .Inc(p => p.MMR, WinnerMmrGain)
            .Inc(p => p.VP, WinnerVpGain);

        var loserUpdate = Builders<Player>.Update
            .Inc(p => p.RankedMatches, 1)
            .Inc(p => p.MMR, -LoserMmrLoss);

        await _db.Players.UpdateOneAsync(winnerFilter, winnerUpdate);
        await _db.Players.UpdateOneAsync(loserFilter, loserUpdate);
        events.Add($"Ranked result persisted (winner +{WinnerMmrGain} MMR, loser -{LoserMmrLoss} MMR).");
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

    private static int GetBaseStat(PokedexEntry? entry, params string[] keys)
    {
        if (entry?.BaseStats == null || entry.BaseStats.Count == 0)
            return 50;

        foreach (var key in keys)
        {
            if (entry.BaseStats.TryGetValue(key, out var value))
                return value;
        }

        foreach (var pair in entry.BaseStats)
        {
            foreach (var key in keys)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
        }

        return 50;
    }

    private static int CalculateBattleStat(int baseStat, int level)
        => Math.Max(1, ((2 * Math.Max(1, baseStat) * Math.Max(1, level)) / 100) + 5);

    private static string NormalizeType(string? type)
        => string.IsNullOrWhiteSpace(type) ? "normal" : type.Trim().ToLowerInvariant();

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "physical";

        var normalized = category.Trim().ToLowerInvariant();
        if (normalized.Contains("special"))
            return "special";
        if (normalized.Contains("status"))
            return "status";
        return "physical";
    }

    private static double GetTypeMultiplier(string moveType, IEnumerable<string> defenderTypes)
    {
        var normalizedMoveType = NormalizeType(moveType);
        var multiplier = 1.0;

        foreach (var defenderType in defenderTypes)
        {
            var normalizedDefType = NormalizeType(defenderType);
            if (!TypeEffectiveness.TryGetValue(normalizedMoveType, out var vsTable))
                continue;

            if (vsTable.TryGetValue(normalizedDefType, out var value))
                multiplier *= value;
        }

        return multiplier;
    }

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
    private record DamageOutcome(int Damage, int AttackStat, int DefenseStat, double TypeMultiplier);
}
