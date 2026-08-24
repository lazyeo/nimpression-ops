namespace Nimpression.Application.Features.Areas.Common;

/// <summary>
/// 数据库异常检查辅助类。安全识别 PostgreSQL 唯一约束与外键约束违规。
/// </summary>
public static class DbExceptionHelper
{
    /// <summary>
    /// 判断给定的异常是否源于数据库唯一约束冲突（SqlState 23505）。
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

    /// <summary>
    /// 判断给定的异常是否源于数据库外键约束限制冲突（SqlState 23503）。
    /// </summary>
    public static bool IsForeignKeyViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (string.Equals(current.GetType().Name, "PostgresException", StringComparison.Ordinal))
            {
                var sqlStateProp = current.GetType().GetProperty("SqlState");
                if (sqlStateProp?.GetValue(current)?.ToString() == "23503")
                {
                    return true;
                }
            }

            if (current.Message.Contains("23503", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("foreign key constraint", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("violates foreign key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
