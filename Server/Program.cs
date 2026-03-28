using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PokemonMMO.Data;
using PokemonMMO.Hubs;
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
            OnTokenValidated = async ctx =>
            {
                var authService = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var rawToken    = ctx.Request.Headers.Authorization
                    .ToString()
                    .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                if (await authService.IsTokenRevokedAsync(rawToken))
                    ctx.Fail("Token đã bị thu hồi (đăng xuất).");
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Game & Auth services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<PokemonDataService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AuthService>();

// ---------------------------------------------------------------------------
// MVC Controllers + SignalR
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddSignalR();

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

// SignalR hub
app.MapHub<GameHub>("/game");

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------
Console.WriteLine($"✅ Pokémon MMO Server listening on http://0.0.0.0:{port}");
Console.WriteLine($"📡 SignalR Hub: ws://localhost:{port}/game");
Console.WriteLine($"🗄️  MongoDB: {mongoUri}/{mongoDb}");
Console.WriteLine($"🔐 Auth API:  http://localhost:{port}/api/auth");

app.Run();
