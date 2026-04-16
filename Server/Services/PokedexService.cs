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

    // Hàm lấy thông tin Pokemon theo ID
    public async Task<PokedexEntry> GetPokemonByIdAsync(int id)
    {
        return await _context.Pokedex.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    // Hàm lấy thông tin chiêu thức theo ID
    public async Task<MoveEntry> GetMoveByIdAsync(int id)
    {
        return await _context.Moves.Find(m => m.Id == id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Cleanup test data — Xoá Pokemon cũ và reset thông số để test luồng mới.
    /// </summary>
    public async Task CleanupAndResetDatabaseAsync()
    {
        Console.WriteLine("[Cleanup] Đang dọn dẹp dữ liệu cũ để bạn test...");
        
        // 1. Xoá toàn bộ Pokemon cũ của mọi người chơi
        await _context.PokemonInstances.DeleteManyAsync(_ => true);
        
        // 2. Reset MMR và VP cho mọi Player về mặc định
        var update = Builders<Player>.Update
            .Set(p => p.MMR, 1000)
            .Set(p => p.VP, 0)
            .Set(p => p.RankedWins, 0)
            .Set(p => p.RankedMatches, 0);
        await _context.Players.UpdateManyAsync(_ => true, update);

        Console.WriteLine("[Cleanup] Đã dọn dẹp xong! Giờ bạn chỉ cần Login lại để nhận 6 Pokemon mới.");
    }

    // Hàm seed tự động JSON vào MongoDB
    public async Task SeedDatabaseAsync()
    {
        // Dọn dẹp trước khi Seed (Bạn có thể comment dòng này sau khi test xong)
        await CleanupAndResetDatabaseAsync();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 1. Seed Pokedex
        if (!await _context.Pokedex.Find(_ => true).AnyAsync())
        {
            var pokedexPath = Path.Combine(AppContext.BaseDirectory, "Data", "pokedex_final.json");
            if (!File.Exists(pokedexPath))
                pokedexPath = Path.Combine(Directory.GetCurrentDirectory(), "Server", "Data", "pokedex_final.json");

            if (File.Exists(pokedexPath))
            {
                var json = await File.ReadAllTextAsync(pokedexPath);
                var entries = JsonSerializer.Deserialize<List<PokedexEntry>>(json, options);
                if (entries != null && entries.Count > 0)
                {
                    await _context.Pokedex.InsertManyAsync(entries);
                    Console.WriteLine($"[Seed] Đã nhập {entries.Count} Pokemon vào collection Pokedex.");
                }
            }
            else
            {
                Console.WriteLine($"[Seed] Không tìm thấy file {pokedexPath}");
            }
        }

        // 2. Seed Moves
        if (!await _context.Moves.Find(_ => true).AnyAsync())
        {
            var movesPath = Path.Combine(AppContext.BaseDirectory, "Data", "moves_final.json");
            if (!File.Exists(movesPath))
                movesPath = Path.Combine(Directory.GetCurrentDirectory(), "Server", "Data", "moves_final.json");

            if (File.Exists(movesPath))
            {
                var json = await File.ReadAllTextAsync(movesPath);
                var entries = JsonSerializer.Deserialize<List<MoveEntry>>(json, options);
                if (entries != null && entries.Count > 0)
                {
                    await _context.Moves.InsertManyAsync(entries);
                    Console.WriteLine($"[Seed] Đã nhập {entries.Count} Chiêu thức vào collection Moves.");
                }
            }
            else
            {
                Console.WriteLine($"[Seed] Không tìm thấy file {movesPath}");
            }
        }
    }
}