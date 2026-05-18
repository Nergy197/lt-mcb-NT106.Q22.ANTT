using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

/// <summary>
/// Background service chạy mỗi giờ, xóa các Pokemon trial đã hết hạn khỏi DB.
/// </summary>
public class TrialCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<TrialCleanupService> _logger;

    public TrialCleanupService(IServiceScopeFactory scopeFactory, ILogger<TrialCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TrialCleanup] Service khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

            var now = DateTime.UtcNow;
            var result = await db.PokemonInstances.DeleteManyAsync(
                p => p.IsTrial && p.TrialExpiry != null && p.TrialExpiry <= now);

            if (result.DeletedCount > 0)
                _logger.LogInformation("[TrialCleanup] Đã xóa {Count} Pokemon trial hết hạn.", result.DeletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TrialCleanup] Lỗi khi dọn dẹp Pokemon trial.");
        }
    }
}
