using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Application.Features.Privacy.Abstractions;

/// <summary>
/// 个人数据导出 Zip 打包服务（AC N2.4）。
/// 将个人全量结构化 JSON 数据以及说明文档/附件压缩为标准的 .zip 归档文件。
/// </summary>
public interface IPrivacyExportService
{
    /// <summary>
    /// 将导出载荷生成为 Zip 压缩包字节流。
    /// </summary>
    Task<byte[]> CreateExportZipArchiveAsync(DriverPersonalDataExportDto exportData, CancellationToken cancellationToken = default);
}
