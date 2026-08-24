namespace Nimpression.Application.Features.Vehicles.Common;

/// <summary>
/// 数据库异常检查辅助类。安全识别 Npgsql / PostgreSQL 唯一约束违规（SqlState 23505）而无需硬依赖 Npgsql 程序集。
/// </summary>
public static class DbExceptionHelper
{
    /// <summary>
    /// 判断给定的异常是否源于数据库唯一约束冲突（如 SqlState 23505）。
    /// </summary>
    public static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (string.Equals(current.GetType().Name, "PostgresException", StringComparison.Ordinal))
            {
                var sqlStateProp = current.GetType().GetProperty("SqlState");
                if (sqlStateProp?.GetValue(current)?.ToString() == "23505")
                {
                    return true;
                }
            }

            if (current.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
