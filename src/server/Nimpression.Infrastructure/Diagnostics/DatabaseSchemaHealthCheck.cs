using Microsoft.EntityFrameworkCore;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Diagnostics;

/// <summary>
/// 数据库连通性与架构完整性诊断检查。
/// 彻底解决空库/未迁移时静默返回 200 的缺陷，确保关键表与迁移状态缺失时快速暴露。
/// </summary>
public static class DatabaseSchemaHealthCheck
{
    private static readonly string[] CriticalTables =
    [
        "Users",
        "Drivers",
        "Vehicles",
        "Areas",
        "JobTasks",
        "AuditEvents"
    ];

    public static async Task<(bool IsHealthy, string Details)> CheckAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return (false, "Cannot connect to database.");
            }

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count > 0)
            {
                return (false, $"Database schema has {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}.");
            }

            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            if (appliedMigrations.Count == 0)
            {
                return (false, "Database has zero applied migrations. Schema is not initialized.");
            }

            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            var missingTables = CriticalTables.Where(t => !existingTables.Contains(t)).ToList();
            if (missingTables.Count > 0)
            {
                return (false, $"Missing critical database table(s): {string.Join(", ", missingTables)}.");
            }

            return (true, "Database schema is healthy and up-to-date.");
        }
        catch (Exception ex)
        {
            return (false, $"Database health check failed with exception: {ex.Message}");
        }
    }
}
