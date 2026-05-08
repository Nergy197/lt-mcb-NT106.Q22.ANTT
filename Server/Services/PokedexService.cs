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
