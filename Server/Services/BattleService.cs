using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Options;

namespace PokemonMMO.Services;

public class BattleService
{
    private readonly MongoDbContext _db;
    private readonly BattleOptions _battleOptions;
    private static readonly ConcurrentDictionary<string, BattleSession> _battles = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _battleLocks = new();
    private static readonly Random _rng = new();

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

    public BattleService(MongoDbContext db, IOptions<BattleOptions> battleOptions)
    {
        _db = db;
        _battleOptions = battleOptions.Value;
        ValidateBattleOptions(_battleOptions);
    }

    public int TurnTimeoutSeconds => _battleOptions.TurnTimeoutSeconds;

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
            ActiveIndex2 = FindFirstAliveIndex(snapshots2),
            TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(_battleOptions.TurnTimeoutSeconds)
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

        var gate = GetBattleGate(battleId);
        await gate.WaitAsync();
        try
        {
            if (battle.State != BattleState.Running)
                throw new Exception("Battle is not running.");

            ValidateAction(action);
            ValidateActionForBattleState(battle, action);

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

        return HasBothActions(battle);
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

            var timeoutEvents = new List<BattleEvent>();
            ApplyTurnTimeoutIfNeeded(battle, timeoutEvents);

            battle.PendingActions.TryGetValue(battle.Player1Id, out var player1Action);
            battle.PendingActions.TryGetValue(battle.Player2Id, out var player2Action);

            if (battle.State == BattleState.Running
                && (player1Action == null || player2Action == null))
                return null;

            var resolvedTurnNumber = battle.TurnNumber;
            var result = new BattleTurnResult
            {
                BattleId = battle.BattleId,
                ResolvedTurnNumber = resolvedTurnNumber
            };
            result.TypedEvents.AddRange(timeoutEvents);

            if (battle.State == BattleState.Running)
            {
                var orderedActions = await BuildOrderedActionsAsync(battle, player1Action!, player2Action!);
                foreach (var ordered in orderedActions)
                {
                    if (battle.State != BattleState.Running)
                        break;

                    await ApplyActionAsync(battle, ordered.Action, result.TypedEvents);
                    UpdateBattleEndState(battle, result.TypedEvents);
                }

                if (battle.State == BattleState.Running)
                    await ApplyEndOfTurnEffectsAsync(battle, result.TypedEvents);

                UpdateBattleEndState(battle, result.TypedEvents);
            }

            battle.PendingActions.Clear();
            if (battle.State == BattleState.Running)
            {
                battle.TurnNumber++;
                battle.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(_battleOptions.TurnTimeoutSeconds);
            }
            else
            {
                try
                {
                    await PersistBattleOutcomeAsync(battle, result.TypedEvents);
                }
                catch (Exception ex)
                {
                    result.TypedEvents.Add(new MessageEvent { Message = $"Failed to persist battle result: {ex.Message}" });
                }
            }

            result.NextTurnNumber = battle.TurnNumber;
            result.Events = TypedEventsToStrings(result.TypedEvents);
            PopulateResultSnapshot(result, battle);
            await WriteBattleLogAsync(battle, result, "turn_resolve");

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

    public async Task<BattleTurnResult?> ForfeitPlayerAsync(string playerId, string reason)
    {
        var battle = _battles.Values.FirstOrDefault(b =>
            b.State == BattleState.Running
            && (b.Player1Id == playerId || b.Player2Id == playerId));

        if (battle == null)
            return null;

        var gate = GetBattleGate(battle.BattleId);
        await gate.WaitAsync();
        try
        {
            if (battle.State != BattleState.Running)
                return null;

            battle.State = BattleState.Ended;
            battle.WinnerPlayerId = GetOpponentPlayerId(battle, playerId);
            battle.PendingActions.Clear();

            var result = new BattleTurnResult
            {
                BattleId = battle.BattleId,
                ResolvedTurnNumber = battle.TurnNumber,
                NextTurnNumber = battle.TurnNumber,
                State = battle.State,
                WinnerPlayerId = battle.WinnerPlayerId
            };

            result.TypedEvents.Add(new MessageEvent { Message = $"Player {playerId} forfeited: {reason}" });
            result.TypedEvents.Add(new BattleEndEvent { WinnerPlayerId = battle.WinnerPlayerId, Reason = "forfeit" });

            try
            {
                await PersistBattleOutcomeAsync(battle, result.TypedEvents);
            }
            catch (Exception ex)
            {
                result.TypedEvents.Add(new MessageEvent { Message = $"Failed to persist battle result: {ex.Message}" });
            }

            result.Events = TypedEventsToStrings(result.TypedEvents);
            PopulateResultSnapshot(result, battle);
            await WriteBattleLogAsync(battle, result, "forfeit");
            _battles.TryRemove(battle.BattleId, out _);
            _battleLocks.TryRemove(battle.BattleId, out _);
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
            .Limit(_battleOptions.MaxPartySize)
            .ToListAsync();
    }

    private static List<BattlePokemonSnapshot> ToSnapshots(List<PokemonInstance> team)
    {
        return team.Select(p =>
        {
            var snap = new BattlePokemonSnapshot
            {
                InstanceId = p.Id,
                SpeciesId = p.SpeciesId,
                Nickname = p.Nickname,
                Level = p.Level,
                CurrentHp = p.CurrentHp,
                MaxHp = p.MaxHp,
                NonVolatileStatus = ParseLegacyStatus(p.StatusCondition),
                Moves = p.Moves.Select(m => new PokemonMove
                {
                    MoveId = m.MoveId,
                    CurrentPp = m.CurrentPp
                }).ToList()
            };
            // Restore toxic counter if the legacy string encoded it
            if (snap.NonVolatileStatus == PokemonStatusCondition.Toxic)
                snap.ToxicCounter = 1;
            return snap;
        }).ToList();
    }

    private static PokemonStatusCondition ParseLegacyStatus(string? raw) =>
        (raw ?? "").ToUpperInvariant() switch
        {
            "BRN" or "BURN"       => PokemonStatusCondition.Burn,
            "PAR" or "PARALYSIS"  => PokemonStatusCondition.Paralysis,
            "PSN" or "POISON"     => PokemonStatusCondition.Poison,
            "TOX" or "TOXIC"      => PokemonStatusCondition.Toxic,
            "SLP" or "SLEEP"      => PokemonStatusCondition.Sleep,
            "FRZ" or "FREEZE"     => PokemonStatusCondition.Freeze,
            _                     => PokemonStatusCondition.None
        };

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
            return _battleOptions.SwitchActionPriority;

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

    private async Task ApplyActionAsync(BattleSession battle, BattleAction action, List<BattleEvent> events)
    {
        if (action.Type == BattleActionType.Switch)
        {
            ApplySwitchAction(battle, action, events);
            return;
        }

        await ApplyMoveActionAsync(battle, action, events);
    }

    private static void ApplySwitchAction(BattleSession battle, BattleAction action, List<BattleEvent> events,
        bool isAutoSwitch = false)
    {
        if (action.SwitchIndex is null)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] switch failed: missing target index." });
            return;
        }

        var team = GetTeam(battle, action.PlayerId);
        var currentIndex = GetActiveIndex(battle, action.PlayerId);
        var targetIndex = action.SwitchIndex.Value;

        if (targetIndex < 0 || targetIndex >= team.Count)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] switch failed: index out of range." });
            return;
        }

        if (currentIndex == targetIndex)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] switch ignored: pokemon already active." });
            return;
        }

        if (team[targetIndex].IsFainted)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] switch failed: selected pokemon is fainted." });
            return;
        }

        var withdrawn = currentIndex >= 0 && currentIndex < team.Count ? team[currentIndex] : null;
        SetActiveIndex(battle, action.PlayerId, targetIndex);
        events.Add(new SwitchEvent
        {
            PlayerId = action.PlayerId,
            WithdrawnPokemonName = withdrawn != null ? GetDisplayName(withdrawn) : "",
            SentOutPokemonName = GetDisplayName(team[targetIndex]),
            NewActiveIndex = targetIndex,
            IsAutoSwitch = isAutoSwitch
        });
    }

    private async Task ApplyMoveActionAsync(BattleSession battle, BattleAction action, List<BattleEvent> events)
    {
        var attacker = GetActivePokemon(battle, action.PlayerId);
        if (attacker == null)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] move failed: no active pokemon." });
            return;
        }

        if (attacker.IsFainted)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] move failed: active pokemon is fainted." });
            return;
        }

        if (action.MoveSlot is null)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] move failed: missing move slot." });
            return;
        }

        var slot = action.MoveSlot.Value;
        if (slot < 0 || slot >= attacker.Moves.Count)
        {
            events.Add(new MessageEvent { Message = $"[{action.PlayerId}] move failed: move slot out of range." });
            return;
        }

        // ── Pre-move status checks (pbs-unity: non-volatile status prevents acting) ──
        if (!CheckCanAct(attacker, events))
            return;

        var selectedMove = attacker.Moves[slot];
        if (selectedMove.CurrentPp <= 0)
        {
            events.Add(new MessageEvent { Message = $"[{GetDisplayName(attacker)}] cannot act: no PP left." });
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
        events.Add(new MoveUsedEvent
        {
            UserId = action.PlayerId,
            PokemonName = GetDisplayName(attacker),
            MoveName = moveName,
            MoveId = selectedMove.MoveId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        var accuracy = Math.Clamp(move.Accuracy ?? 100, 1, 100);
        var roll = _rng.Next(1, 101);

        if (roll > accuracy)
        {
            events.Add(new MoveMissedEvent
            {
                UserId = action.PlayerId,
                PokemonName = GetDisplayName(attacker),
                MoveName = moveName
            });
            return;
        }

        var defenderPlayerId = GetOpponentPlayerId(battle, action.PlayerId);
        var defender = GetActivePokemon(battle, defenderPlayerId);
        if (defender == null || defender.IsFainted)
        {
            events.Add(new MessageEvent { Message = $"[{GetDisplayName(attacker)}] used {moveName}, but target is unavailable." });
            return;
        }

        var category = NormalizeCategory(move.Category);
        var power = Math.Max(0, move.Power ?? 0);

        // ── Status moves ──────────────────────────────────────────────────────
        if (category == "status" || power == 0)
        {
            ApplyStatusMove(attacker, defender, defenderPlayerId, move, battle, events);
            return;
        }

        // ── Damage moves ──────────────────────────────────────────────────────
        var damageOutcome = await CalculateDamageAsync(attacker, defender, move, battle);
        var damage = damageOutcome.Damage;

        if (damageOutcome.TypeMultiplier <= 0)
        {
            events.Add(new MoveNoEffectEvent { TargetName = GetDisplayName(defender) });
            return;
        }

        var hpBefore = defender.CurrentHp;
        defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);

        events.Add(new PokemonDamageEvent
        {
            PlayerId = defenderPlayerId,
            PokemonName = GetDisplayName(defender),
            Damage = damage,
            HpBefore = hpBefore,
            HpAfter = defender.CurrentHp,
            MaxHp = defender.MaxHp,
            TypeMultiplier = damageOutcome.TypeMultiplier
        });

        if (damageOutcome.TypeMultiplier >= 2)
            events.Add(new SuperEffectiveEvent { Multiplier = damageOutcome.TypeMultiplier });
        else if (damageOutcome.TypeMultiplier < 1)
            events.Add(new NotVeryEffectiveEvent { Multiplier = damageOutcome.TypeMultiplier });

        if (defender.IsFainted)
        {
            events.Add(new PokemonFaintEvent { PlayerId = defenderPlayerId, PokemonName = GetDisplayName(defender) });
            TryAutoSwitch(battle, defenderPlayerId, events);
        }
    }

    /// <summary>
    /// Pre-move check: paralysis/sleep/freeze.
    /// Returns false if pokemon cannot act this turn.
    /// Inspired by pbs-unity BattleProperties pre-move checks.
    /// </summary>
    private bool CheckCanAct(BattlePokemonSnapshot pokemon, List<BattleEvent> events)
    {
        switch (pokemon.NonVolatileStatus)
        {
            case PokemonStatusCondition.Sleep:
                if (pokemon.SleepTurnsLeft > 0)
                {
                    pokemon.SleepTurnsLeft--;
                    events.Add(new SleepSkipEvent
                    {
                        PokemonName = GetDisplayName(pokemon),
                        TurnsLeft = pokemon.SleepTurnsLeft
                    });
                    if (pokemon.SleepTurnsLeft == 0)
                    {
                        pokemon.NonVolatileStatus = PokemonStatusCondition.None;
                        events.Add(new StatusHealedEvent
                        {
                            PokemonName = GetDisplayName(pokemon),
                            Status = PokemonStatusCondition.Sleep
                        });
                    }
                    return false;
                }
                pokemon.NonVolatileStatus = PokemonStatusCondition.None;
                break;

            case PokemonStatusCondition.Freeze:
                var thawRoll = _rng.Next(1, 101);
                if (thawRoll > 20) // 20% thaw chance per turn
                {
                    events.Add(new SleepSkipEvent { PokemonName = GetDisplayName(pokemon), TurnsLeft = -1 });
                    return false;
                }
                pokemon.NonVolatileStatus = PokemonStatusCondition.None;
                events.Add(new FreezeThawEvent { PokemonName = GetDisplayName(pokemon) });
                break;

            case PokemonStatusCondition.Paralysis:
                var paraRoll = _rng.Next(1, 101);
                if (paraRoll <= 25) // 25% fully paralyzed
                {
                    events.Add(new ParalysisStuckEvent { PokemonName = GetDisplayName(pokemon) });
                    return false;
                }
                break;
        }
        return true;
    }

    /// <summary>
    /// Handles status-category moves (inflict conditions, stat changes).
    /// Inspired by pbs-unity Databases.Effects.Moves handling.
    /// </summary>
    private void ApplyStatusMove(
        BattlePokemonSnapshot attacker,
        BattlePokemonSnapshot defender,
        string defenderPlayerId,
        MoveEntry move,
        BattleSession battle,
        List<BattleEvent> events)
    {
        var effect = (move.Effect ?? "").ToLowerInvariant();

        // Status infliction effects
        var statusToInflict = effect switch
        {
            "burn" or "will-o-wisp"      => PokemonStatusCondition.Burn,
            "thunder-wave" or "paralyze" or "paralysis" => PokemonStatusCondition.Paralysis,
            "toxic" or "badly-poison"    => PokemonStatusCondition.Toxic,
            "poison" or "poison-powder"  => PokemonStatusCondition.Poison,
            "sleep" or "spore" or "sing" or "hypnosis" => PokemonStatusCondition.Sleep,
            "freeze"                     => PokemonStatusCondition.Freeze,
            _ => PokemonStatusCondition.None
        };

        if (statusToInflict != PokemonStatusCondition.None)
        {
            TryInflictStatus(defender, defenderPlayerId, statusToInflict, events);
            return;
        }

        // Stat change effects (e.g. growl = -1 ATK, tail whip = -1 DEF, etc.)
        var statChange = ParseStatChangeEffect(effect);
        if (statChange is not null)
        {
            var target = statChange.TargetSelf ? attacker : defender;
            var targetPlayerId = statChange.TargetSelf
                ? GetOpponentPlayerId(battle, defenderPlayerId)
                : defenderPlayerId;
            ApplyStatStageChange(target, targetPlayerId, statChange.Stat, statChange.Stages, events);
            return;
        }

        events.Add(new MessageEvent { Message = $"[{GetDisplayName(attacker)}] used a status move. (Effect: {effect})" });
    }

    private void TryInflictStatus(BattlePokemonSnapshot target, string targetPlayerId,
        PokemonStatusCondition status, List<BattleEvent> events)
    {
        if (target.NonVolatileStatus != PokemonStatusCondition.None)
        {
            events.Add(new StatusBlockedEvent
            {
                PokemonName = GetDisplayName(target),
                Reason = "already has a status condition"
            });
            return;
        }

        target.NonVolatileStatus = status;
        if (status == PokemonStatusCondition.Sleep)
            target.SleepTurnsLeft = _rng.Next(1, 4); // 1–3 turns
        if (status == PokemonStatusCondition.Toxic)
            target.ToxicCounter = 1;

        events.Add(new StatusInflictedEvent
        {
            PlayerId = targetPlayerId,
            PokemonName = GetDisplayName(target),
            Status = status
        });
    }

    private static void ApplyStatStageChange(BattlePokemonSnapshot target, string targetPlayerId,
        StatIndex stat, int stages, List<BattleEvent> events)
    {
        var current = target.GetStage(stat);
        var clamped = Math.Clamp(current + stages, -6, 6);
        var actual = clamped - current;

        if (actual == 0)
        {
            events.Add(new StatChangeBlockedEvent
            {
                PokemonName = GetDisplayName(target),
                Stat = stat,
                Reason = stages > 0 ? "already at maximum" : "already at minimum"
            });
            return;
        }

        target.StatStages[(int)stat] = clamped;
        events.Add(new StatChangeEvent
        {
            PlayerId = targetPlayerId,
            PokemonName = GetDisplayName(target),
            Stat = stat,
            Stages = actual,
            NewStage = clamped
        });
    }

    private record StatChangeInfo(StatIndex Stat, int Stages, bool TargetSelf);

    private static StatChangeInfo? ParseStatChangeEffect(string effect) => effect switch
    {
        "growl"           => new StatChangeInfo(StatIndex.ATK, -1, false),
        "tail-whip"       => new StatChangeInfo(StatIndex.DEF, -1, false),
        "leer"            => new StatChangeInfo(StatIndex.DEF, -1, false),
        "screech"         => new StatChangeInfo(StatIndex.DEF, -2, false),
        "charm"           => new StatChangeInfo(StatIndex.ATK, -2, false),
        "growl-sharp"     => new StatChangeInfo(StatIndex.SPA, -1, false),
        "swords-dance"    => new StatChangeInfo(StatIndex.ATK, +2, true),
        "nasty-plot"      => new StatChangeInfo(StatIndex.SPA, +2, true),
        "calm-mind"       => new StatChangeInfo(StatIndex.SPA, +1, true),
        "bulk-up"         => new StatChangeInfo(StatIndex.ATK, +1, true),
        "agility"         => new StatChangeInfo(StatIndex.SPE, +2, true),
        "harden"          => new StatChangeInfo(StatIndex.DEF, +1, true),
        "withdraw"        => new StatChangeInfo(StatIndex.DEF, +1, true),
        "defense-curl"    => new StatChangeInfo(StatIndex.DEF, +1, true),
        "amnesia"         => new StatChangeInfo(StatIndex.SPD, +2, true),
        "barrier"         => new StatChangeInfo(StatIndex.DEF, +2, true),
        "acid-armor"      => new StatChangeInfo(StatIndex.DEF, +2, true),
        "minimize"        => new StatChangeInfo(StatIndex.EVA, +2, true),
        "double-team"     => new StatChangeInfo(StatIndex.EVA, +1, true),
        _                 => (StatChangeInfo?)null
    };

    private async Task<DamageOutcome> CalculateDamageAsync(
        BattlePokemonSnapshot attacker,
        BattlePokemonSnapshot defender,
        MoveEntry move,
        BattleSession battle)
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

        var attackStatBase = CalculateBattleStat(attackBaseStat, attacker.Level);
        var defenseStatBase = CalculateBattleStat(defenseBaseStat, defender.Level);

        // ── Apply stat stages (pbs-unity GetStageMultiplier logic) ───────────
        var attackStageIdx = category == "special" ? StatIndex.SPA : StatIndex.ATK;
        var defenseStageIdx = category == "special" ? StatIndex.SPD : StatIndex.DEF;
        var attackStat = (int)(attackStatBase * attacker.GetStageMultiplier(attackStageIdx));
        var defenseStat = (int)(defenseStatBase * defender.GetStageMultiplier(defenseStageIdx));

        // ── Burn: -50% physical ATK (pbs-unity Burn effect) ─────────────────
        if (attacker.NonVolatileStatus == PokemonStatusCondition.Burn && category == "physical")
            attackStat = (int)(attackStat * 0.5);

        var level = Math.Max(1, attacker.Level);
        var power = Math.Max(1, move.Power ?? 1);

        var baseDamage = (((2d * level / 5d) + 2d) * power * attackStat / Math.Max(1, defenseStat)) / 50d + 2d;
        var hasStab = attackerTypes.Any(t => NormalizeType(t) == moveType);
        var stab = hasStab ? 1.5 : 1.0;

        // ── Weather modifier (pbs-unity weather damage scaling) ──────────────
        var weatherMod = GetWeatherDamageModifier(battle.Weather, moveType);

        var randomFactor = _battleOptions.DamageRandomMin
            + (_rng.NextDouble() * (_battleOptions.DamageRandomMax - _battleOptions.DamageRandomMin));

        var modifier = stab * typeMultiplier * weatherMod * randomFactor;
        var damage = Math.Max(1, (int)Math.Floor(baseDamage * modifier));
        return new DamageOutcome(damage, attackStat, defenseStat, typeMultiplier);
    }

    private static double GetWeatherDamageModifier(WeatherCondition weather, string moveType) =>
        weather switch
        {
            WeatherCondition.Sun  when moveType == "fire"  => 1.5,
            WeatherCondition.Sun  when moveType == "water" => 0.5,
            WeatherCondition.Rain when moveType == "water" => 1.5,
            WeatherCondition.Rain when moveType == "fire"  => 0.5,
            _ => 1.0
        };

    private static void TryAutoSwitch(BattleSession battle, string playerId, List<BattleEvent> events)
    {
        var team = GetTeam(battle, playerId);
        var currentIndex = GetActiveIndex(battle, playerId);

        if (currentIndex >= 0 && currentIndex < team.Count && !team[currentIndex].IsFainted)
            return;

        var next = FindFirstAliveIndex(team);
        if (next < 0)
            return;

        var withdrawn = currentIndex >= 0 && currentIndex < team.Count ? team[currentIndex] : null;
        SetActiveIndex(battle, playerId, next);
        events.Add(new SwitchEvent
        {
            PlayerId = playerId,
            WithdrawnPokemonName = withdrawn != null ? GetDisplayName(withdrawn) : "",
            SentOutPokemonName = GetDisplayName(team[next]),
            NewActiveIndex = next,
            IsAutoSwitch = true
        });
    }

    private static void UpdateBattleEndState(BattleSession battle, List<BattleEvent> events)
    {
        var player1Defeated = battle.Team1.All(p => p.IsFainted);
        var player2Defeated = battle.Team2.All(p => p.IsFainted);

        if (!player1Defeated && !player2Defeated)
            return;

        if (battle.State == BattleState.Ended)
            return;

        battle.State = BattleState.Ended;

        if (player1Defeated && player2Defeated)
        {
            battle.WinnerPlayerId = null;
            events.Add(new BattleEndEvent { WinnerPlayerId = null, Reason = "draw" });
            return;
        }

        battle.WinnerPlayerId = player2Defeated ? battle.Player1Id : battle.Player2Id;
        events.Add(new BattleEndEvent { WinnerPlayerId = battle.WinnerPlayerId, Reason = "all_fainted" });
    }

    private async Task PersistBattleOutcomeAsync(BattleSession battle, List<BattleEvent> events)
    {
        var player1Filter = Builders<Player>.Filter.Eq(p => p.Id, battle.Player1Id);
        var player2Filter = Builders<Player>.Filter.Eq(p => p.Id, battle.Player2Id);

        if (string.IsNullOrWhiteSpace(battle.WinnerPlayerId))
        {
            var drawUpdate = Builders<Player>.Update.Inc(p => p.RankedMatches, 1);
            await _db.Players.UpdateOneAsync(player1Filter, drawUpdate);
            await _db.Players.UpdateOneAsync(player2Filter, drawUpdate);
            events.Add(new MessageEvent { Message = "Ranked result persisted (draw)." });
            return;
        }

        var winnerId = battle.WinnerPlayerId;
        var loserId = winnerId == battle.Player1Id ? battle.Player2Id : battle.Player1Id;

        var winnerFilter = Builders<Player>.Filter.Eq(p => p.Id, winnerId);
        var loserFilter = Builders<Player>.Filter.Eq(p => p.Id, loserId);

        var winnerUpdate = Builders<Player>.Update
            .Inc(p => p.RankedMatches, 1)
            .Inc(p => p.RankedWins, 1)
            .Inc(p => p.MMR, _battleOptions.WinnerMmrGain)
            .Inc(p => p.VP, _battleOptions.WinnerVpGain);

        var loserUpdate = Builders<Player>.Update
            .Inc(p => p.RankedMatches, 1)
            .Inc(p => p.MMR, -_battleOptions.LoserMmrLoss);

        await _db.Players.UpdateOneAsync(winnerFilter, winnerUpdate);
        await _db.Players.UpdateOneAsync(loserFilter, loserUpdate);
        events.Add(new MessageEvent { Message = $"Ranked result persisted (winner +{_battleOptions.WinnerMmrGain} MMR, loser -{_battleOptions.LoserMmrLoss} MMR)." });
    }

    private async Task WriteBattleLogAsync(BattleSession battle, BattleTurnResult result, string source)
    {
        var logEntry = new BattleLogEntry
        {
            BattleId = battle.BattleId,
            Source = source,
            ResolvedTurnNumber = result.ResolvedTurnNumber,
            NextTurnNumber = result.NextTurnNumber,
            State = result.State.ToString(),
            Player1Id = battle.Player1Id,
            Player2Id = battle.Player2Id,
            WinnerPlayerId = result.WinnerPlayerId,
            Events = result.Events.ToList(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _db.BattleLogs.InsertOneAsync(logEntry);
    }

    private static void PopulateResultSnapshot(BattleTurnResult result, BattleSession battle)
    {
        result.State = battle.State;
        result.WinnerPlayerId = battle.WinnerPlayerId;
        result.ActiveIndex1 = battle.ActiveIndex1;
        result.ActiveIndex2 = battle.ActiveIndex2;
        result.ActiveHp1 = GetActivePokemonHp(battle.Team1, battle.ActiveIndex1);
        result.ActiveHp2 = GetActivePokemonHp(battle.Team2, battle.ActiveIndex2);
        result.Weather = battle.Weather;
        result.WeatherTurnsLeft = battle.WeatherTurnsLeft;
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

    private static void ApplyTurnTimeoutIfNeeded(BattleSession battle, List<BattleEvent> events)
    {
        if (battle.State != BattleState.Running)
            return;

        if (DateTime.UtcNow < battle.TurnDeadlineUtc)
            return;

        if (HasBothActions(battle))
            return;

        var hasP1Action = battle.PendingActions.ContainsKey(battle.Player1Id);
        var hasP2Action = battle.PendingActions.ContainsKey(battle.Player2Id);

        if (!hasP1Action && !hasP2Action)
        {
            battle.State = BattleState.Ended;
            battle.WinnerPlayerId = null;
            events.Add(new MessageEvent { Message = $"Turn {battle.TurnNumber} timeout: both players inactive." });
            events.Add(new BattleEndEvent { WinnerPlayerId = null, Reason = "timeout_draw" });
            return;
        }

        var loserId = hasP1Action ? battle.Player2Id : battle.Player1Id;
        battle.State = BattleState.Ended;
        battle.WinnerPlayerId = GetOpponentPlayerId(battle, loserId);
        events.Add(new MessageEvent { Message = $"Turn {battle.TurnNumber} timeout: {loserId} did not submit action." });
        events.Add(new BattleEndEvent { WinnerPlayerId = battle.WinnerPlayerId, Reason = "timeout" });
    }

    private static bool HasBothActions(BattleSession battle)
        => battle.PendingActions.ContainsKey(battle.Player1Id)
           && battle.PendingActions.ContainsKey(battle.Player2Id);

    private static void ValidateActionForBattleState(BattleSession battle, BattleAction action)
    {
        if (battle.State != BattleState.Running)
            throw new Exception("Battle is not running.");

        var team = GetTeam(battle, action.PlayerId);
        if (team.Count == 0)
            throw new Exception("Player has no team in this battle.");

        var active = GetActivePokemon(battle, action.PlayerId);
        if (active == null)
            throw new Exception("No active pokemon.");

        if (action.Type == BattleActionType.Move)
        {
            if (active.IsFainted)
                throw new Exception("Active pokemon fainted. You must switch.");

            var moveSlot = action.MoveSlot ?? -1;
            if (moveSlot < 0 || moveSlot >= active.Moves.Count)
                throw new Exception("Move slot does not exist on active pokemon.");

            if (active.Moves[moveSlot].CurrentPp <= 0)
                throw new Exception("Selected move has no PP.");

            return;
        }

        if (action.Type == BattleActionType.Switch)
        {
            var targetIndex = action.SwitchIndex ?? -1;
            if (targetIndex < 0 || targetIndex >= team.Count)
                throw new Exception("Switch index out of range.");

            var activeIndex = GetActiveIndex(battle, action.PlayerId);
            if (targetIndex == activeIndex)
                throw new Exception("Target pokemon is already active.");

            if (team[targetIndex].IsFainted)
                throw new Exception("Cannot switch to a fainted pokemon.");
        }
    }

    private void ValidateAction(BattleAction action)
    {
        if (action.Type == BattleActionType.Move)
        {
            if (action.MoveSlot is null || action.MoveSlot < 0 || action.MoveSlot > 3)
                throw new Exception("Invalid move slot.");
        }
        else if (action.Type == BattleActionType.Switch)
        {
            if (action.SwitchIndex is null || action.SwitchIndex < 0 || action.SwitchIndex >= _battleOptions.MaxPartySize)
                throw new Exception("Invalid switch index.");
        }
        else
        {
            throw new Exception("Unsupported action type.");
        }
    }

    /// <summary>
    /// End-of-turn effects: burn, toxic, poison, weather damage.
    /// Mirrors pbs-unity end-of-turn status damage sequence.
    /// </summary>
    private async Task ApplyEndOfTurnEffectsAsync(BattleSession battle, List<BattleEvent> events)
    {
        // Weather tick
        if (battle.Weather != WeatherCondition.None && battle.WeatherTurnsLeft > 0)
        {
            battle.WeatherTurnsLeft--;
            if (battle.WeatherTurnsLeft == 0)
            {
                events.Add(new WeatherEndedEvent { EndedWeather = battle.Weather });
                battle.Weather = WeatherCondition.None;
            }
        }

        // Apply end-of-turn status/weather damage for each active pokemon
        foreach (var (playerId, team, activeIndex) in new[]
        {
            (battle.Player1Id, battle.Team1, battle.ActiveIndex1),
            (battle.Player2Id, battle.Team2, battle.ActiveIndex2)
        })
        {
            if (activeIndex < 0 || activeIndex >= team.Count)
                continue;

            var pokemon = team[activeIndex];
            if (pokemon.IsFainted)
                continue;

            // Non-volatile status end-of-turn damage
            ApplyStatusEndOfTurnDamage(pokemon, playerId, events);

            // Weather damage (sandstorm/hail)
            await ApplyWeatherEndOfTurnDamageAsync(pokemon, playerId, battle, events);

            // Faint check after end-of-turn
            if (pokemon.IsFainted)
            {
                events.Add(new PokemonFaintEvent { PlayerId = playerId, PokemonName = GetDisplayName(pokemon) });
                TryAutoSwitch(battle, playerId, events);
            }
        }
    }

    private static void ApplyStatusEndOfTurnDamage(BattlePokemonSnapshot pokemon, string playerId,
        List<BattleEvent> events)
    {
        int dmg;
        switch (pokemon.NonVolatileStatus)
        {
            case PokemonStatusCondition.Burn:
                dmg = Math.Max(1, pokemon.MaxHp / 16);
                pokemon.CurrentHp = Math.Max(0, pokemon.CurrentHp - dmg);
                events.Add(new PokemonDamageEvent
                {
                    PlayerId = playerId,
                    PokemonName = GetDisplayName(pokemon),
                    Damage = dmg,
                    HpBefore = pokemon.CurrentHp + dmg,
                    HpAfter = pokemon.CurrentHp,
                    MaxHp = pokemon.MaxHp,
                    IsEndOfTurn = true
                });
                break;

            case PokemonStatusCondition.Poison:
                dmg = Math.Max(1, pokemon.MaxHp / 8);
                pokemon.CurrentHp = Math.Max(0, pokemon.CurrentHp - dmg);
                events.Add(new PokemonDamageEvent
                {
                    PlayerId = playerId,
                    PokemonName = GetDisplayName(pokemon),
                    Damage = dmg,
                    HpBefore = pokemon.CurrentHp + dmg,
                    HpAfter = pokemon.CurrentHp,
                    MaxHp = pokemon.MaxHp,
                    IsEndOfTurn = true
                });
                break;

            case PokemonStatusCondition.Toxic:
                // Escalating: 1/16, 2/16, 3/16... capped at 15/16
                var counter = Math.Clamp(pokemon.ToxicCounter, 1, 15);
                dmg = Math.Max(1, pokemon.MaxHp * counter / 16);
                pokemon.CurrentHp = Math.Max(0, pokemon.CurrentHp - dmg);
                pokemon.ToxicCounter = Math.Min(15, pokemon.ToxicCounter + 1);
                events.Add(new PokemonDamageEvent
                {
                    PlayerId = playerId,
                    PokemonName = GetDisplayName(pokemon),
                    Damage = dmg,
                    HpBefore = pokemon.CurrentHp + dmg,
                    HpAfter = pokemon.CurrentHp,
                    MaxHp = pokemon.MaxHp,
                    IsEndOfTurn = true
                });
                break;
        }
    }

    private async Task ApplyWeatherEndOfTurnDamageAsync(BattlePokemonSnapshot pokemon, string playerId,
        BattleSession battle, List<BattleEvent> events)
    {
        if (battle.Weather != WeatherCondition.Sandstorm && battle.Weather != WeatherCondition.Hail)
            return;

        var entry = await _db.Pokedex.Find(p => p.Id == pokemon.SpeciesId).FirstOrDefaultAsync();
        var types = entry?.Types?.Select(NormalizeType).ToList() ?? new List<string>();

        // Sandstorm: Rock, Steel, Ground are immune
        if (battle.Weather == WeatherCondition.Sandstorm
            && (types.Contains("rock") || types.Contains("steel") || types.Contains("ground")))
            return;

        // Hail: Ice is immune
        if (battle.Weather == WeatherCondition.Hail && types.Contains("ice"))
            return;

        var dmg = Math.Max(1, pokemon.MaxHp / 16);
        pokemon.CurrentHp = Math.Max(0, pokemon.CurrentHp - dmg);
        events.Add(new WeatherDamageEvent
        {
            PlayerId = playerId,
            PokemonName = GetDisplayName(pokemon),
            Damage = dmg,
            Weather = battle.Weather
        });
    }

    /// <summary>
    /// Converts typed events to legacy string list for log persistence and backward compat.
    /// </summary>
    private static List<string> TypedEventsToStrings(List<BattleEvent> typedEvents) =>
        typedEvents.Select(e => e switch
        {
            MoveUsedEvent m         => $"[{m.PokemonName}] used {m.MoveName}.",
            MoveMissedEvent m       => $"[{m.PokemonName}] used {m.MoveName} but missed.",
            MoveNoEffectEvent m     => $"It had no effect on [{m.TargetName}].",
            PokemonDamageEvent d    => d.IsEndOfTurn
                                        ? $"[{d.PokemonName}] took {d.Damage} end-of-turn damage. ({d.HpAfter}/{d.MaxHp} HP)"
                                        : $"[{d.PokemonName}] took {d.Damage} damage. ({d.HpAfter}/{d.MaxHp} HP)",
            PokemonFaintEvent f     => $"[{f.PokemonName}] fainted.",
            PokemonHealEvent h      => $"[{h.PokemonName}] restored {h.HealAmount} HP.",
            SuperEffectiveEvent _   => "It's super effective!",
            NotVeryEffectiveEvent _ => "It's not very effective...",
            SwitchEvent s           => s.IsAutoSwitch
                                        ? $"[{s.PlayerId}] auto-switched to {s.SentOutPokemonName}."
                                        : $"[{s.PlayerId}] withdrew {s.WithdrawnPokemonName} and sent out {s.SentOutPokemonName}.",
            StatusInflictedEvent s  => $"[{s.PokemonName}] was inflicted with {s.Status}.",
            StatusHealedEvent s     => $"[{s.PokemonName}] was cured of {s.Status}.",
            StatusBlockedEvent s    => $"[{s.PokemonName}] status blocked: {s.Reason}.",
            ParalysisStuckEvent p   => $"[{p.PokemonName}] is fully paralyzed and cannot move!",
            SleepSkipEvent s        => $"[{s.PokemonName}] is fast asleep.",
            FreezeThawEvent f       => $"[{f.PokemonName}] thawed out!",
            StatChangeEvent s       => s.Stages > 0
                                        ? $"[{s.PokemonName}]'s {s.Stat} rose{(s.Stages >= 2 ? " sharply" : "")}!"
                                        : $"[{s.PokemonName}]'s {s.Stat} fell{(s.Stages <= -2 ? " harshly" : "")}!",
            StatChangeBlockedEvent s=> $"[{s.PokemonName}]'s {s.Stat} {s.Reason}.",
            WeatherChangedEvent w   => $"The weather changed to {w.NewWeather}.",
            WeatherEndedEvent w     => $"The {w.EndedWeather} subsided.",
            WeatherDamageEvent w    => $"[{w.PokemonName}] is buffeted by the {w.Weather} for {w.Damage} damage.",
            BattleEndEvent b        => b.WinnerPlayerId != null
                                        ? $"Battle ended. Winner: {b.WinnerPlayerId}"
                                        : "Battle ended in a draw.",
            MessageEvent m          => m.Message,
            _                       => e.EventType
        }).ToList();

    private record OrderedAction(BattleAction Action, int Priority, int Speed, int Tiebreaker);
    private record DamageOutcome(int Damage, int AttackStat, int DefenseStat, double TypeMultiplier);

    private static void ValidateBattleOptions(BattleOptions options)
    {
        if (options.MaxPartySize <= 0)
            throw new InvalidOperationException("Battle:MaxPartySize must be positive.");
        if (options.TurnTimeoutSeconds <= 0)
            throw new InvalidOperationException("Battle:TurnTimeoutSeconds must be positive.");
        if (options.DamageRandomMin <= 0 || options.DamageRandomMin > options.DamageRandomMax)
            throw new InvalidOperationException("Battle damage random range is invalid.");
    }
}
