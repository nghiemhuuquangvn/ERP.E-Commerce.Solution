using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.MFG.UnitTests.Orders;

internal sealed class StockDocRepoFake : IStockDocumentRepository
{
    public readonly List<MaterialTransaction> Docs = [];

    public Task<MaterialTransaction?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Docs.FirstOrDefault(d => d.Id == id && !d.IsDeleted));
    public Task<MaterialTransaction?> FindByKeyAsync(string idempotencyKey, CancellationToken ct = default) => Task.FromResult(Docs.FirstOrDefault(d => d.IdempotencyKey == idempotencyKey));

    public Task<(List<MaterialTransaction> Items, int Total)> ListAsync(StockDocumentFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Docs.Where(d => d.PlantId == f.PlantId && (f.Status == null || d.Status == f.Status) && (f.Type == null || d.Type == f.Type) && (f.OrderId == null || d.OrderId == f.OrderId)).ToList();
        return Task.FromResult((query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), query.Count));
    }

    public Task<List<MaterialTransaction>> ListForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Docs.Where(d => d.OrderId == orderId).ToList());

    public Task<List<StockFlowLine>> FlowsAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(Docs.Where(d => d.OrderId == orderId && d.Status is StockDocumentStatus.Pending or StockDocumentStatus.Posted)
            .SelectMany(d => d.Lines.Where(l => l.Status is StockLineStatus.Pending or StockLineStatus.Posted).Select(l => new StockFlowLine(l.OrderMaterialId, d.Type, d.Status, d.Mode, l.Status, l.Qty, l.PostedLotId ?? l.LotId, l.UnitCost, l.Kind, l.OrderOutputId)))
            .ToList());

    public Task<int> CountPendingAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Docs.Count(d => d.OrderId == orderId && d.Status == StockDocumentStatus.Pending));

    public Task<List<string>> LotNumbersInUseAsync(Guid itemId, CancellationToken ct = default)
        => Task.FromResult(Docs.Where(d => d.Status is StockDocumentStatus.Pending or StockDocumentStatus.Posted)
            .SelectMany(d => d.Lines.Where(l => l.Kind == StockLineKind.Receipt && l.ItemId == itemId && l.LotNumber != null && l.Status is StockLineStatus.Pending or StockLineStatus.Posted).Select(l => l.LotNumber!)).ToList());

    public Task<int> CountHeldLotsAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(Docs.Where(d => d.OrderId == orderId && d.Status == StockDocumentStatus.Posted)
            .SelectMany(d => d.Lines).Count(l => l.Kind == StockLineKind.Receipt && l.IsQcHold && l.QcReleasedAt == null && l.Status == StockLineStatus.Posted));

    public Task<List<(Guid TenantId, Guid DocumentId)>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default)
        => Task.FromResult(Docs.Where(d => d.Status == StockDocumentStatus.Pending && (d.NextAttemptAt == null || d.NextAttemptAt <= nowUtc)).Take(take).Select(d => (d.TenantId, d.Id)).ToList());

    public void Add(MaterialTransaction document) => Docs.Add(document);
}

/// <summary>DS-01, a released order, a fake Warehouse that books movements, and the P3-3 handlers (test cases TC-MAT-*, TC-GEN-010).</summary>
internal class StockCtx : ReleaseCtx
{
    public ReasonCode OverIssueReason { get; }
    public StockPostingService Posting => new(Stock, F.ProductClient, Repo, F.Uow, PlantTime, Sync, Reports, new OperationLogCloser(TimeLogs, PlantTime), PlantSettings, NullLogger<StockPostingService>.Instance);

    public StockCtx()
    {
        OverIssueReason = ReasonCode.Create(ReasonType.OverIssue, "OVI-01", "Over standard loss", null);
        F.Reasons.Add(OverIssueReason);
    }

    /// <summary>Seeds DS-01, stocks it, creates and releases MO-A (TP-BAN01 x 100) and returns the stored order.</summary>
    public async Task<ProductionOrder> ReleasedAsync(decimal quantity = 100)
    {
        var draft = await DraftAsync(quantity);
        await ReleaseAsync(draft.Id);
        return Stored(draft.Id);
    }

    public static IUserContextService UserWith(params string[] permissions) => new FakeUser(Guid.NewGuid(), permissions);

    public MaterialLineBody Line(ProductionOrder order, string component, decimal quantity, Guid? lot = null)
        => new(order.Materials.Single(m => m.ComponentCode == component).LineNo, quantity, lot);

    public ProductionOrderMaterial Mat(ProductionOrder order, string component) => order.Materials.Single(m => m.ComponentCode == component);

    public Task<StockDocumentResultDto> IssueAsync(ProductionOrder order, IReadOnlyList<MaterialLineBody> lines, Guid? reason = null, string? key = null, IUserContextService? user = null, IStockPostingService? posting = null)
        => new IssueMaterialHandler(Access, Repo, Documents, Stock, F.ProductClient, F.UnitClient, PlantSettings, F.ReasonRepo, Sync, Numbers, PlantTime, posting ?? Posting, user ?? User, F.Uow)
            .Handle(new IssueMaterialCommand(order.Id, lines, reason, null, key), default);

    public Task<StockDocumentResultDto> ReturnAsync(ProductionOrder order, IReadOnlyList<MaterialLineBody> lines, string? key = null)
        => new ReturnMaterialHandler(Access, Repo, Documents, F.ProductClient, F.UnitClient, Numbers, PlantTime, Posting, User, F.Uow)
            .Handle(new ReturnMaterialCommand(order.Id, lines, null, null, key), default);

    public async Task<MaterialTransaction> LastDocAsync() => await Task.FromResult(Documents.Docs.Last());
}
