using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using System.Text.Json;

namespace PokemonMMO.Services;

public class PokedexService
{
    private readonly MongoDbContext _context;

    public PokedexService(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<PokedexEntry> GetPokemonByIdAsync(int id)
        => await _context.Pokedex.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<MoveEntry> GetMoveByIdAsync(int id)
        => await _context.Moves.Find(m => m.Id == id).FirstOrDefaultAsync();

    /// <summary>
    /// Chỉ dùng khi cần reset thủ công — KHÔNG tự gọi lúc startup.
    /// </summary>
    public async Task CleanupAndResetDatabaseAsync()
    {
        Console.WriteLine("[Cleanup] Xóa toàn bộ Pokemon instances và reset MMR...");
        await _context.PokemonInstances.DeleteManyAsync(_ => true);

        var update = Builders<Player>.Update
            .Set(p => p.MMR, 1000)
            .Set(p => p.VP, 0)
            .Set(p => p.RankedWins, 0)
            .Set(p => p.RankedMatches, 0);
        await _context.Players.UpdateManyAsync(_ => true, update);
        Console.WriteLine("[Cleanup] Xong. Login lại để nhận 6 Pokemon mới.");
    }

    /// <summary>
    /// Seed Pokedex và Moves từ file JSON nếu chưa đủ dữ liệu.
    /// KHÔNG xóa Pokemon của người chơi.
    /// </summary>
    public async Task SeedDatabaseAsync()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // ── 1. Seed Pokedex (cần đủ >= 1000 entries mới bỏ qua) ──────────
        long pokedexCount = await _context.Pokedex.CountDocumentsAsync(_ => true);
        if (pokedexCount < 1000)
        {
            var path = FindDataFile("pokedex_final.json");
            if (path != null)
            {
                if (pokedexCount > 0)
                {
                    await _context.Pokedex.DeleteManyAsync(_ => true);
                    Console.WriteLine("[Seed] Xóa Pokedex cũ không đủ data.");
                }
                var entries = JsonSerializer.Deserialize<List<PokedexEntry>>(
                    await File.ReadAllTextAsync(path), options);
                if (entries?.Count > 0)
                {
                    await _context.Pokedex.InsertManyAsync(entries);
                    Console.WriteLine($"[Seed] Đã nhập {entries.Count} Pokedex entries.");
                }
            }
            else
            {
                Console.WriteLine("[Seed] Không tìm thấy pokedex_final.json");
            }
        }
        else
        {
            Console.WriteLine($"[Seed] Pokedex OK ({pokedexCount} entries).");
        }

        // ── 2. Seed Moves (cần đủ >= 500 entries) ────────────────────────
        long moveCount = await _context.Moves.CountDocumentsAsync(_ => true);
        if (moveCount < 500)
        {
            var path = FindDataFile("moves_final.json");
            if (path != null)
            {
                if (moveCount > 0)
                {
                    await _context.Moves.DeleteManyAsync(_ => true);
                    Console.WriteLine("[Seed] Xóa Moves cũ không đủ data.");
                }
                var entries = JsonSerializer.Deserialize<List<MoveEntry>>(
                    await File.ReadAllTextAsync(path), options);
                if (entries?.Count > 0)
                {
                    await _context.Moves.InsertManyAsync(entries);
                    Console.WriteLine($"[Seed] Đã nhập {entries.Count} Moves.");
                }
            }
            else
            {
                Console.WriteLine("[Seed] Không tìm thấy moves_final.json");
            }
        }
        else
        {
            Console.WriteLine($"[Seed] Moves OK ({moveCount} entries).");
        }

        // ── 3. Populate LearnableMoves từ species JSON ────────────────────
        long emptyLearnableCount = await _context.Pokedex.CountDocumentsAsync(
            p => p.LearnableMoves == null || p.LearnableMoves.Count == 0);

        if (emptyLearnableCount > 100)
        {
            Console.WriteLine($"[Seed] Populating LearnableMoves cho {emptyLearnableCount} Pokemon...");

            var validMoveIds = (await _context.Moves.Find(_ => true)
                .Project(m => m.Id).ToListAsync()).ToHashSet();

            var speciesDir = FindSpeciesDir();
            if (speciesDir != null)
            {
                var entries = await _context.Pokedex.Find(
                    p => p.LearnableMoves == null || p.LearnableMoves.Count == 0).ToListAsync();

                var updates = new List<MongoDB.Driver.WriteModel<PokedexEntry>>();
                int updated = 0;

                foreach (var entry in entries)
                {
                    var speciesFile = Path.Combine(speciesDir, $"{entry.Name.ToLower()}.json");
                    if (!File.Exists(speciesFile)) continue;

                    var learnableMoves = ParseLearnableMoves(speciesFile, validMoveIds);
                    if (learnableMoves.Count == 0) continue;

                    var filter = MongoDB.Driver.Builders<PokedexEntry>.Filter.Eq(p => p.Id, entry.Id);
                    var update = MongoDB.Driver.Builders<PokedexEntry>.Update
                        .Set(p => p.LearnableMoves, learnableMoves);
                    updates.Add(new MongoDB.Driver.UpdateOneModel<PokedexEntry>(filter, update));
                    updated++;

                    if (updates.Count >= 100)
                    {
                        await _context.Pokedex.BulkWriteAsync(updates);
                        updates.Clear();
                    }
                }

                if (updates.Count > 0)
                    await _context.Pokedex.BulkWriteAsync(updates);

                Console.WriteLine($"[Seed] LearnableMoves đã populate cho {updated} Pokemon.");
            }
            else
            {
                Console.WriteLine("[Seed] Không tìm thấy thư mục species JSON.");
            }
        }
        else
        {
            Console.WriteLine("[Seed] LearnableMoves OK.");
        }
    }

    private static List<int> ParseLearnableMoves(string filePath, HashSet<int> validMoveIds)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(filePath));
            if (!doc.RootElement.TryGetProperty("moves", out var movesArr)) return new();

            var result = new List<int>();
            foreach (var m in movesArr.EnumerateArray())
            {
                if (!m.TryGetProperty("move", out var move)) continue;
                if (!move.TryGetProperty("url", out var urlProp)) continue;
                var url = urlProp.GetString();
                if (url == null) continue;
                var parts = url.TrimEnd('/').Split('/');
                if (int.TryParse(parts[^1], out int id) && validMoveIds.Contains(id))
                    result.Add(id);
            }
            return result;
        }
        catch { return new(); }
    }

    private static string? FindSpeciesDir()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", "species"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "species"),
            Path.Combine(Directory.GetCurrentDirectory(), "Server", "wwwroot", "data", "species"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindDataFile(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Server", "Data", fileName),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
