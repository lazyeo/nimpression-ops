namespace Nimpression.Api.Endpoints;

/// <summary>
/// 一组相关端点的注册单元。实现类由程序集扫描自动发现并挂载 ——
/// **新增一组端点只需新增一个文件，不必回到 Program.cs 添一行**。
///
/// 这不只是洁癖：Program.cs 是所有功能切片的必经之地，
/// 一旦每个切片都要改它，并行开发就必然在这一个文件上反复冲突。
/// </summary>
public interface IEndpointModule
{
    /// <summary>把本模块的端点挂到路由树上。</summary>
    void MapEndpoints(IEndpointRouteBuilder routes);
}
