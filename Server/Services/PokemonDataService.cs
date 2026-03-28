using System.Text.Json;

namespace PokemonMMO.Services;

/// <summary>
/// Fetches static Pokemon species data from PokeAPI and caches in memory.
/// Production: replace with Redis or a local JSON database.
/// </summary>
public class PokemonDataService
{
    private readonly HttpClient _http = new();
    private readonly Dictionary<int, PokemonBaseData> _cache = new();

    /// <summary>
    /// Get base data for a species (cached after first fetch).
    /// </summary>
    public async Task<PokemonBaseData> GetPokemonData(int speciesId)
    {
        if (_cache.TryGetValue(speciesId, out var cached))
            return cached;

        Console.WriteLine($"[StaticData] Fetching PokeAPI data for Species #{speciesId}...");

        var response = await _http.GetAsync($"https://pokeapi.co/api/v2/pokemon/{speciesId}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int GetStat(string name)
        {
            foreach (var stat in root.GetProperty("stats").EnumerateArray())
            {
                if (stat.GetProperty("stat").GetProperty("name").GetString() == name)
                    return stat.GetProperty("base_stat").GetInt32();
            }
            return 0;
        }

        var baseData = new PokemonBaseData
        {
            Id       = root.GetProperty("id").GetInt32(),
            Name     = root.GetProperty("name").GetString() ?? "unknown",
            BaseHp   = GetStat("hp"),
            BaseAtk  = GetStat("attack"),
            BaseDef  = GetStat("defense"),
            BaseSpAtk = GetStat("special-attack"),
            BaseSpDef = GetStat("special-defense"),
            BaseSpd  = GetStat("speed")
        };

        _cache[speciesId] = baseData;
        return baseData;
    }

    /// <summary>
    /// Pre-load common Pokemon into cache (e.g. starters).
    /// </summary>
    public async Task PreWarm(int[] ids)
    {
        foreach (var id in ids)
            await GetPokemonData(id);

        Console.WriteLine($"[StaticData] Pre-warmed {ids.Length} Pokemon into memory.");
    }
}

/// <summary>
/// Static species data fetched from PokeAPI.
/// </summary>
public class PokemonBaseData
{
    public int    Id       { get; set; }
    public string Name     { get; set; } = "";
    public int    BaseHp   { get; set; }
    public int    BaseAtk  { get; set; }
    public int    BaseDef  { get; set; }
    public int    BaseSpAtk { get; set; }
    public int    BaseSpDef { get; set; }
    public int    BaseSpd  { get; set; }
}
