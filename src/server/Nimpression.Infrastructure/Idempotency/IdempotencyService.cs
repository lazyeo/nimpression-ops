using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.Common;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Idempotency;

/// <summary>
/// 离线重放幂等服务实现（F5.4）。
/// 业务变更与幂等记录写入同事务，通过数据库主键唯一约束冲突判重，杜绝 TOCTOU 竞态。
/// </summary>
public sealed class IdempotencyService(
    AppDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IIdempotencyService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<Result> ExecuteAsync(
        string key,
        object requestPayload,
        Func<Task<Result>> action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return await action();
        }

        var requestHash = ComputePayloadHash(requestPayload);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. 同事务内直接尝试插入幂等记录占位（由主键唯一约束原子性判重，杜绝 TOCTOU）
            var record = new IdempotencyRecord(
                key,
                requestHash,
                "{}",
                200,
                dateTimeProvider.UtcNow);

            await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // 2. 占位成功（首次执行者）：执行业务逻辑
            var result = await action();
            if (!result.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            // 3. 业务成功：提交事务（业务变更与幂等记录原子落库）
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);

            // 唯一约束冲突（23505）：说明该 Key 已被首次请求处理
            var existing = await dbContext.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

            if (existing != null)
            {
                if (string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    // 同 Key + 同请求内容 -> 返回首次结果，不重复执行
                    return Result.Success();
                }

                // 同 Key + 不同请求内容 -> 409 Conflict
                return Error.Conflict(
                    "idempotency_key_mismatch",
                    $"Idempotency key '{key}' was already used with a different request payload.");
            }

            throw;
        }
    }

    public async Task<Result<TResponse>> ExecuteAsync<TResponse>(
        string key,
        object requestPayload,
        Func<Task<Result<TResponse>>> action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return await action();
        }

        var requestHash = ComputePayloadHash(requestPayload);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var record = new IdempotencyRecord(
                key,
                requestHash,
                "{}",
                200,
                dateTimeProvider.UtcNow);

            await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var result = await action();
            if (!result.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            var responseJson = JsonSerializer.Serialize(result.Value, SerializerOptions);
            var entry = dbContext.Entry(record);
            entry.Property(r => r.ResponseJson).CurrentValue = responseJson;
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);

            var existing = await dbContext.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

            if (existing != null)
            {
                if (string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    var cachedValue = JsonSerializer.Deserialize<TResponse>(existing.ResponseJson, SerializerOptions);
                    return cachedValue != null ? Result<TResponse>.Success(cachedValue) : Result<TResponse>.Success(default!);
                }

                return Error.Conflict(
                    "idempotency_key_mismatch",
                    $"Idempotency key '{key}' was already used with a different request payload.");
            }

            throw;
        }
    }

    private static string ComputePayloadHash(object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
