using System.Collections.Concurrent;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

public class BattleService
{
    private readonly MongoDbContext _db;
    private static readonly ConcurrentDictionary<string, BattleSession> _battles = new();

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

        var session = new BattleSession
        {
            BattleId = Guid.NewGuid().ToString("N"),
            State = BattleState.Running,
            TurnNumber = 1,
            Player1Id = player1Id,
            Player2Id = player2Id,
            Team1 = ToSnapshots(team1),
            Team2 = ToSnapshots(team2),
            ActiveIndex1 = 0,
            ActiveIndex2 = 0
        };

        _battles[session.BattleId] = session;
        return session;
    }

    public BattleSession? GetBattle(string battleId)
    {
        _battles.TryGetValue(battleId, out var battle);
        return battle;
    }

    public void SubmitAction(string battleId, BattleAction action)
    {
        if (!_battles.TryGetValue(battleId, out var battle))
            throw new Exception("Battle not found.");

        if (battle.State != BattleState.Running)
            throw new Exception("Battle is not running.");

        if (action.PlayerId != battle.Player1Id && action.PlayerId != battle.Player2Id)
            throw new Exception("Player is not part of this battle.");

        if (battle.PendingActions.ContainsKey(action.PlayerId))
            throw new Exception("Action already submitted for this turn.");

        ValidateAction(action);
        battle.PendingActions[action.PlayerId] = action;
    }

    public bool IsTurnReady(string battleId)
    {
        if (!_battles.TryGetValue(battleId, out var battle))
            return false;

        return battle.PendingActions.ContainsKey(battle.Player1Id)
            && battle.PendingActions.ContainsKey(battle.Player2Id);
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
}
