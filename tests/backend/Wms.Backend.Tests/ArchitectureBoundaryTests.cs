using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Wms.Backend.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DomainModulesReferenceOnlyApprovedProjects()
    {
        var allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wms.Tenancy"] = ["Wms.BuildingBlocks"],
            ["Wms.SecurityAudit"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.Layout"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.MasterData"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.Inventory"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.Inbound"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.Outbound"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.TaskExecution"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.Integration"] = ["Wms.BuildingBlocks", "Wms.Tenancy"],
            ["Wms.MobileSync"] = ["Wms.BuildingBlocks", "Wms.Tenancy", "Wms.TaskExecution"]
        };

        foreach (var (module, approved) in allowed)
        {
            var project = XDocument.Load(Path.Combine(RepositoryRoot, "src", "backend", module, $"{module}.csproj"));
            var references = project.Descendants("ProjectReference")
                .Select(x => Path.GetFileNameWithoutExtension((string?)x.Attribute("Include")))
                .Where(x => x is not null)
                .Cast<string>();
            references.Should().OnlyContain(reference => approved.Contains(reference), $"{module} must not query another module through a project dependency");
        }
    }

    [Fact]
    public void EachDbContextMapsOnlyToItsOwnedSchema()
    {
        var schemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wms.Tenancy"] = "tenancy", ["Wms.SecurityAudit"] = "security_audit",
            ["Wms.Layout"] = "layout", ["Wms.MasterData"] = "master_data",
            ["Wms.Inventory"] = "inventory", ["Wms.Inbound"] = "inbound",
            ["Wms.Outbound"] = "outbound", ["Wms.TaskExecution"] = "task_execution",
            ["Wms.Integration"] = "integration", ["Wms.MobileSync"] = "mobile_sync"
        };

        var mapping = new Regex("ToTable\\(\\s*\"[^\"]+\"\\s*,\\s*\"(?<schema>[^\"]+)\"", RegexOptions.Compiled);
        foreach (var (module, ownedSchema) in schemas)
        {
            var mappings = Directory.GetFiles(Path.Combine(RepositoryRoot, "src", "backend", module), "*.cs", SearchOption.AllDirectories)
                .SelectMany(path => mapping.Matches(File.ReadAllText(path)).Select(match => match.Groups["schema"].Value));
            mappings.Should().OnlyContain(schema => schema == ownedSchema, $"{module} owns only schema {ownedSchema}");
        }
    }

    [Fact]
    public void MigrationDefinesModuleRolesAndAppendOnlyRevokes()
    {
        var migration = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "backend", "database", "001_initial.sql"));
        foreach (var role in new[] { "tenancy", "security_audit", "layout", "master_data", "inventory", "inbound", "outbound", "task_execution", "integration", "mobile_sync" })
            migration.Should().Contain($"wms_mod_{role}");

        migration.Should().Contain("REVOKE UPDATE, DELETE ON inventory.movement");
        migration.Should().Contain("REVOKE UPDATE, DELETE ON security_audit.audit_record");
        migration.Should().Contain("FORCE ROW LEVEL SECURITY");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json")) && Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
