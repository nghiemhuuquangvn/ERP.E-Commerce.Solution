using System.Text;
using ERP.MFG.Infrastructure.Persistence;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace ERP.Architecture.Tests;

/// <summary>
/// The PostgreSQL variant of <see cref="LiveSchemaDriftTests"/> for ManufacturingService (R161). Opt-in: set ERP_DRIFT_PG to a server connection string
/// WITHOUT a database, for example
///   ERP_DRIFT_PG="Host=localhost;Port=5432;Username=postgres;Password=..."
/// then: dotnet test tests/unit/ERP.Architecture.Tests --filter PostgreSqlSchemaDriftTests
///
/// It creates a scratch database, applies every script of src/Database/Manufacturing in file order TWICE (the scripts must be idempotent), compares the
/// information_schema with the real MfgDbContext model and drops the scratch database. It reports
///   MISSING  a table or column the model maps but the scripts do not create,
///   TYPE     a column whose type family differs from the model's,
///   NULLABLE the model may write NULL into a NOT NULL column without default,
///   BLOCKER  a NOT NULL column without default that the model never writes (every INSERT fails).
/// The optimistic-locking column xmin is a system column and is skipped.
/// </summary>
public class PostgreSqlSchemaDriftTests(ITestOutputHelper output)
{
    private sealed class FixedTenantProvider : ITenantProvider
    {
        public Guid? CurrentTenantId => Guid.NewGuid();
    }

    private sealed record Column(string Type, bool Nullable, bool HasDefault, bool Generated);

    [Fact]
    public void TheManufacturingScriptsMatchTheModel()
    {
        var server = Environment.GetEnvironmentVariable("ERP_DRIFT_PG");
        if (string.IsNullOrWhiteSpace(server))
        {
            output.WriteLine("ERP_DRIFT_PG not set - skipped.");
            return;
        }

        var scripts = Directory.GetFiles(FindScriptsDirectory(), "*.sql").OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.NotEmpty(scripts);

        var scratch = "erp_mfg_drift_" + Guid.NewGuid().ToString("N")[..10];
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false };
        var scratchConnection = new NpgsqlConnectionStringBuilder(server) { Database = scratch, Pooling = false };
        var report = new StringBuilder();
        var tablesCompared = 0;
        var columnsCompared = 0;

        Execute(admin.ConnectionString, $"CREATE DATABASE \"{scratch}\"");
        try
        {
            // the scripts are append-only and idempotent: the second pass must change nothing and fail nowhere
            for (var pass = 1; pass <= 2; pass++)
                foreach (var script in scripts)
                {
                    try { Execute(scratchConnection.ConnectionString, File.ReadAllText(script)); }
                    catch (Exception e) { report.AppendLine($"SCRIPT   {Path.GetFileName(script)} (pass {pass}): {e.Message}"); }
                }

            var live = ReadColumns(scratchConnection.ConnectionString);
            using var context = CreateContext(server);
            var byTable = context.Model.GetEntityTypes().Where(e => e.GetTableName() != null && e.GetViewName() == null).GroupBy(e => e.GetTableName()!, StringComparer.Ordinal);

            foreach (var group in byTable)
            {
                var table = group.Key;
                var names = string.Join("+", group.Select(e => e.ClrType.Name));
                if (!live.TryGetValue(table, out var columns))
                {
                    report.AppendLine($"MISSING  {table}: table does not exist ({names})");
                    continue;
                }

                tablesCompared++;
                var written = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entity in group)
                {
                    var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
                    foreach (var property in entity.GetProperties())
                    {
                        var name = property.GetColumnName(storeObject);
                        if (name == null || name == "xmin") continue;
                        written.Add(name);
                        columnsCompared++;
                        if (!columns.TryGetValue(name, out var column))
                        {
                            report.AppendLine($"MISSING  {table}.{name} ({entity.ClrType.Name}.{property.Name})");
                            continue;
                        }

                        var modelType = property.GetColumnType(storeObject);
                        if (modelType != null && Family(modelType) != Family(column.Type))
                            report.AppendLine($"TYPE     {table}.{name}: model {modelType}, database {column.Type} ({entity.ClrType.Name}.{property.Name})");

                        if (Nullable.GetUnderlyingType(property.ClrType) != null && !property.IsShadowProperty() && !property.IsForeignKey() && !column.Nullable && !column.HasDefault)
                            report.AppendLine($"NULLABLE {table}.{name}: model may write NULL, column is NOT NULL ({entity.ClrType.Name}.{property.Name})");
                    }
                }

                foreach (var blocker in columns.Where(c => !written.Contains(c.Key) && !c.Value.Nullable && !c.Value.HasDefault && !c.Value.Generated).Select(c => c.Key).OrderBy(c => c, StringComparer.Ordinal))
                    report.AppendLine($"BLOCKER  {table}.{blocker}: NOT NULL, no default, not written by {names}");
            }
        }
        finally
        {
            try { Execute(admin.ConnectionString, $"DROP DATABASE IF EXISTS \"{scratch}\" WITH (FORCE)"); }
            catch (Exception e) { output.WriteLine($"Could not drop {scratch}: {e.Message}"); }
        }

        output.WriteLine(report.Length == 0 ? $"No drift found ({scripts.Count} scripts applied twice, {tablesCompared} tables and {columnsCompared} columns compared)." : report.ToString());
        Assert.True(tablesCompared > 20 && columnsCompared > 300, $"The comparison looks empty: {tablesCompared} tables, {columnsCompared} columns.");
        Assert.True(report.Length == 0, report.ToString());
    }

    private static string FindScriptsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Database", "Manufacturing");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("src/Database/Manufacturing was not found above " + AppContext.BaseDirectory);
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, Dictionary<string, Column>> ReadColumns(string connectionString)
    {
        const string sql = """
            SELECT table_name, column_name, data_type, is_nullable = 'YES', column_default IS NOT NULL OR is_identity = 'YES', is_generated = 'ALWAYS'
            FROM information_schema.columns WHERE table_schema = 'public'
            """;
        var result = new Dictionary<string, Dictionary<string, Column>>(StringComparer.Ordinal);
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!result.TryGetValue(reader.GetString(0), out var columns))
                result[reader.GetString(0)] = columns = new Dictionary<string, Column>(StringComparer.Ordinal);
            columns[reader.GetString(1)] = new Column(reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5));
        }

        return result;
    }

    // The same kind of value is the same family: the length and precision are not compared.
    private static string Family(string type)
    {
        var name = type.Trim().ToLowerInvariant();
        var paren = name.IndexOf('(');
        if (paren > 0) name = (name[..paren] + name[(name.IndexOf(')') + 1)..]).Trim();
        return name switch
        {
            "character varying" or "varchar" or "character" or "char" or "text" or "citext" => "text",
            "int2" => "smallint",
            "int4" or "int" => "integer",
            "int8" => "bigint",
            "bool" => "boolean",
            "timestamptz" => "timestamp with time zone",
            "timestamp" => "timestamp without time zone",
            "decimal" => "numeric",
            _ => name,
        };
    }

    private static DbContext CreateContext(string server)
    {
        var options = new DbContextOptionsBuilder<MfgDbContext>().UseNpgsql(server).Options;
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext { TenantId = Guid.NewGuid().ToString() });
        userContext.Setup(u => u.GetTenantId()).Returns(Guid.NewGuid().ToString());
        var services = new ServiceCollection()
            .AddSingleton<ITenantProvider>(new FixedTenantProvider())
            .AddSingleton(userContext.Object)
            .AddSingleton(options)
            .BuildServiceProvider();
        return (DbContext)ActivatorUtilities.CreateInstance(new AutoMockProvider(services), typeof(MfgDbContext));
    }
}
