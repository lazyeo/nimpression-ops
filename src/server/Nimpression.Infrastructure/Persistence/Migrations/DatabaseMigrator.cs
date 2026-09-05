using Microsoft.EntityFrameworkCore;

namespace Nimpression.Infrastructure.Persistence.Migrations;

/// <summary>
/// 生产环境数据库迁移执行器。
/// 采用 PostgreSQL Advisory Lock 机制保障多副本/多并发执行环境下的原子性与防重入，
/// 遇到任何失败一律显式抛出异常导致非零退出，严禁吞掉异常继续运行。
/// </summary>
public static class DatabaseMigrator
{
    // PostgreSQL 64-bit Advisory Lock Key: 0x4E494D5052455353 = "NIMPRESS"
    public const long MigrationAdvisoryLockKey = 0x4E494D5052455353;

    public static async Task MigrateAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // 获取会话级分布式顾问锁（Advisory Lock），防止多实例并发迁移冲突
            await context.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_lock({MigrationAdvisoryLockKey});",
                cancellationToken);

            try
            {
                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count > 0)
                {
                    Console.WriteLine($"Discovered {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}");
                }
                else
                {
                    Console.WriteLine("Database schema is already up to date. No pending migrations.");
                }

                await context.Database.MigrateAsync(cancellationToken);
                Console.WriteLine("Database migrations applied successfully.");
            }
            finally
            {
                // 确保无论迁移成功还是失败，均显式释放顾问锁
                await context.Database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_unlock({MigrationAdvisoryLockKey});",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
