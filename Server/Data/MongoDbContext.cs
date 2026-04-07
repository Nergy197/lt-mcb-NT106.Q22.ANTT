using MongoDB.Driver;
using PokemonMMO.Models;
namespace PokemonMMO.Data;

/// <summary>
/// Centralized MongoDB access — mirrors the Mongoose connection from the old Node.js server.
/// Each collection maps to one model class.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        // Ensure indexes on first boot
        CreateIndexes();
    }

    // ── Collections ──────────────────────────────────────────────────────
    public IMongoCollection<Account> Accounts
        => _database.GetCollection<Account>("accounts");

    public IMongoCollection<Player> Players
        => _database.GetCollection<Player>("players");

    public IMongoCollection<PokemonInstance> PokemonInstances
        => _database.GetCollection<PokemonInstance>("pokemoninstances");

    public IMongoCollection<PokedexEntry> Pokedex 
        => _database.GetCollection<PokedexEntry>("pokedex");

    public IMongoCollection<MoveEntry> Moves 
    => _database.GetCollection<MoveEntry>("moves");

    public IMongoCollection<RevokedToken> RevokedTokens
        => _database.GetCollection<RevokedToken>("revoked_tokens");

    public IMongoCollection<BattleLogEntry> BattleLogs
        => _database.GetCollection<BattleLogEntry>("battle_logs");

    // ── Indexes ──────────────────────────────────────────────────────────
    private void CreateIndexes()
    {
        // Account: unique username & email
        Accounts.Indexes.CreateOne(new CreateIndexModel<Account>(
            Builders<Account>.IndexKeys.Ascending(a => a.Username),
            new CreateIndexOptions { Unique = true }));

        Accounts.Indexes.CreateOne(new CreateIndexModel<Account>(
            Builders<Account>.IndexKeys.Ascending(a => a.Email),
            new CreateIndexOptions { Unique = true }));

        // Player: unique name, index on account_id
        Players.Indexes.CreateOne(new CreateIndexModel<Player>(
            Builders<Player>.IndexKeys.Ascending(p => p.Name),
            new CreateIndexOptions { Unique = true }));

        Players.Indexes.CreateOne(new CreateIndexModel<Player>(
            Builders<Player>.IndexKeys.Ascending(p => p.AccountId)));

        // PokemonInstance: compound index (owner + party)
        PokemonInstances.Indexes.CreateOne(new CreateIndexModel<PokemonInstance>(
            Builders<PokemonInstance>.IndexKeys
                .Ascending(p => p.OwnerId)
                .Ascending(p => p.IsInParty)));

        // Pokedex: unique id
        Pokedex.Indexes.CreateOne(new CreateIndexModel<PokedexEntry>(
            Builders<PokedexEntry>.IndexKeys.Ascending(p => p.Id),
            new CreateIndexOptions { Unique = true }));

        // Moves: unique id
        Moves.Indexes.CreateOne(new CreateIndexModel<MoveEntry>(
            Builders<MoveEntry>.IndexKeys.Ascending(m => m.Id),
            new CreateIndexOptions { Unique = true }));

        // RevokedTokens: TTL index — auto-delete expired tokens after 1 day buffer
        RevokedTokens.Indexes.CreateOne(new CreateIndexModel<RevokedToken>(
            Builders<RevokedToken>.IndexKeys.Ascending(r => r.Expiry),
            new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(1) }));

        // BattleLogs: index by battle timeline
        BattleLogs.Indexes.CreateOne(new CreateIndexModel<BattleLogEntry>(
            Builders<BattleLogEntry>.IndexKeys
                .Ascending(b => b.BattleId)
                .Descending(b => b.CreatedAtUtc)));
    }
}
