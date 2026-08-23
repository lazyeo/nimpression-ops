namespace Nimpression.Domain;

/// <summary>
/// 供反射定位领域程序集用的空标记类型（架构测试、MediatR/FluentValidation 的程序集扫描）。
/// 用标记类型而非 <c>Assembly.Load("...")</c> 字符串，是为了让重命名时编译期就报错。
/// </summary>
public sealed class DomainAssemblyMarker
{
    private DomainAssemblyMarker()
    {
    }
}
