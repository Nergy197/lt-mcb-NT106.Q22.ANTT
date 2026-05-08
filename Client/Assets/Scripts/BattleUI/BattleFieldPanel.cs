using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Battle.UI
{
    /// <summary>
    /// HUD thường trú hiển thị điều kiện sân: thời tiết, địa hình, số lượt.
    /// Không phải BasePanel — luôn hiển thị trong suốt trận đấu.
    ///
    /// Layout yêu cầu (tự tìm qua transform.Find):
    ///   "TurnText"    — số lượt hiện tại
    ///   "WeatherRow"  — GameObject cha chứa icon + text thời tiết
    ///   "TerrainRow"  — GameObject cha chứa icon + text địa hình
    /// Hoặc gán public fields thủ công trong Inspector.
    /// </summary>
    public class BattleFieldPanel : MonoBehaviour
    {
        [Header("Text refs (tự tìm nếu để trống)")]
        public TextMeshProUGUI turnText;
        public TextMeshProUGUI weatherText;
        public TextMeshProUGUI terrainText;

        [Header("Row GameObjects (ẩn khi không có weather/terrain)")]
        public GameObject weatherRow;
        public GameObject terrainRow;

        // Màu text theo thời tiết
        private static readonly Dictionary<string, Color> WeatherColors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Sun",       new Color(1.00f, 0.80f, 0.20f) },
            { "HarshSun",  new Color(1.00f, 0.60f, 0.00f) },
            { "Rain",      new Color(0.40f, 0.70f, 1.00f) },
            { "HeavyRain", new Color(0.20f, 0.50f, 0.90f) },
            { "Sandstorm", new Color(0.80f, 0.65f, 0.35f) },
            { "Snow",      new Color(0.75f, 0.90f, 1.00f) },
            { "Hail",      new Color(0.70f, 0.88f, 1.00f) },
        };

        private static readonly Dictionary<string, Color> TerrainColors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Grassy",   new Color(0.35f, 0.85f, 0.35f) },
            { "Electric", new Color(0.95f, 0.90f, 0.20f) },
            { "Psychic",  new Color(0.90f, 0.40f, 0.80f) },
            { "Misty",    new Color(0.80f, 0.75f, 1.00f) },
        };

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (turnText    == null) turnText    = transform.Find("TurnText")?.GetComponent<TextMeshProUGUI>();
            if (weatherText == null)
            {
                var wr = transform.Find("WeatherRow");
                if (wr != null) weatherText = wr.Find("Text")?.GetComponent<TextMeshProUGUI>()
                                           ?? wr.GetComponentInChildren<TextMeshProUGUI>();
                if (weatherRow == null && wr != null) weatherRow = wr.gameObject;
            }
            if (terrainText == null)
            {
                var tr2 = transform.Find("TerrainRow");
                if (tr2 != null) terrainText = tr2.Find("Text")?.GetComponent<TextMeshProUGUI>()
                                            ?? tr2.GetComponentInChildren<TextMeshProUGUI>();
                if (terrainRow == null && tr2 != null) terrainRow = tr2.gameObject;
            }
        }

        private void OnEnable()  => BattleEvents.OnFieldConditionUpdated += Refresh;
        private void OnDisable() => BattleEvents.OnFieldConditionUpdated -= Refresh;

        // ── Render ──────────────────────────────────────────────────────────

        private void Refresh(FieldConditionData data)
        {
            if (turnText != null)
                turnText.text = $"LƯỢT {data.TurnNumber}";

            bool hasWeather = !string.IsNullOrEmpty(data.Weather)
                           && !data.Weather.Equals("None", System.StringComparison.OrdinalIgnoreCase);

            if (weatherRow != null) weatherRow.SetActive(hasWeather);
            if (weatherText != null)
            {
                weatherText.gameObject.SetActive(hasWeather);
                if (hasWeather)
                {
                    string turns = data.WeatherTurns > 0 ? $" ({data.WeatherTurns})" : "";
                    weatherText.text = $"{WeatherIcon(data.Weather)} {data.Weather}{turns}";
                    if (WeatherColors.TryGetValue(data.Weather, out var wc))
                        weatherText.color = wc;
                }
            }

            bool hasTerrain = !string.IsNullOrEmpty(data.Terrain)
                           && !data.Terrain.Equals("None", System.StringComparison.OrdinalIgnoreCase);

            if (terrainRow != null) terrainRow.SetActive(hasTerrain);
            if (terrainText != null)
            {
                terrainText.gameObject.SetActive(hasTerrain);
                if (hasTerrain)
                {
                    string turns = data.TerrainTurns > 0 ? $" ({data.TerrainTurns})" : "";
                    terrainText.text = $"{TerrainIcon(data.Terrain)} {data.Terrain} Terrain{turns}";
                    if (TerrainColors.TryGetValue(data.Terrain, out var tc))
                        terrainText.color = tc;
                }
            }
        }

        // ── Icon helpers ────────────────────────────────────────────────────

        private static string WeatherIcon(string w) => w?.ToLowerInvariant() switch
        {
            "sun"  or "harshsun"   => "☀",
            "rain" or "heavyrain"  => "🌧",
            "sandstorm"            => "🌪",
            "snow" or "hail"       => "❄",
            _ => "🌤",
        };

        private static string TerrainIcon(string t) => t?.ToLowerInvariant() switch
        {
            "grassy"   => "🌿",
            "electric" => "⚡",
            "psychic"  => "🔮",
            "misty"    => "🌫",
            _ => "◆",
        };
    }
}
