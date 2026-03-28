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

    public IMongoCollection<PokemonMoves> PokemonMoves
        => _database.GetCollection<PokemonMoves>("pokemonmoves");

    public IMongoCollection<PokemonStats> PokemonStats
        => _database.GetCollection<PokemonStats>("pokemonstats");

    public IMongoCollection<RevokedToken> RevokedTokens
        => _database.GetCollection<RevokedToken>("revoked_tokens");

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

        // PokemonMoves: unique compound (pokemon_instance_id + slot)
        PokemonMoves.Indexes.CreateOne(new CreateIndexModel<PokemonMoves>(
            Builders<PokemonMoves>.IndexKeys
                .Ascending(m => m.PokemonInstanceId)
                .Ascending(m => m.Slot),
            new CreateIndexOptions { Unique = true }));

        // PokemonStats: unique on pokemon_instance_id
        PokemonStats.Indexes.CreateOne(new CreateIndexModel<PokemonStats>(
            Builders<PokemonStats>.IndexKeys.Ascending(s => s.PokemonInstanceId),
            new CreateIndexOptions { Unique = true }));

        // RevokedTokens: TTL index — auto-delete expired tokens after 1 day buffer
        RevokedTokens.Indexes.CreateOne(new CreateIndexModel<RevokedToken>(
            Builders<RevokedToken>.IndexKeys.Ascending(r => r.Expiry),
            new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(1) }));
    }
}
