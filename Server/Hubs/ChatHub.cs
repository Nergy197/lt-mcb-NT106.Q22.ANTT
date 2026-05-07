using Microsoft.AspNetCore.SignalR;
using PokemonMMO.Services;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Data;
using PokemonMMO.Models;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace PokemonMMO.Hubs;

public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly MongoDbContext _db;
    private readonly FriendService _friendService;

    // Maps PlayerId -> ConnectionId
    private static readonly ConcurrentDictionary<string, string> PlayerConnections = new();
    // Maps ConnectionId -> PlayerId
    private static readonly ConcurrentDictionary<string, string> OnlinePlayers = new();

    public ChatHub(ChatService chatService, MongoDbContext db, FriendService friendService)
    {
        _chatService = chatService;
        _db = db;
        _friendService = friendService;
    }

    // Các hàm Static Helper cho Controller sử dụng
    public static bool IsPlayerOnline(string playerId) => PlayerConnections.ContainsKey(playerId);
    public static string? GetConnectionId(string playerId) => PlayerConnections.TryGetValue(playerId, out var connId) ? connId : null;

    public override async Task OnConnectedAsync()
    {
        var player = await ResolveCurrentPlayer();
        if (player != null)
        {
            PlayerConnections[player.Id] = Context.ConnectionId;
            OnlinePlayers[Context.ConnectionId] = player.Id;
            await Groups.AddToGroupAsync(Context.ConnectionId, "WorldChat");
            Console.WriteLine($"[ChatHub] Player {player.Name} connected ({Context.ConnectionId})");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (OnlinePlayers.TryRemove(Context.ConnectionId, out var playerId))
        {
            PlayerConnections.TryRemove(playerId, out _);
            Console.WriteLine($"[ChatHub] Player {playerId} disconnected");
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<Player?> ResolveCurrentPlayer()
    {
        var httpContext = Context.GetHttpContext();
        var playerId = httpContext?.Request.Query["playerId"].ToString();
        if (string.IsNullOrEmpty(playerId)) return null;

        return await _db.Players
            .Find(p => p.Id == playerId)
            .FirstOrDefaultAsync();
    }

    // ── World Chat ───────────────────────────────────────────────────────

    public async Task SendWorldMessage(string content)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;

        try
        {
            var dto = await _chatService.SaveWorldMessageAsync(player.Id, player.Name, content);
            await Clients.Group("WorldChat").SendAsync("WorldMessage", dto);
        }
        catch (ArgumentException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public async Task LoadWorldHistory()
    {
        var history = await _chatService.GetWorldHistoryAsync();
        foreach (var msg in history)
        {
            await Clients.Caller.SendAsync("ReceiveHistoryMessage", msg);
        }
        Console.WriteLine($"[ChatHub] Đã stream {history.Count} tin nhắn lịch sử Thế giới.");
    }

    // ── Direct Messages ──────────────────────────────────────────────────

    public async Task SendDirectMessage(string receiverPlayerId, string content)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;

        var areFriends = await _friendService.AreFriendsAsync(player.Id, receiverPlayerId);
        if (!areFriends)
        {
            await Clients.Caller.SendAsync("Error", "Bạn chỉ có thể nhắn tin cho bạn bè.");
            return;
        }

        try
        {
            var dto = await _chatService.SaveDirectMessageAsync(player.Id, player.Name, receiverPlayerId, content);
            
            await Clients.Caller.SendAsync("DirectMessage", dto);
            if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
            {
                await Clients.Client(receiverConnId).SendAsync("DirectMessage", dto);
            }
        }
        catch (ArgumentException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public async Task LoadDirectHistory(string otherPlayerId)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;
 
        var history = await _chatService.GetDirectHistoryAsync(player.Id, otherPlayerId);
        foreach (var msg in history)
        {
            await Clients.Caller.SendAsync("ReceiveHistoryMessage", msg);
        }
        Console.WriteLine($"[ChatHub] Đã stream {history.Count} tin nhắn lịch sử DM.");
    }

    public async Task StartTyping(string receiverPlayerId)
    {
        if (OnlinePlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
            {
                await Clients.Client(receiverConnId).SendAsync("TypingIndicator", new { PlayerId = playerId, IsTyping = true });
            }
        }
    }

    public async Task StopTyping(string receiverPlayerId)
    {
        if (OnlinePlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
            {
                await Clients.Client(receiverConnId).SendAsync("TypingIndicator", new { PlayerId = playerId, IsTyping = false });
            }
        }
    }
}
