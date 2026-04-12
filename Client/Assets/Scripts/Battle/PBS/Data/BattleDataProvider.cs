// BattleDataProvider.cs
// Thay thế pbs-unity's in-memory Databases (Databases/Moves.cs, Databases/Pokemon.cs, etc.)
// bằng HTTP API calls đến backend ASP.NET Core.
//
// Dữ liệu được cache trong session để tránh gọi API nhiều lần cho cùng 1 move/pokemon.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace PokemonMMO.Battle
{
    /// <summary>
    /// Cung cấp data về moves và pokemon từ backend API.
    /// Thay thế pbs-unity's Databases.Moves.instance và Databases.Pokemon.instance.
    ///
    /// Cách dùng:
    ///   var provider = GetComponent<BattleDataProvider>();
    ///   provider.Initialize(serverUrl, jwtToken);
    ///   var moveInfo = await provider.GetMoveAsync(moveId, currentPp, maxPp);
    /// </summary>
    public class BattleDataProvider : MonoBehaviour
    {
        [Header("Server")]
        public string serverUrl = "http://127.0.0.1:2567";

        private string _jwtToken;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // ── Caches (session-scoped) ───────────────────────────────────────────
        private readonly Dictionary<int, MoveApiResponse>    _moveCache    = new();
        private readonly Dictionary<int, PokemonApiResponse> _pokemonCache = new();

        // ── Thread-safe main thread queue ─────────────────────────────────────
        private readonly Queue<Action> _mainThread = new Queue<Action>();
        private readonly object _lock = new object();

        private void Update()
        {
            lock (_lock)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();
        }

        // ── Initialization ────────────────────────────────────────────────────

        public void Initialize(string serverUrl, string jwtToken)
        {
            this.serverUrl = serverUrl;
            _jwtToken = jwtToken;
        }

        // ── Move API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lấy thông tin move để hiển thị trên FightPanel.
        /// Trả về MoveDisplayInfo với name, type, category, power, accuracy, PP.
        /// </summary>
        public async Task<MoveDisplayInfo> GetMoveDisplayInfoAsync(int moveId, int currentPp)
        {
            var raw = await GetMoveRawAsync(moveId);
            return new MoveDisplayInfo
            {
                MoveId    = moveId,
                Name      = raw?.name ?? $"Move#{moveId}",
                Type      = (raw?.type ?? "normal").ToLowerInvariant(),
                Category  = raw?.category ?? "Physical",
                Power     = raw?.power ?? 0,
                Accuracy  = raw?.accuracy ?? 100,
                CurrentPp = currentPp,
                MaxPp     = raw?.pp ?? 10
            };
        }

        /// <summary>
        /// Lấy tất cả move display info cho một pokemon's moveset.
        /// </summary>
        public async Task<List<MoveDisplayInfo>> GetMovesetDisplayInfoAsync(
            List<PokemonMoveSlot> moves)
        {
            var result = new List<MoveDisplayInfo>();
            foreach (var slot in moves)
            {
                var info = await GetMoveDisplayInfoAsync(slot.MoveId, slot.CurrentPp);
                result.Add(info);
            }
            return result;
        }

        private async Task<MoveApiResponse> GetMoveRawAsync(int moveId)
        {
            if (_moveCache.TryGetValue(moveId, out var cached))
                return cached;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{serverUrl}/api/moves/{moveId}");
                if (!string.IsNullOrEmpty(_jwtToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var move = JsonUtility.FromJson<MoveApiResponse>(json);
                _moveCache[moveId] = move;
                return move;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleDataProvider] GetMove({moveId}): {ex.Message}");
                return null;
            }
        }

        // ── Pokemon API ───────────────────────────────────────────────────────

        /// <summary>
        /// Lấy tên loài Pokemon từ speciesId (dùng cho dialog box).
        /// </summary>
        public async Task<string> GetSpeciesNameAsync(int speciesId)
        {
            var raw = await GetPokemonRawAsync(speciesId);
            return raw?.name ?? $"Pokemon#{speciesId}";
        }

        /// <summary>
        /// Lấy danh sách types của pokemon (dùng để check STAB, weather immunity).
        /// </summary>
        public async Task<List<string>> GetPokemonTypesAsync(int speciesId)
        {
            var raw = await GetPokemonRawAsync(speciesId);
            return raw?.types ?? new List<string> { "normal" };
        }

        private async Task<PokemonApiResponse> GetPokemonRawAsync(int speciesId)
        {
            if (_pokemonCache.TryGetValue(speciesId, out var cached))
                return cached;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{serverUrl}/api/pokedex/{speciesId}");
                if (!string.IsNullOrEmpty(_jwtToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var pokemon = JsonUtility.FromJson<PokemonApiResponse>(json);
                _pokemonCache[speciesId] = pokemon;
                return pokemon;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleDataProvider] GetPokemon({speciesId}): {ex.Message}");
                return null;
            }
        }

        // ── Coroutine wrappers (dùng khi cần gọi từ MonoBehaviour khác) ───────

        /// <summary>Coroutine wrapper cho GetMoveDisplayInfoAsync.</summary>
        public IEnumerator GetMoveDisplayInfoCoroutine(int moveId, int currentPp,
            Action<MoveDisplayInfo> onComplete)
        {
            var task = GetMoveDisplayInfoAsync(moveId, currentPp);
            yield return new WaitUntil(() => task.IsCompleted);
            onComplete?.Invoke(task.IsCompletedSuccessfully ? task.Result : null);
        }

        /// <summary>Coroutine wrapper cho GetMovesetDisplayInfoAsync.</summary>
        public IEnumerator GetMovesetCoroutine(List<PokemonMoveSlot> moves,
            Action<List<MoveDisplayInfo>> onComplete)
        {
            var task = GetMovesetDisplayInfoAsync(moves);
            yield return new WaitUntil(() => task.IsCompleted);
            onComplete?.Invoke(task.IsCompletedSuccessfully ? task.Result : null);
        }

        public void ClearCache()
        {
            _moveCache.Clear();
            _pokemonCache.Clear();
        }

        // ── API response DTOs (must match server JSON field names) ────────────

        [Serializable]
        private class MoveApiResponse
        {
            public int    id;
            public string name;
            public int?   power;
            public int?   accuracy;
            public string type;
            public int    priority;
            public string category;
            public int    pp;
            public string effect;
        }

        [Serializable]
        private class PokemonApiResponse
        {
            public int          id;
            public string       name;
            public List<string> types;
        }
    }
}
