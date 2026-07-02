using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using PokemonMMO.Options;

namespace PokemonMMO.Hubs;

/// <summary>
/// Thư mục Hubs/ - MatchmakingHub (Tìm trận)
/// Xử lý việc người chơi tham gia Lobby và bắt đầu trận đấu 1v1.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MatchmakingHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly BattleService _battleService;
    private readonly GameService _gameService;
    private readonly BattleOptions _opts;

    // In-memory player tracking (sessionId → playerId)
    public static readonly ConcurrentDictionary<string, string> ConnectedPlayers = new();

    // Hàng chờ tìm trận (PlayerId -> ConnectionId)
    private static readonly ConcurrentDictionary<string, string> MatchmakingQueue = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> MatchmakingTasks = new();

    private static readonly ConcurrentDictionary<string, string> CasualQueue = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> CasualTasks = new();

    // Phòng riêng: mã phòng → (hostPlayerId, hostConnectionId, thời điểm tạo)
    private static readonly ConcurrentDictionary<string, (string PlayerId, string ConnId, DateTime CreatedUtc)> PrivateRooms = new();

    // Phòng riêng tự hết hạn nếu không ai vào sau khoảng thời gian này
    private static readonly TimeSpan PrivateRoomTtl = TimeSpan.FromMinutes(5);

    // Mutex để tránh race condition khi 2 người cùng tìm trận
    private static readonly SemaphoreSlim _matchLock = new SemaphoreSlim(1, 1);

    public MatchmakingHub(MongoDbContext db, BattleService battleService, GameService gameService, IOptions<BattleOptions> opts)
    {
        _db = db;
        _battleService = battleService;
        _gameService = gameService;
        _opts = opts.Value;
    }

    public async Task JoinLobby()
    {
        var player = await GetAuthenticatedPlayer();
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Unable to resolve player from token.");
            Context.Abort();
            return;
        }

        if (!ConnectedPlayers.TryAdd(Context.ConnectionId, player.Id))
        {
            ConnectedPlayers[Context.ConnectionId] = player.Id;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        await Clients.Group("Lobby").SendAsync("PlayerJoined", new PlayerJoinedEventDto
        {
            SessionId = Context.ConnectionId,
            Id = player.Id,
            Name = player.Name
        });

        // Đồng bộ Party khi vừa vào Lobby
        await SyncPartyToClient(player.Id);
    }

    public async Task Heal()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        try
        {
            await _gameService.HealPlayerParty(playerId);
            await SyncPartyToClient(playerId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to heal party: {ex.Message}");
        }
    }

    private async Task SyncPartyToClient(string playerId)
    {
        var party = await _db.PokemonInstances
            .Find(Builders<PokemonInstance>.Filter.And(
                Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, playerId),
                Builders<PokemonInstance>.Filter.Eq(p => p.IsInParty, true)))
            .ToListAsync();

        await Clients.Caller.SendAsync("Debug", $"[Server] Syncing party for {playerId}, count: {party.Count}");

        var partyData = party.Select(p => new PartyPokemonDto
        {
            Id = p.Id,
            SpeciesId = p.SpeciesId,
            Level = p.Level,
            Hp = p.CurrentHp,
            MaxHp = p.MaxHp
        }).ToList();

        await Clients.Caller.SendAsync("PartyUpdated", partyData);
    }

    public async Task StartMatch(string opponentPlayerId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        var opponentConnectionId = ConnectedPlayers
            .FirstOrDefault(kv => kv.Value == opponentPlayerId).Key;

        if (string.IsNullOrWhiteSpace(opponentConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "Opponent is not online.");
            return;
        }

        try
        {
            await CreateAndNotifyBattle(myPlayerId, opponentPlayerId, Context.ConnectionId, opponentConnectionId, BattleMode.Private);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to start match: {ex.Message}");
        }
    }

    /// <summary>
    /// Tạo bot battle ngay lập tức, không cần đợi 30s. Dùng cho demo / testing.
    /// </summary>
    public async Task<string> FightBot()
    {
        // Ưu tiên ConnectedPlayers (nếu đã JoinLobby), fallback sang DB qua JWT
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            var player = await GetAuthenticatedPlayer();
            if (player == null) { await Clients.Caller.SendAsync("Error", "Not authenticated."); return ""; }
            playerId = player.Id;
            ConnectedPlayers[Context.ConnectionId] = playerId;
        }

        return await CreateAndNotifyBattle(playerId, BattleService.BotPlayerId, Context.ConnectionId, null);
    }

    public async Task FindCasualMatch()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        CasualQueue[myPlayerId] = Context.ConnectionId;

        int countdown = _opts.BotFallbackSeconds;
        await Clients.Caller.SendAsync("SearchStarted", new SearchStartedDto { CountdownSeconds = countdown });

        var myCts = new CancellationTokenSource();
        CasualTasks[myPlayerId] = myCts;

        try
        {
            for (int i = countdown; i > 0; i--)
            {
                string opponentId = null;
                string myConnId = null;
                string oppConnId = null;

                await _matchLock.WaitAsync(myCts.Token);
                try
                {
                    if (!CasualQueue.ContainsKey(myPlayerId))
                        return;

                    var opponent = CasualQueue.FirstOrDefault(kv => kv.Key != myPlayerId);
                    if (opponent.Key != null)
                    {
                        CasualQueue.TryRemove(myPlayerId, out myConnId);
                        CasualQueue.TryRemove(opponent.Key, out oppConnId);
                        opponentId = opponent.Key;
                        if (CasualTasks.TryRemove(opponentId, out var oppCts)) oppCts.Cancel();
                        CasualTasks.TryRemove(myPlayerId, out _);
                    }
                }
                finally
                {
                    _matchLock.Release();
                }

                if (opponentId != null)
                {
                    await CreateAndNotifyBattle(myPlayerId, opponentId, myConnId, oppConnId, BattleMode.Casual);
                    return;
                }

                await Clients.Caller.SendAsync("SearchTick", new SearchTickDto { SecondsLeft = i });
                await Task.Delay(1000, myCts.Token);
            }

            string finalConnId = null;
            bool shouldFightBot = false;
            await _matchLock.WaitAsync();
            try
            {
                if (CasualQueue.TryRemove(myPlayerId, out finalConnId))
                {
                    CasualTasks.TryRemove(myPlayerId, out _);
                    shouldFightBot = true;
                }
            }
            finally
            {
                _matchLock.Release();
            }

            if (shouldFightBot)
                await CreateAndNotifyBattle(myPlayerId, BattleService.BotPlayerId, finalConnId, null, BattleMode.Casual);
        }
        catch (OperationCanceledException) { }
    }

    public async Task<string> CreatePrivateRoom()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return "";
        }

        PrunePrivateRooms();

        // Mỗi người chỉ giữ một phòng — xóa phòng cũ (nếu có)
        foreach (var kv in PrivateRooms.Where(kv => kv.Value.PlayerId == playerId).ToList())
            PrivateRooms.TryRemove(kv.Key, out _);

        var code = GenerateRoomCode();
        PrivateRooms[code] = (playerId, Context.ConnectionId, DateTime.UtcNow);
        return code;
    }

    public async Task JoinPrivateRoom(string code)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        PrunePrivateRooms();

        code = code.Trim();
        if (!PrivateRooms.TryRemove(code, out var host))
        {
            await Clients.Caller.SendAsync("Error", "Mã phòng không tồn tại hoặc đã hết hạn.");
            return;
        }

        // Phòng đã quá hạn giữa lúc kiểm tra — coi như không tồn tại
        if (DateTime.UtcNow - host.CreatedUtc > PrivateRoomTtl)
        {
            await Clients.Caller.SendAsync("Error", "Mã phòng không tồn tại hoặc đã hết hạn.");
            return;
        }

        if (host.PlayerId == myPlayerId)
        {
            PrivateRooms[code] = host;
            await Clients.Caller.SendAsync("Error", "Không thể tự tham gia phòng của mình.");
            return;
        }

        // Lấy ConnectionId hiện tại của chủ phòng (có thể đã đổi do reconnect)
        var hostConnId = ConnectedPlayers.FirstOrDefault(kv => kv.Value == host.PlayerId).Key;
        if (string.IsNullOrEmpty(hostConnId))
        {
            await Clients.Caller.SendAsync("Error", "Chủ phòng hiện không trực tuyến.");
            return;
        }

        await CreateAndNotifyBattle(host.PlayerId, myPlayerId, hostConnId, Context.ConnectionId, BattleMode.Private);
    }

    public async Task CancelPrivateRoom()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        foreach (var kv in PrivateRooms.Where(kv => kv.Value.PlayerId == playerId).ToList())
            PrivateRooms.TryRemove(kv.Key, out _);

        await Clients.Caller.SendAsync("PrivateRoomCancelled");
    }

    /// <summary>Dọn các phòng riêng đã quá hạn (không ai vào).</summary>
    private static void PrunePrivateRooms()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in PrivateRooms.Where(kv => now - kv.Value.CreatedUtc > PrivateRoomTtl).ToList())
            PrivateRooms.TryRemove(kv.Key, out _);
    }

    private static string GenerateRoomCode()
    {
        return new Random().Next(100000, 1000000).ToString();
    }

    public async Task FindMatch()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        MatchmakingQueue[myPlayerId] = Context.ConnectionId;

        int countdown = _opts.BotFallbackSeconds;
        await Clients.Caller.SendAsync("SearchStarted", new SearchStartedDto { CountdownSeconds = countdown });

        var myCts = new CancellationTokenSource();
        MatchmakingTasks[myPlayerId] = myCts;

        try
        {
            for (int i = countdown; i > 0; i--)
            {
                string opponentId = null;
                string myConnId = null;
                string oppConnId = null;

                await _matchLock.WaitAsync(myCts.Token);
                try
                {
                    // Nếu mình đã bị đối thủ khác "claim" và xóa khỏi hàng, thoát luôn
                    if (!MatchmakingQueue.ContainsKey(myPlayerId))
                        return;

                    var opponent = MatchmakingQueue.FirstOrDefault(kv => kv.Key != myPlayerId);
                    if (opponent.Key != null)
                    {
                        MatchmakingQueue.TryRemove(myPlayerId, out myConnId);
                        MatchmakingQueue.TryRemove(opponent.Key, out oppConnId);
                        opponentId = opponent.Key;
                        if (MatchmakingTasks.TryRemove(opponentId, out var oppCts)) oppCts.Cancel();
                        MatchmakingTasks.TryRemove(myPlayerId, out _);
                    }
                }
                finally
                {
                    _matchLock.Release();
                }

                if (opponentId != null)
                {
                    await CreateAndNotifyBattle(myPlayerId, opponentId, myConnId, oppConnId, BattleMode.Ranked);
                    return;
                }

                await Clients.Caller.SendAsync("SearchTick", new SearchTickDto { SecondsLeft = i });
                await Task.Delay(1000, myCts.Token);
            }

            // Hết thời gian chờ -> Đấu với Bot
            string finalConnId = null;
            bool shouldFightBot = false;
            await _matchLock.WaitAsync();
            try
            {
                if (MatchmakingQueue.TryRemove(myPlayerId, out finalConnId))
                {
                    MatchmakingTasks.TryRemove(myPlayerId, out _);
                    shouldFightBot = true;
                }
            }
            finally
            {
                _matchLock.Release();
            }

            if (shouldFightBot)
                await CreateAndNotifyBattle(myPlayerId, BattleService.BotPlayerId, finalConnId, null);
        }
        catch (OperationCanceledException)
        {
            // Trận đã được tìm thấy bởi người khác — họ xử lý tạo trận
        }
    }

    public async Task CancelMatchmaking()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
            return;

        bool cancelled = false;

        MatchmakingQueue.TryRemove(myPlayerId, out _);
        if (MatchmakingTasks.TryRemove(myPlayerId, out var cts))
        {
            cts.Cancel();
            cancelled = true;
        }

        CasualQueue.TryRemove(myPlayerId, out _);
        if (CasualTasks.TryRemove(myPlayerId, out var casualCts))
        {
            casualCts.Cancel();
            cancelled = true;
        }

        if (cancelled)
            await Clients.Caller.SendAsync("SearchCancelled", "Đã hủy tìm trận.");
    }

    private async Task<string> CreateAndNotifyBattle(
        string p1,
        string p2,
        string conn1,
        string? conn2,
        BattleMode mode = BattleMode.Casual)
    {
        var battle = await _battleService.CreateBattle(p1, p2, mode);
        var startDto = new BattleStartedEventDto
        {
            BattleId = battle.BattleId,
            Player1Id = battle.Player1Id,
            Player2Id = battle.Player2Id,
            TurnNumber = battle.TurnNumber,
            TurnTimeoutSeconds = _battleService.TurnTimeoutSeconds,
            TurnDeadlineUtc = battle.TurnDeadlineUtc,
            State = battle.State.ToString(),
            Mode = battle.Mode.ToString()
        };

        if (!string.IsNullOrEmpty(conn1))
            await Clients.Client(conn1).SendAsync("MatchFound", startDto);

        if (!string.IsNullOrEmpty(conn2))
            await Clients.Client(conn2).SendAsync("MatchFound", startDto);

        return battle.BattleId;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedPlayers.TryRemove(Context.ConnectionId, out var playerId))
        {
            MatchmakingQueue.TryRemove(playerId, out _);
            if (MatchmakingTasks.TryRemove(playerId, out var cts)) cts.Cancel();

            CasualQueue.TryRemove(playerId, out _);
            if (CasualTasks.TryRemove(playerId, out var casualCts)) casualCts.Cancel();

            foreach (var kv in PrivateRooms.Where(kv => kv.Value.PlayerId == playerId).ToList())
                PrivateRooms.TryRemove(kv.Key, out _);

            await Clients.Group("Lobby").SendAsync("PlayerLeft", new PlayerLeftEventDto
            {
                SessionId = Context.ConnectionId
            });
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<Player?> GetAuthenticatedPlayer()
    {
        if (Context.User == null) {
            Console.WriteLine("[Matchmaking] FAIL: Context.User is NULL");
            return null;
        }

        // Log tất cả các claims để debug
        foreach (var claim in Context.User.Claims) {
            Console.WriteLine($"[Matchmaking] Claim: {claim.Type} = {claim.Value}");
        }

        var accountId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
            ?? Context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) {
            Console.WriteLine("[Matchmaking] FAIL: AccountId not found in claims");
            return null;
        }

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();

        if (player == null) {
            Console.WriteLine($"[Matchmaking] FAIL: Player profile not found for AccountId: {accountId}");
            // Fallback: Tạo profile nếu thiếu
            var username = Context.User.Identity?.Name ?? "Guest_" + accountId[..5];
            player = new Player { AccountId = accountId, Name = username, MMR = 1000, RankPoints = 0 };
            await _db.Players.InsertOneAsync(player);
            Console.WriteLine($"[Matchmaking] FIXED: Created missing player profile for {username}");
        }

        return player;
    }
}
