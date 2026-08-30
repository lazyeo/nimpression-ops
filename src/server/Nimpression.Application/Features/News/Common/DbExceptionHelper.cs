namespace Nimpression.Application.Features.News.Common;

/// <summary>
/// 数据库异常辅助工具。用于检测唯一约束冲突（如 PostgreSQL 23505）。
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
