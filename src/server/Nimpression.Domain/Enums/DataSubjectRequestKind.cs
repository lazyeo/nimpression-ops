namespace Nimpression.Domain.Enums;

/// <summary>
/// 隐私主体请求类别（数据查阅导出 / 匿名化删除 / 更正）。
/// </summary>
public enum DataSubjectRequestKind
{
    Export = 1,
    Deletion = 2,
    Rectification = 3,
}
