using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viora.Application.ProfessionalVerifications;
using Viora.Infrastructure.Persistence;

namespace Viora.Infrastructure.Media;

public sealed class VerificationFileCleanupWorker(IServiceScopeFactory scopes,
    ILogger<VerificationFileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IPrivateFileStorage>();
                var items = await db.VerificationFileCleanups.OrderBy(x => x.CreatedAt).Take(20).ToListAsync(stoppingToken);
                foreach (var item in items)
                {
                    try
                    {
                        await storage.DeleteAsync(item.FileKey, stoppingToken);
                        db.VerificationFileCleanups.Remove(item);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogWarning("Private document cleanup deferred for retry."); }
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Private document cleanup queue unavailable; retrying later."); }
        }
    }
}
