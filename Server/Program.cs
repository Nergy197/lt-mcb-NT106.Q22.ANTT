using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PokemonMMO.Data;
using PokemonMMO.Hubs;
using PokemonMMO.Options;
using PokemonMMO.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Port configuration
// ---------------------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("PORT")
    ?? builder.Configuration["Server:Port"]
    ?? "2567";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------------------------------------------------------------------------
// MongoDB
// ---------------------------------------------------------------------------
var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI")
    ?? builder.Configuration["MongoDb:ConnectionString"]
    ?? "mongodb://localhost:27017";
var mongoDb = Environment.GetEnvironmentVariable("MONGO_DATABASE")
    ?? builder.Configuration["MongoDb:DatabaseName"]
    ?? "pokemon_mmo";

builder.Services.AddSingleton(new MongoDbContext(mongoUri, mongoDb));

// ---------------------------------------------------------------------------
// JWT Authentication
// ---------------------------------------------------------------------------
var jwtSecret   = builder.Configuration["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "PokemonMMO";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PokemonMMO";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Check blacklist sau khi JWT hợp lệ về mặt cryptographic
        options.Events = new JwtBearerEvents
        {
            // SignalR WebSocket clients commonly pass token via query string.
            OnMessageReceived = ctx =>
            {
                if (!string.IsNullOrEmpty(ctx.Token))
                    return Task.CompletedTask;

                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && (
                    path.StartsWithSegments("/hubs/matchmaking") ||
                    path.StartsWithSegments("/hubs/battle") ||
                    path.StartsWithSegments("/game")))
                    ctx.Token = accessToken;

                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var authService = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var rawToken    = ctx.Request.Headers.Authorization
                    .ToString()
                    .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(rawToken))
                    rawToken = ctx.Request.Query["access_token"].ToString();

                if (!string.IsNullOrWhiteSpace(rawToken) && await authService.IsTokenRevokedAsync(rawToken))
                    ctx.Fail("Token đã bị thu hồi (đăng xuất).");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.Configure<BattleOptions>(builder.Configuration.GetSection("Battle"));

// ---------------------------------------------------------------------------
// Game & Auth services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<PokemonDataService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<BattleService>();
// ── New services for Pokedex, Moves and Email ──────────────────────────────
builder.Services.AddScoped<PokedexService>();
builder.Services.AddScoped<EmailService>();

// ---------------------------------------------------------------------------
// MVC Controllers + SignalR
// ---------------------------------------------------------------------------
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
});

// ---------------------------------------------------------------------------
// Swagger (với hỗ trợ JWT Authorization)
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PokemonMMO API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                },
                Scheme = "oauth2",
                Name   = "Bearer",
                In     = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// ---------------------------------------------------------------------------
// CORS (allow Unity client)
// ---------------------------------------------------------------------------
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health check
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Pokemon MMO Server" }));

// REST API controllers (auth, etc.)
app.MapControllers();

// SignalR hubs
app.MapHub<MatchmakingHub>("/hubs/matchmaking");
app.MapHub<BattleHub>("/hubs/battle");

// Compatibility mapping (Unity client cũ dùng /game)
app.MapHub<BattleHub>("/game");

// ---------------------------------------------------------------------------
// Seed Database & Cleanup
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var pokedex = scope.ServiceProvider.GetRequiredService<PokedexService>();
    await pokedex.SeedDatabaseAsync();
}

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------
Console.WriteLine($"✅ Pokémon MMO Server listening on http://0.0.0.0:{port}");
Console.WriteLine($"📡 Matchmaking Hub: ws://localhost:{port}/hubs/matchmaking");
Console.WriteLine($"⚔️  Battle Hub:      ws://localhost:{port}/hubs/battle");
Console.WriteLine($"🗄️  MongoDB: {mongoUri}/{mongoDb}");
Console.WriteLine($"🔐 Auth API:  http://localhost:{port}/api/auth");
Console.WriteLine($"📖 Swagger:   http://localhost:{port}/swagger");

app.Run();
