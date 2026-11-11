using System.Text.RegularExpressions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Warehouse upgrade W2: <c>StockLedger</c> is the only code that changes stock quantities. The item total (Inventory), the lots, the bin stock and
/// the warehouse balances move together there, so they cannot drift (<c>GET inventory/reconciliation</c> proves it). A handler that calls a quantity
/// method of those aggregates itself would change one store and not the others.
/// </summary>
public class NoStockMutationOutsideTheLedgerTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }

    // The quantity mutators of Inventory, InventoryLot and InventoryLocationStock. Policy setters (SetStockPolicy, SetReorderLevels, AssignLocation) are not stock changes.
    private static readonly Regex Mutator = new(
        @"\.(ReceiveStock|SellStock|AdjustStock|ReserveStock|ReleaseReservation|RecordPointOfSaleSale|Deplete|Replenish|SplitOff|TransferOut|TransferIn)\(",
        RegexOptions.Compiled);

    // Receive / Reserve / Consume are common words: only flag them on the bin and lot types we know are named like this in the Warehouse code.
    private static readonly Regex AmbiguousMutator = new(@"\b(locationStock|fromStock|toStock|candidate|destination|source|lot)\.(Receive|Reserve|Consume)\(", RegexOptions.Compiled);

    [Fact]
    public void OnlyTheStockLedger_ChangesStockQuantities()
    {
        var warehouse = Path.Combine(RepositoryRoot(), "src", "services", "WareHouseService", "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(warehouse, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/")) continue;
            if (normalized.Contains("/ERP.WH.Domain/")) continue;                    // the aggregates define the methods
            if (normalized.EndsWith("/Services/StockLedger.cs")) continue;           // the one allowed caller

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("///")) continue;
                if (Mutator.IsMatch(line) || AmbiguousMutator.IsMatch(line))
                    offenders.Add($"{Path.GetRelativePath(RepositoryRoot(), file)}:{i + 1}: {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "These lines change stock outside StockLedger (use IStockLedger so Inventory, lots, bins and StockBalance stay in step):\n" + string.Join("\n", offenders));
    }
}
