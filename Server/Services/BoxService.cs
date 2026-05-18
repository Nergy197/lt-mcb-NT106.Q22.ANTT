using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using System.Text.Json;

namespace PokemonMMO.Services;

public class BoxService
{
    public const int SlotsPerBox     = 30;
    public const int NormalBoxCount  = 22;
    public const int TrialBoxCount   = 10;
    public const int TotalBoxCount   = NormalBoxCount + TrialBoxCount;

    private readonly IMongoCollection<PokemonInstance> _pokemon;
    private readonly IMongoCollection<Player>          _players;
    private readonly IWebHostEnvironment               _env;

    // speciesId → pokemon name (lowercase), loaded once
    private static Dictionary<int, string>? _nameById;
    private static readonly object _nameLock = new();

    public BoxService(MongoDbContext db, IWebHostEnvironment env)
    {
        _pokemon = db.PokemonInstances;
        _players = db.Players;
        _env     = env;
    }

    private Dictionary<int, string> GetNameById()
    {
        if (_nameById != null) return _nameById;
        lock (_nameLock)
        {
            if (_nameById != null) return _nameById;
            var path = Path.Combine(_env.WebRootPath, "data", "pokedex_final.json");
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<int, string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("id", out var idProp) &&
                    el.TryGetProperty("name", out var nameProp))
                    dict[idProp.GetInt32()] = nameProp.GetString()?.ToLower() ?? "";
            }
            _nameById = dict;
        }
        return _nameById;
    }

    private string ResolveIconUrl(int speciesId)
    {
        var names = GetNameById();
        if (!names.TryGetValue(speciesId, out var name) || string.IsNullOrEmpty(name))
            return "";

        var iconsDir = Path.Combine(_env.WebRootPath, "data", "pokemon", "icons");

        // Thử tên gốc trước
        var file = $"{name}.png";
        if (File.Exists(Path.Combine(iconsDir, file))) return $"/data/pokemon/icons/{file}";

        // Thử bỏ hậu tố form (tương tự RecruitService)
        string[] suffixes = { "-galar","-alola","-hisui","-paldea","-mega","-mega-x","-mega-y",
            "-origin","-therian","-incarnate","-primal","-ash","-blade","-shield",
            "-attack","-defense","-speed","-black","-white","-heat","-wash","-frost","-fan","-mow" };
        foreach (var sfx in suffixes.OrderByDescending(s => s.Length))
        {
            if (name.EndsWith(sfx))
            {
                var baseFile = $"{name[..^sfx.Length]}.png";
                if (File.Exists(Path.Combine(iconsDir, baseFile))) return $"/data/pokemon/icons/{baseFile}";
            }
        }
        return "";
    }

    public async Task<PartyInfoDto> GetPartyAsync(string accountId)
    {
        var player = await _players
            .Find(p => p.AccountId == accountId)
            .FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        var partyPokemon = await _pokemon
            .Find(p => p.OwnerId == player.Id && p.IsInParty)
            .SortBy(p => p.PartySlot)
            .ToListAsync();

        var slots = partyPokemon.Select(p => new PartySlotDto
        {
            Slot      = p.PartySlot ?? 0,
            PokemonId = p.Id,
            SpeciesId = p.SpeciesId,
            Nickname  = p.Nickname ?? "",
            Level     = p.Level,
            CurrentHp = p.CurrentHp,
            MaxHp     = p.MaxHp
        }).ToList();

        return new PartyInfoDto { Slots = slots };
    }

    public async Task WithdrawAsync(string accountId, string pokemonId)
    {
        var player = await _players.Find(p => p.AccountId == accountId).FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        var partyCount = await _pokemon.CountDocumentsAsync(p => p.OwnerId == player.Id && p.IsInParty);
        if (partyCount >= 6) throw new Exception("Đội đã đủ 6 Pokemon.");

        var takenSlots = await _pokemon
            .Find(p => p.OwnerId == player.Id && p.IsInParty)
            .Project(p => p.PartySlot)
            .ToListAsync();

        int freeSlot = Enumerable.Range(0, 6).First(i => !takenSlots.Contains(i));

        var update = Builders<PokemonInstance>.Update
            .Set(p => p.IsInParty,  true)
            .Set(p => p.PartySlot,  freeSlot);

        var result = await _pokemon.UpdateOneAsync(
            p => p.Id == pokemonId && p.OwnerId == player.Id && !p.IsInParty, update);

        if (result.MatchedCount == 0) throw new Exception("Pokemon không tìm thấy hoặc đã trong đội.");
    }

    public async Task DepositAsync(string accountId, string pokemonId)
    {
        var player = await _players.Find(p => p.AccountId == accountId).FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        var partyCount = await _pokemon.CountDocumentsAsync(p => p.OwnerId == player.Id && p.IsInParty);
        if (partyCount <= 1) throw new Exception("Không thể gửi Pokemon cuối cùng trong đội.");

        var update = Builders<PokemonInstance>.Update
            .Set(p => p.IsInParty, false)
            .Unset(p => p.PartySlot);

        var result = await _pokemon.UpdateOneAsync(
            p => p.Id == pokemonId && p.OwnerId == player.Id && p.IsInParty, update);

        if (result.MatchedCount == 0) throw new Exception("Pokemon không tìm thấy hoặc không trong đội.");
    }

    public async Task<BoxInfoDto> GetBoxAsync(string accountId, int boxIndex)
    {
        var player = await _players
            .Find(p => p.AccountId == accountId)
            .FirstOrDefaultAsync()
            ?? throw new Exception("Player not found");

        bool isTrialBox = boxIndex >= NormalBoxCount;
        string boxName;
        List<BoxSlotDto> slots;

        if (isTrialBox)
        {
            int trialBoxIdx = boxIndex - NormalBoxCount; // 0–9
            boxName = $"Trial Box {trialBoxIdx + 1}";

            var now = DateTime.UtcNow;
            var trialPokemon = await _pokemon
                .Find(p => p.OwnerId == player.Id && !p.IsInParty && p.IsTrial && p.TrialExpiry > now)
                .SortBy(p => p.Id)
                .ToListAsync();

            slots = trialPokemon
                .Skip(trialBoxIdx * SlotsPerBox)
                .Take(SlotsPerBox)
                .Select((p, i) => new BoxSlotDto
                {
                    Slot      = i,
                    PokemonId = p.Id,
                    SpeciesId = p.SpeciesId,
                    Nickname  = p.Nickname ?? "",
                    Level     = p.Level,
                    IconUrl   = ResolveIconUrl(p.SpeciesId)
                }).ToList();
        }
        else
        {
            boxName = $"Box {boxIndex + 1}";

            var normalPokemon = await _pokemon
                .Find(p => p.OwnerId == player.Id && !p.IsInParty && !p.IsTrial)
                .SortBy(p => p.Id)
                .ToListAsync();

            slots = normalPokemon
                .Skip(boxIndex * SlotsPerBox)
                .Take(SlotsPerBox)
                .Select((p, i) => new BoxSlotDto
                {
                    Slot      = i,
                    PokemonId = p.Id,
                    SpeciesId = p.SpeciesId,
                    Nickname  = p.Nickname ?? "",
                    Level     = p.Level,
                    IconUrl   = ResolveIconUrl(p.SpeciesId)
                }).ToList();
        }

        return new BoxInfoDto
        {
            BoxIndex   = boxIndex,
            TotalBoxes = TotalBoxCount,
            BoxName    = boxName,
            Slots      = slots
        };
    }
}
