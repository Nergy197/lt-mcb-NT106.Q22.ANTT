// SignalRBattleAdapter.cs
// Thay thế Mirror networking của pbs-unity bằng SignalR (Microsoft.AspNetCore.SignalR.Client).
//
// ── Cài đặt package (một lần) ────────────────────────────────────────────────
// 1. Cài NuGet For Unity: https://github.com/GlitchEnzo/NuGetForUnity
// 2. Trong Unity: NuGet → Manage NuGet Packages → tìm "Microsoft.AspNetCore.SignalR.Client"
// 3. Install version 8.x (tương thích .NET Standard 2.1 / Unity 2021+)
//
// Hoặc download DLL thủ công và đặt vào Assets/Plugins/:
//   Microsoft.AspNetCore.SignalR.Client.dll
//   Microsoft.AspNetCore.Http.Connections.Client.dll
//   Microsoft.Extensions.Logging.dll
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;

namespace PokemonMMO.Battle
{
    /// <summary>
    /// Quản lý kết nối SignalR đến BattleHub và MatchmakingHub.
    /// Thay thế hoàn toàn Mirror networking của pbs-unity.
    ///
    /// Cách dùng:
    ///   1. Thêm component này vào một GameObject trong Battle Scene.
    ///   2. Set serverUrl trong Inspector.
    ///   3. Gọi Initialize(token, playerId) sau khi đăng nhập.
    ///   4. Subscribe các events (OnBattleSync, OnTurnResolved, ...).
    ///   5. Gọi ConnectToBattleAsync(battleId) khi trận bắt đầu.
    /// </summary>
    public class SignalRBattleAdapter : MonoBehaviour
    {
        [Header("Server")]
        [Tooltip("URL của server, ví dụ: http://127.0.0.1:2567")]
        public string serverUrl = "http://127.0.0.1:2567";

        // ── State ─────────────────────────────────────────────────────────────
        private HubConnection _battleHub;
        private string _jwtToken;
        private string _playerId;
        private string _currentBattleId;

        // ── Thread-safe queue để marshal callbacks về main thread ─────────────
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        // ── Public C# events (tương đương Mirror ClientRpc) ──────────────────

        /// <summary>Server gửi toàn bộ battle state (khi join/reconnect).</summary>
        public event Action<BattleSession> OnBattleSync;

        /// <summary>Server gửi kết quả turn (sau khi cả 2 submit action).</summary>
        public event Action<BattleTurnResult> OnTurnResolved;

        /// <summary>Server thông báo trận kết thúc.</summary>
        public event Action<string, string> OnBattleEnded; // (winnerPlayerId, reason)

        /// <summary>Server xác nhận action của mình đã được nhận.</summary>
        public event Action<string> OnActionAccepted; // "Move" hoặc "Switch"

        /// <summary>Kết nối thành công/thất bại.</summary>
        public event Action<bool, string> OnConnectionChanged; // (connected, message)

        /// <summary>Lỗi từ server (action invalid, v.v.).</summary>
        public event Action<string> OnServerError;

        // ── Connection state ──────────────────────────────────────────────────
        public bool IsConnected => _battleHub?.State == HubConnectionState.Connected;
        public string CurrentBattleId => _currentBattleId;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Update()
        {
            // Drain queue trên main thread
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                    _mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        private void OnDestroy()
        {
            DisconnectAsync().Forget();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Khởi tạo adapter với JWT token sau khi login.</summary>
        public void Initialize(string jwtToken, string playerId)
        {
            _jwtToken = jwtToken;
            _playerId = playerId;
        }

        /// <summary>
        /// Kết nối đến BattleHub và join vào trận đấu.
        /// Gọi sau khi MatchmakingHub thông báo BattleStarted.
        /// </summary>
        public async Task ConnectToBattleAsync(string battleId)
        {
            _currentBattleId = battleId;

            if (_battleHub != null)
                await DisconnectAsync();

            _battleHub = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/battleHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_jwtToken);
                })
                .WithAutomaticReconnect()
                .Build();

            RegisterBattleHubHandlers();

            _battleHub.Reconnecting += error =>
            {
                Enqueue(() => OnConnectionChanged?.Invoke(false, "Reconnecting..."));
                return Task.CompletedTask;
            };
            _battleHub.Reconnected += connId =>
            {
                Enqueue(() => OnConnectionChanged?.Invoke(true, "Reconnected."));
                // Re-join battle group sau reconnect
                _ = JoinBattleAsync(battleId);
                return Task.CompletedTask;
            };
            _battleHub.Closed += error =>
            {
                var msg = error?.Message ?? "Connection closed.";
                Enqueue(() => OnConnectionChanged?.Invoke(false, msg));
                return Task.CompletedTask;
            };

            try
            {
                await _battleHub.StartAsync();
                Enqueue(() => OnConnectionChanged?.Invoke(true, "Connected to BattleHub."));
                await JoinBattleAsync(battleId);
            }
            catch (Exception ex)
            {
                Enqueue(() => OnConnectionChanged?.Invoke(false, ex.Message));
                Debug.LogError($"[BattleAdapter] Failed to connect: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_battleHub == null) return;
            try { await _battleHub.StopAsync(); }
            catch { /* ignore */ }
            try { await _battleHub.DisposeAsync(); }
            catch { /* ignore */ }
            _battleHub = null;
        }

        // ── Battle actions (tương đương pbs-unity SendCommandToServer) ────────

        /// <summary>Gửi lệnh dùng move (slot 0–3).</summary>
        public async Task ChooseMoveAsync(int moveSlot)
        {
            if (!EnsureConnected()) return;
            try
            {
                await _battleHub.InvokeAsync("ChooseMove", _currentBattleId, moveSlot);
            }
            catch (Exception ex)
            {
                Enqueue(() => OnServerError?.Invoke(ex.Message));
            }
        }

        /// <summary>Gửi lệnh switch Pokémon (party index 0–5).</summary>
        public async Task SwitchPokemonAsync(int partyIndex)
        {
            if (!EnsureConnected()) return;
            try
            {
                await _battleHub.InvokeAsync("SwitchPokemon", _currentBattleId, partyIndex);
            }
            catch (Exception ex)
            {
                Enqueue(() => OnServerError?.Invoke(ex.Message));
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task JoinBattleAsync(string battleId)
        {
            try
            {
                await _battleHub.InvokeAsync("JoinBattle", battleId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleAdapter] JoinBattle failed: {ex.Message}");
            }
        }

        private void RegisterBattleHubHandlers()
        {
            // BattleSync — nhận trực tiếp object BattleSession
            _battleHub.On<BattleSession>("BattleSync", session =>
            {
                if (session != null)
                    Enqueue(() => OnBattleSync?.Invoke(session));
            });

            // TurnResolved — nhận JsonElement để handle polymorphism thủ công
            _battleHub.On<System.Text.Json.JsonElement>("TurnResolved", data =>
            {
                // Parse các field cơ bản trước
                string rawJson = data.GetRawText();
                var result = JsonUtility.FromJson<BattleTurnResult>(rawJson);
                
                if (result != null)
                {
                    // Trích xuất mảng TypedEvents thô để parse polymorphism
                    if (data.TryGetProperty("TypedEvents", out var eventsProp))
                    {
                        string eventsJson = eventsProp.GetRawText();
                        result.typedEvents = BattleEventParser.ParseEventArray(eventsJson);
                    }
                    
                    Enqueue(() => OnTurnResolved?.Invoke(result));
                }
            });

            // ActionAccepted — nhận object nặc danh dạng JsonElement
            _battleHub.On<System.Text.Json.JsonElement>("ActionAccepted", data =>
            {
                string action = "Unknown";
                if (data.TryGetProperty("Action", out var prop))
                    action = prop.GetString();
                
                Enqueue(() => OnActionAccepted?.Invoke(action));
            });

            // BattleEnded — nhận object BattleEndedEventDto dạng JsonElement
            _battleHub.On<System.Text.Json.JsonElement>("BattleEnded", data =>
            {
                string winner = null;
                string reason = "";
                
                if (data.TryGetProperty("WinnerPlayerId", out var wProp))
                    winner = wProp.GetString();
                
                if (data.TryGetProperty("Reason", out var rProp))
                    reason = rProp.GetString();

                Enqueue(() => OnBattleEnded?.Invoke(winner, reason));
            });

            // Error — lỗi từ server
            _battleHub.On<string>("Error", message =>
            {
                Enqueue(() => OnServerError?.Invoke(message));
            });
        }

        private BattleSession ParseBattleSession(string json)
        {
            try { return JsonUtility.FromJson<BattleSession>(json); }
            catch (Exception ex) { Debug.LogWarning($"[BattleAdapter] ParseBattleSession: {ex.Message}"); return null; }
        }

        private BattleTurnResult ParseTurnResult(string json)
        {
            try
            {
                var result = JsonUtility.FromJson<BattleTurnResult>(json);
                // Parse TypedEvents manually (JsonUtility không handle polymorphism)
                var typedEventsJson = ExtractArrayField(json, "TypedEvents")
                                   ?? ExtractArrayField(json, "typedEvents");
                if (!string.IsNullOrEmpty(typedEventsJson))
                    result.typedEvents = BattleEventParser.ParseEventArray(typedEventsJson);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleAdapter] ParseTurnResult: {ex.Message}");
                return null;
            }
        }

        /// <summary>Extracts a string field from a flat JSON object (not nested).</summary>
        private static string ExtractField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var pattern = $@"""{key}""\s*:\s*(?:""([^""]*)""|null)";
            var m = System.Text.RegularExpressions.Regex.Match(json, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Extracts a JSON array as raw string from a JSON object.</summary>
        private static string ExtractArrayField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var pattern = $@"""{key}""\s*:\s*(\[)";
            var m = System.Text.RegularExpressions.Regex.Match(json, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            int start = m.Groups[1].Index;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            }
            return null;
        }

        private bool EnsureConnected()
        {
            if (IsConnected) return true;
            Enqueue(() => OnServerError?.Invoke("Not connected to server."));
            return false;
        }

        private void Enqueue(Action action)
        {
            lock (_queueLock)
                _mainThreadQueue.Enqueue(action);
        }
    }

    // ── Extension để fire-and-forget Task mà không cần await ─────────────────
    internal static class TaskExtensions
    {
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (Exception ex) { Debug.LogWarning($"[TaskExtensions.Forget] {ex.Message}"); }
        }
    }
}
