namespace Nimpression.Application;

/// <summary>
/// 供程序集扫描定位应用层的空标记类型（MediatR、FluentValidation、Mapster 的注册）。
/// 用标记类型而非字符串程序集名，重命名时编译期即报错。
/// </summary>
public sealed class ApplicationAssemblyMarker
{
    private ApplicationAssemblyMarker()
    {
    }
}
