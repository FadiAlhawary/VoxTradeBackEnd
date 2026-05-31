using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;

namespace VoxTrade.Data;

/// <summary>
/// Ensures <c>public.wallet_freeze_guard_json</c> exists (Scripts/migrations/003_wallet_freeze_guard.sql).
/// </summary>
public static class WalletFreezeSchemaBootstrap
{
    public static async Task EnsureWalletFreezeGuardAsync(
        TradingDbContext context,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var sqlPath = Path.Combine(
            environment.ContentRootPath,
            "Scripts",
            "migrations",
            "003_wallet_freeze_guard.sql");

        if (!File.Exists(sqlPath))
        {
            logger.LogWarning(
                "Wallet freeze migration not found at {Path}; skipping SQL bootstrap",
                sqlPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(sqlPath, cancellationToken);

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            logger.LogInformation("Applied wallet freeze guard SQL (wallet_freeze_guard_json)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply wallet freeze guard SQL");
        }
    }
}
