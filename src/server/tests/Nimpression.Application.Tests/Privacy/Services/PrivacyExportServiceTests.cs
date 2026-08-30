using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Infrastructure.Privacy;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Services;

public sealed class PrivacyExportServiceTests
{
    [Fact]
    public async Task CreateExportZipArchiveAsync_generates_valid_zip_with_json_and_readme()
    {
        var service = new PrivacyExportService();
        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero);

        var data = new DriverPersonalDataExportDto(
            new ExportMetadataDto(Guid.NewGuid(), userId, now, "NZ Privacy Act 2020 IPP 6", "Nimpression Ops", "NZ"),
            new UserExportDto(userId, "driver.test@nimpression.co.nz", "Arthur Dent", "Driver", "Active", "en-NZ", now.AddYears(-1), now.AddDays(-1)),
            new DriverProfileExportDto(driverId, "EMP-9001", "Class 4", new DateOnly(2027, 5, 1), new DateOnly(2025, 1, 1), "Active", 35m, "NZD", 45m, "NZD", 0.9m, "NZD", "+64 21 000 1111", "42 Galaxy Way", "Ford Prefect"),
            [
                new ShiftExportDto(Guid.NewGuid(), now.AddDays(-2), now.AddDays(-2).AddHours(8), 30, "Completed", "Smooth run", 7.5m)
            ],
            [
                new JobTaskExportDto(Guid.NewGuid(), "TSK-001", "Auckland Port Cargo", "Container dropoff", now.AddDays(-2), "High", "Completed", now.AddDays(-2), now.AddDays(-2), now.AddDays(-2).AddHours(4), 50m, 52m)
            ],
            [
                new PayslipExportDto(Guid.NewGuid(), Guid.NewGuid(), "Period 2026-08-01 - 2026-08-14", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 14), "HoursBased", 1500m, "NZD", 1500m, 1200m, false, now.AddDays(-10), now.AddDays(-8), [
                    new PayslipLineExportDto(Guid.NewGuid(), "HoursBased", "OrdinaryHours", "Standard wage", 40m, null, null, 35m, 1400m)
                ])
            ],
            [],
            [],
            [
                new ConsentRecordExportDto("2026.1", now.AddYears(-1), "192.168.1.1", "Browser/Agent")
            ]);

        // Act
        var zipBytes = await service.CreateExportZipArchiveAsync(data);

        // Assert
        zipBytes.Should().NotBeNullOrEmpty();

        using var memoryStream = new MemoryStream(zipBytes);
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        zipArchive.Entries.Should().Contain(e => e.FullName == "driver_data_export.json");
        zipArchive.Entries.Should().Contain(e => e.FullName == "README.txt");

        // Verify JSON entry content
        var jsonEntry = zipArchive.GetEntry("driver_data_export.json")!;
        using var stream = jsonEntry.Open();
        using var doc = await JsonDocument.ParseAsync(stream);

        doc.RootElement.GetProperty("metadata").GetProperty("jurisdiction").GetString().Should().Be("NZ");
        doc.RootElement.GetProperty("user").GetProperty("displayName").GetString().Should().Be("Arthur Dent");
        doc.RootElement.GetProperty("driver").GetProperty("employeeNo").GetString().Should().Be("EMP-9001");
        doc.RootElement.GetProperty("shifts").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("payslips").GetArrayLength().Should().Be(1);

        // Verify README.txt
        var readmeEntry = zipArchive.GetEntry("README.txt")!;
        using var readmeStream = readmeEntry.Open();
        using var reader = new StreamReader(readmeStream);
        var readmeText = await reader.ReadToEndAsync();
        readmeText.Should().Contain("NIMPRESSION OPS — PERSONAL DATA EXPORT ARCHIVE");
        readmeText.Should().Contain("Arthur Dent");
        readmeText.Should().Contain("Information Privacy Principle 6 (IPP 6)");
    }
}
