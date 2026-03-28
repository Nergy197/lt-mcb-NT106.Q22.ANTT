using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

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
}