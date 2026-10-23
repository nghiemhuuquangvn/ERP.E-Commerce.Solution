using System.Reflection;
using System.Text;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ERP.Architecture.Tests;

/// <summary>
/// Opt-in diagnostic: compares the REAL EF model of every service with the schema of a running
/// SQL Server, to catch databases that drifted from the code (hand-written migrations, databases
/// provisioned before a column existed). It reports
///   MISSING  a table/column the model reads or writes but the database does not have, and
///   BLOCKER  a NOT NULL column without a default that the model never writes (every INSERT fails).
///
/// Does nothing unless ERP_DRIFT_SQL is set to a server connection string without a database, e.g.
///   ERP_DRIFT_SQL="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True"
/// then: dotnet test tests/unit/ERP.Architecture.Tests --filter LiveSchemaDriftTests
/// </summary>
public class LiveSchemaDriftTests(ITestOutputHelper output)
{
    private static readonly Dictionary<string, string> DatabaseByContext = new(StringComparer.Ordinal)
    {
        ["AuthDbContext"] = "AuthDB",
        ["SalesOrderDbContext"] = "SalesDB",
        ["PurchasingDbContext"] = "PurchasingDB",
        ["WarehouseDbContext"] = "WarehouseDB",
        ["AccountingDbContext"] = "AccountingDB",
        ["CrmDbContext"] = "CrmDB",
        ["HrmDbContext"] = "HrmDB",
        ["MasterDataDbContext"] = "MasterDataDB",
        ["ProductDbContext"] = "ProductDB",
        ["PaymentDbContext"] = "PaymentDB",
        ["ShippingDbContext"] = "ShippingDB",
        ["POSDbContext"] = "PosDB",
        ["PromotionDbContext"] = "PromotionDB",
        ["NotificationDbContext"] = "NotificationDB",
        ["ChatBotDbContext"] = "ChatBotDB",
        ["ReportingDbContext"] = "ReportingDB",
    };

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public Guid? CurrentTenantId => Guid.NewGuid();
    }

    [Fact]
    public void ModelsMatchTheLiveDatabases()
    {
        var server = Environment.GetEnvironmentVariable("ERP_DRIFT_SQL");
        if (string.IsNullOrWhiteSpace(server))
        {
            output.WriteLine("ERP_DRIFT_SQL not set - skipped.");
            return;
        }

        var report = new StringBuilder();
        foreach (var contextType in TenantIsolationArchitectureTests.AllDbContextTypes().OrderBy(t => t.Name))
        {
            if (!DatabaseByContext.TryGetValue(contextType.Name, out var database))
            {
                report.AppendLine($"?? {contextType.Name}: no database mapped in this test");
                continue;
            }

            using var context = CreateContext(contextType, server);
            using var connection = new SqlConnection(new SqlConnectionStringBuilder(server) { InitialCatalog = database }.ConnectionString);
            connection.Open();

            var live = ReadColumns(connection);
            // Owned types and table splitting put several entity types in one table: judge the table
            // by the union of their columns.
            var byTable = context.Model.GetEntityTypes()
                .Where(e => e.GetTableName() != null && e.GetViewName() == null)
                .GroupBy(e => e.GetTableName()!, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byTable)
            {
                var table = group.Key;
                var names = string.Join("+", group.Select(e => e.ClrType.Name));
                var modelColumns = group
                    .SelectMany(e => e.GetProperties().Select(p => p.GetColumnName(StoreObjectIdentifier.Table(table, e.GetSchema()))))
                    .Where(c => c != null)
                    .Select(c => c!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!live.TryGetValue(table, out var columns))
                {
                    report.AppendLine($"MISSING  {database}.{table}: table does not exist ({names})");
                    continue;
                }

                foreach (var missing in modelColumns.Where(c => !columns.ContainsKey(c)).OrderBy(c => c))
                    report.AppendLine($"MISSING  {database}.{table}.{missing} ({names})");

                foreach (var blocker in columns.Where(c => !modelColumns.Contains(c.Key) && c.Value.BlocksInsert).Select(c => c.Key).OrderBy(c => c))
                    report.AppendLine($"BLOCKER  {database}.{table}.{blocker}: NOT NULL, no default, not written by {names}");
            }
        }

        output.WriteLine(report.Length == 0 ? "No drift found." : report.ToString());
        Assert.True(report.Length == 0, report.ToString());
    }

    private sealed record Column(bool BlocksInsert);

    private static Dictionary<string, Dictionary<string, Column>> ReadColumns(SqlConnection connection)
    {
        const string sql = """
            SELECT t.name, c.name,
                   CAST(CASE WHEN c.is_nullable = 0 AND dc.object_id IS NULL AND c.is_identity = 0
                             AND c.is_computed = 0 AND TYPE_NAME(c.user_type_id) <> 'timestamp' THEN 1 ELSE 0 END AS bit)
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            """;

        var result = new Dictionary<string, Dictionary<string, Column>>(StringComparer.OrdinalIgnoreCase);
        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!result.TryGetValue(reader.GetString(0), out var columns))
                result[reader.GetString(0)] = columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
            columns[reader.GetString(1)] = new Column(reader.GetBoolean(2));
        }

        return result;
    }

    private static DbContext CreateContext(Type contextType, string server)
    {
        var options = (DbContextOptions)typeof(LiveSchemaDriftTests)
            .GetMethod(nameof(Options), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(contextType)
            .Invoke(null, [server])!;

        var userContext = new Mock<IUserContextService>();
        userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext { TenantId = Guid.NewGuid().ToString() });
        userContext.Setup(u => u.GetTenantId()).Returns(Guid.NewGuid().ToString());

        var services = new ServiceCollection()
            .AddSingleton<ITenantProvider>(new FixedTenantProvider())
            .AddSingleton(userContext.Object)
            .AddSingleton(options.GetType(), options)
            .BuildServiceProvider();

        return (DbContext)ActivatorUtilities.CreateInstance(new AutoMockProvider(services), contextType);
    }

    // Only used to build the model: the connection is never opened by the context.
    private static DbContextOptions<T> Options<T>(string server) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer(server).Options;
}

/// <summary>Resolves registered services, and a Moq mock for any other interface (the model never calls them).</summary>
internal sealed class AutoMockProvider(IServiceProvider inner) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        var registered = inner.GetService(serviceType);
        if (registered != null || !serviceType.IsInterface)
            return registered;

        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(serviceType))!;
        return mock.Object;
    }
}
