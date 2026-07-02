using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

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
    private readonly CurrencyService _currency;
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

    private static readonly string[] Natures =
    {
        "Hardy","Lonely","Brave","Adamant","Naughty",
        "Bold","Docile","Relaxed","Impish","Lax",
        "Timid","Hasty","Serious","Jolly","Naive",
        "Modest","Mild","Quiet","Bashful","Rash",
        "Calm","Gentle","Sassy","Careful","Quirky"
    };

    public RecruitService(MongoDbContext db, IWebHostEnvironment env, CurrencyService currency)
    {
        _db = db;
        _env = env;
        _currency = currency;
    }

    /// <summary>
    /// Xác nhận recruit 1 Pokemon. Yêu cầu JWT (accountId từ token).
    /// </summary>
    public async Task<RecruitConfirmResponse> ConfirmAsync(string accountId, RecruitConfirmRequest req)
    {
        var player = await _db.Players.Find(p => p.AccountId == accountId).FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        bool isTrial = req.RecruitType.Equals("trial", StringComparison.OrdinalIgnoreCase);

        // Trừ VP nếu là chiêu mộ vĩnh viễn (permanent)
        if (!isTrial)
        {
            var vpRemaining = await _currency.SpendVPAsync(
                player.Id,
                2500,
                reason: CurrencyService.ReasonRecruitPerm,
                refId: req.SpeciesId.ToString());

            if (vpRemaining == null)
                throw new Exception("Không đủ VP để nhận Pokémon vĩnh viễn (Cần 2500 VP).");
        }

        // Kiểm tra trial box còn chỗ không
        if (isTrial)
        {
            var now = DateTime.UtcNow;
            long trialCount = await _db.PokemonInstances.CountDocumentsAsync(
                p => p.OwnerId == player.Id && !p.IsInParty && p.IsTrial && p.TrialExpiry > now);

            if (trialCount >= BoxService.TrialBoxCount * BoxService.SlotsPerBox)
                throw new Exception("Trial Box đã đầy. Hãy giải phóng chỗ trống trước.");
        }

        // Lấy party count để biết có vào party không
        long partyCount = await _db.PokemonInstances.CountDocumentsAsync(
            p => p.OwnerId == player.Id && p.IsInParty);

        bool goToParty = partyCount < 6;
        int? partySlot = null;

        if (goToParty)
        {
            var takenSlots = await _db.PokemonInstances
                .Find(p => p.OwnerId == player.Id && p.IsInParty)
                .Project(p => p.PartySlot)
                .ToListAsync();
            partySlot = Enumerable.Range(0, 6).First(i => !takenSlots.Contains(i));
        }

        // Random 4 skill từ pool learnable moves của Pokemon
        var dex = await _db.Pokedex.Find(d => d.Id == req.SpeciesId).FirstOrDefaultAsync();
        var pokemonMoves = new List<PokemonMove>();

        var movePool = (dex?.LearnableMoves != null && dex.LearnableMoves.Count > 0)
            ? dex.LearnableMoves
            : dex?.DefaultMoves ?? new List<int>();

        if (movePool.Count > 0)
        {
            var selectedIds = movePool.OrderBy(_ => _rng.Next()).Take(4).ToList();
            var moveEntries = await _db.Moves.Find(m => selectedIds.Contains(m.Id)).ToListAsync();
            foreach (var moveId in selectedIds)
            {
                var moveEntry = moveEntries.FirstOrDefault(m => m.Id == moveId);
                if (moveEntry != null)
                    pokemonMoves.Add(new PokemonMove
                    {
                        MoveId    = moveEntry.Id,
                        MoveName  = moveEntry.Name,
                        MoveType  = moveEntry.Type,
                        Category  = moveEntry.Category,
                        MaxPp     = moveEntry.PP > 0 ? moveEntry.PP : 15,
                        CurrentPp = moveEntry.PP > 0 ? moveEntry.PP : 15,
                    });
            }
        }
        if (pokemonMoves.Count == 0)
            pokemonMoves.Add(new PokemonMove { MoveId = 1, MoveName = "Pound", MoveType = "normal", Category = "Physical", MaxPp = 35, CurrentPp = 35 });

        var newPokemon = new PokemonInstance
        {
            OwnerId     = player.Id,
            SpeciesId   = req.SpeciesId,
            Nickname    = "",
            Level       = 50,
            Nature      = Natures[_rng.Next(Natures.Length)],
            MaxHp       = 150,
            CurrentHp   = 150,
            IsInParty   = goToParty,
            PartySlot   = partySlot,
            IsTrial     = isTrial,
            TrialExpiry = isTrial ? DateTime.UtcNow.AddDays(7) : null,
            Ivs = new StatBlock
            {
                Hp    = _rng.Next(32),
                Atk   = _rng.Next(32),
                Def   = _rng.Next(32),
                SpAtk = _rng.Next(32),
                SpDef = _rng.Next(32),
                Spd   = _rng.Next(32)
            },
            Moves = pokemonMoves
        };

        await _db.PokemonInstances.InsertOneAsync(newPokemon);

        string dest = goToParty ? "party" : (isTrial ? "Trial Box" : "Box");
        return new RecruitConfirmResponse
        {
            Success   = true,
            Message   = $"Pokemon đã được thêm vào {dest}.",
            IsInParty = goToParty,
            PokemonId = newPokemon.Id
        };
    }

    /// <summary>
    /// Roll ngẫu nhiên 10 Pokémon từ toàn bộ Pokédex.
    /// Chỉ chọn Pokémon có icon tồn tại trên server.
    /// Trừ 2500 VP cho mỗi lần Roll.
    /// </summary>
    public async Task<List<RecruitResult>> RollAsync(string accountId, int count = 10)
    {
        var player = await _db.Players.Find(p => p.AccountId == accountId).FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        // Trừ 2500 VP cho lượt Roll
        var vpRemaining = await _currency.SpendVPAsync(
            player.Id,
            2500,
            reason: CurrencyService.ReasonRecruitRoll);

        if (vpRemaining == null)
            throw new Exception("Không đủ VP để chiêu mộ (Cần 2500 VP).");

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
