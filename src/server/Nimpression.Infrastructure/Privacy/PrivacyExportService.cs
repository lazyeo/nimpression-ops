using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Infrastructure.Privacy;

/// <summary>
/// 个人数据导出 Zip 归档生成服务（AC N2.4 / NZ Privacy Act 2020 IPP 6）。
/// 纯内存流构建包含规范 JSON 数据包与法律说明文档的标准 ZIP 文件，不依赖临时磁盘文件。
/// </summary>
public sealed class PrivacyExportService : IPrivacyExportService
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<byte[]> CreateExportZipArchiveAsync(
        DriverPersonalDataExportDto exportData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. 结构化 JSON 数据文件
            var jsonEntry = archive.CreateEntry("driver_data_export.json", CompressionLevel.Optimal);
            using (var entryStream = jsonEntry.Open())
            using (var writer = new Utf8JsonWriter(entryStream, new JsonWriterOptions { Indented = true }))
            {
                JsonSerializer.Serialize(writer, exportData, IndentedJsonOptions);
            }

            // 2. 法律与权利说明文本 (README.txt)
            var readmeEntry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            using (var entryStream = readmeEntry.Open())
            using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
            {
                var readmeContent = BuildReadmeNotice(exportData);
                writer.Write(readmeContent);
            }
        }

        return Task.FromResult(memoryStream.ToArray());
    }

    private static string BuildReadmeNotice(DriverPersonalDataExportDto exportData)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("======================================================================");
        sb.AppendLine("NIMPRESSION OPS — PERSONAL DATA EXPORT ARCHIVE");
        sb.AppendLine("Pursuant to New Zealand Privacy Act 2020, Information Privacy Principle 6 (IPP 6)");
        sb.AppendLine("======================================================================");
        sb.AppendLine(inv, $"Export Timestamp (UTC): {exportData.Metadata.ExportedAt:yyyy-MM-dd HH:mm:ss 'UTC'}");
        sb.AppendLine(inv, $"Subject User Name:      {exportData.User.DisplayName}");
        sb.AppendLine(inv, $"Subject User Email:     {exportData.User.Email}");
        sb.AppendLine(inv, $"Driver Employee No:     {exportData.Driver?.EmployeeNo ?? "N/A"}");
        sb.AppendLine(inv, $"Export Request ID:      {exportData.Metadata.ExportRequestId}");
        sb.AppendLine(inv, $"Data Controller:        {exportData.Metadata.OrganizationName}");
        sb.AppendLine(inv, $"Applicable Jurisdiction: {exportData.Metadata.Jurisdiction}");
        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("ARCHIVE CONTENTS:");
        sb.AppendLine("1. driver_data_export.json");
        sb.AppendLine("   Complete structured JSON export encompassing:");
        sb.AppendLine("   - Identity profile & contact information");
        sb.AppendLine(inv, $"   - Clock-in/Clock-out Timesheet shifts ({exportData.Shifts.Count} records)");
        sb.AppendLine(inv, $"   - Assigned dispatch job tasks ({exportData.Tasks.Count} records)");
        sb.AppendLine(inv, $"   - Statutory payslips & itemised earnings lines ({exportData.Payslips.Count} payslips)");
        sb.AppendLine(inv, $"   - Incident reports & safety disclosures ({exportData.Incidents.Count} records)");
        sb.AppendLine(inv, $"   - Infringement & traffic fines ({exportData.Fines.Count} records)");
        sb.AppendLine(inv, $"   - Privacy consent & policy acceptance audit trail ({exportData.Consents.Count} records)");
        sb.AppendLine();
        sb.AppendLine("2. README.txt");
        sb.AppendLine("   This legal notice and data inventory summary.");
        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("YOUR RIGHTS UNDER THE PRIVACY ACT 2020:");
        sb.AppendLine("- IPP 6 (Access): You have the right to receive a copy of your personal info.");
        sb.AppendLine("- IPP 7 (Correction): You have the right to request correction of inaccurate data.");
        sb.AppendLine();
        sb.AppendLine("For data correction inquiries, contact privacy@nimpression.co.nz.");
        sb.AppendLine("======================================================================");
        return sb.ToString();
    }
}
