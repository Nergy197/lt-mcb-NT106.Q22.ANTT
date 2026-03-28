using PokemonMMO.Data;
using PokemonMMO.Hubs;
using PokemonMMO.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Port configuration (from env or appsettings)
// ---------------------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("PORT")
    ?? builder.Configuration["Server:Port"]
    ?? "2567";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

// MongoDB
var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI")
    ?? builder.Configuration["MongoDb:ConnectionString"]
    ?? "mongodb://localhost:27017";
var mongoDb = Environment.GetEnvironmentVariable("MONGO_DATABASE")
    ?? builder.Configuration["MongoDb:DatabaseName"]
    ?? "pokemon_mmo";

builder.Services.AddSingleton(new MongoDbContext(mongoUri, mongoDb));

// Game services
builder.Services.AddSingleton<PokemonDataService>();
builder.Services.AddScoped<GameService>();

// SignalR (real-time WebSocket)
builder.Services.AddSignalR();

// CORS (allow Unity client to connect)
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware
// ---------------------------------------------------------------------------
app.UseCors();

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Pokemon MMO Server" }));

// SignalR hub — Unity clients connect via WebSocket to /game
app.MapHub<GameHub>("/game");

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------
Console.WriteLine($"✅ Pokémon MMO Server listening on http://0.0.0.0:{port}");
Console.WriteLine($"📡 SignalR Hub: ws://localhost:{port}/game");
Console.WriteLine($"🗄️  MongoDB: {mongoUri}/{mongoDb}");

app.Run();
