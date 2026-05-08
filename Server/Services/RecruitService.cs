using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

/// <summary>
/// Recruit (Gacha) — random 10 Pokémon từ Pokédex cho người chơi.
/// Trả về dữ liệu species (tên, type, icon URL) để Client hiển thị.
/// Chỉ chọn Pokémon có icon file tồn tại trên server.
/// </summary>
public class RecruitService
{
    private readonly MongoDbContext _db;
    private readonly IWebHostEnvironment _env;
    private static readonly Random _rng = new();

    // Các hậu tố form cần loại bỏ khi tìm icon
    private static readonly string[] FormSuffixes =
    {
        "-galar", "-alola", "-hisui", "-paldea",
        "-origin", "-therian", "-incarnate",
        "-mega", "-mega-x", "-mega-y",
        "-primal", "-ash", "-starter",
        "-10", "-50", "-complete",
        "-blade", "-shield",
        "-average", "-large", "-small", "-super",
        "-attack", "-defense", "-speed",
        "-altered", "-sky", "-land",
        "-heat", "-wash", "-frost", "-fan", "-mow",
        "-sandy", "-trash", "-plant",
        "-sunshine", "-overcast", "-rainy", "-snowy",
        "-west", "-east",
        "-black", "-white",
        "-aria", "-pirouette",
        "-normal", "-zen",
        "-confined", "-unbound",
        "-school", "-solo",
        "-midday", "-midnight", "-dusk",
        "-gulping", "-gorging",
        "-ice", "-noice",
        "-rider", "-crowned",
        "-low-key", "-amped",
        "-single-strike", "-rapid-strike",
        "-baile", "-pau", "-pom-pom", "-sensu"
    };

    public RecruitService(MongoDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>
    /// Roll ngẫu nhiên 10 Pokémon từ toàn bộ Pokédex.
    /// Chỉ chọn Pokémon có icon tồn tại trên server.
    /// </summary>
    public async Task<List<RecruitResult>> RollAsync(int count = 10)
    {
        var allPokemon = await _db.Pokedex.Find(_ => true).ToListAsync();

        if (allPokemon.Count == 0)
            throw new InvalidOperationException("Pokédex chưa được seed. Hãy khởi động lại server.");

        // Lọc chỉ giữ Pokémon có icon file thực sự tồn tại
        var iconsDir = Path.Combine(_env.WebRootPath, "data", "pokemon", "icons");
        var validPokemon = new List<(PokedexEntry entry, string iconFileName)>();

        foreach (var p in allPokemon)
        {
            var iconFile = ResolveIconFileName(p.Name.ToLower(), iconsDir);
            if (iconFile != null)
                validPokemon.Add((p, iconFile));
        }

        if (validPokemon.Count == 0)
            throw new InvalidOperationException("Không tìm thấy icon nào trong wwwroot/data/pokemon/icons.");

        var results = new List<RecruitResult>();

        // Shuffle danh sách để chọn không trùng (Fisher-Yates)
        var shuffled = validPokemon.OrderBy(_ => _rng.Next()).ToList();

        for (int i = 0; i < count && i < shuffled.Count; i++)
        {
            var (pick, iconFile) = shuffled[i];

            results.Add(new RecruitResult
            {
                SpeciesId = pick.Id,
                Name      = pick.Name,
                Types     = pick.Types,
                IconUrl   = $"/data/pokemon/icons/{iconFile}",
                SpriteUrl = $"/data/pokemon/front/{iconFile}"
            });
        }

        return results;
    }

    /// <summary>
    /// Tìm file icon phù hợp: thử tên gốc trước, rồi bỏ hậu tố form.
    /// Trả về tên file nếu tìm thấy, null nếu không có.
    /// </summary>
    private string? ResolveIconFileName(string pokemonName, string iconsDir)
    {
        // 1. Thử tên gốc trước
        var fileName = $"{pokemonName}.png";
        if (File.Exists(Path.Combine(iconsDir, fileName)))
            return fileName;

        // 2. Bỏ hậu tố form (dài nhất trước để tránh xung đột)
        foreach (var suffix in FormSuffixes.OrderByDescending(s => s.Length))
        {
            if (pokemonName.EndsWith(suffix))
            {
                var baseName = pokemonName[..^suffix.Length];
                var baseFileName = $"{baseName}.png";
                if (File.Exists(Path.Combine(iconsDir, baseFileName)))
                    return baseFileName;
            }
        }

        return null; // Không tìm thấy icon → bỏ qua Pokémon này
    }
}

/// <summary>
/// DTO trả về Client khi roll recruit.
/// </summary>
public class RecruitResult
{
    public int          SpeciesId { get; set; }
    public string       Name      { get; set; } = "";
    public List<string> Types     { get; set; } = new();
    public string       IconUrl   { get; set; } = "";
    public string       SpriteUrl { get; set; } = "";
}
