using System.Text.RegularExpressions;
using FluentAssertions;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Integration.Tests.Fixtures;

public class TestDataIsolationGuardTests
{
    [Fact]
    public void TestDataFactory_GeneratesUniqueAndValidEmailAddresses()
    {
        const int count = 500;
        var emails = new HashSet<string>(count);

        for (var i = 0; i < count; i++)
        {
            var email = TestDataFactory.CreateEmail($"guard_{i}");
            var added = emails.Add(email);
            added.Should().BeTrue($"Each generated email must be globally unique, but duplicate was found: {email}");

            var act = () => new EmailAddress(email);
            act.Should().NotThrow<Exception>("Generated email must be a valid EmailAddress value object.");
        }

        emails.Should().HaveCount(count);
    }

    [Fact]
    public void StaticCodeAnalysis_GuardsAgainstHardcodedSharedEmailsInIntegrationTests()
    {
        // 定位 Integration.Tests 源码目录
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var testsDirectory = FindTestsDirectory(currentDir);
        testsDirectory.Should().NotBeNull("Integration.Tests source directory must be locateable");

        var csFiles = Directory.GetFiles(testsDirectory!, "*.cs", SearchOption.AllDirectories);

        // 排除种子专用测试与防回归测试自身
        var filesToScan = csFiles
            .Where(f => !Path.GetFileName(f).Equals("DatabaseSeederTests.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("TestDataIsolationGuardTests.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains(Path.Combine("obj", "Debug")))
            .ToList();

        var forbiddenEmails = new[]
        {
            "admin@nimpression.co.nz",
            "driver1@nimpression.co.nz",
            "driver2@nimpression.co.nz",
            "dispatcher@nimpression.co.nz",
            "dispatch.north@nimpression.co.nz",
            "dispatch.south@nimpression.co.nz"
        };

        var violations = new List<string>();

        foreach (var file in filesToScan)
        {
            var content = File.ReadAllText(file);
            foreach (var email in forbiddenEmails)
            {
                if (content.Contains($"\"{email}\"", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"File '{Path.GetFileName(file)}' contains hardcoded shared email \"{email}\". Please use TestDataFactory.CreateEmail() to avoid unique constraint violations.");
                }
            }
        }

        violations.Should().BeEmpty("Integration tests must not hardcode seeded user emails, as they share the same Testcontainers database instance and will violate unique constraints.");
    }

    private static string? FindTestsDirectory(string startingPath)
    {
        var dir = new DirectoryInfo(startingPath);
        while (dir != null)
        {
            var target = Path.Combine(dir.FullName, "src", "server", "tests", "Nimpression.Integration.Tests");
            if (Directory.Exists(target))
            {
                return target;
            }

            // Also check direct directory if already inside tests
            if (dir.Name == "Nimpression.Integration.Tests" && Directory.Exists(Path.Combine(dir.FullName, "Fixtures")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
