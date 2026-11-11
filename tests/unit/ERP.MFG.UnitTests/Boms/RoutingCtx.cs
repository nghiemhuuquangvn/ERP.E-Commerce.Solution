using ERP.MFG.Application.Features.Imports;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Common;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>
/// DS-01 routings (test cases §1.3 and §1.6) on top of the BOM fixture: shifts / CAL-5D, the work centers WC-ASM (labor), WC-PNT (machine, efficiency 80 %),
/// WC-PKG (labor), WC-CUT (machine, 2 resources, utilization 90 %), a subcontract center and the standard operations.
/// </summary>
internal class RoutingCtx : BomCtx
{
    public Ds01 D { get; }
    public Dictionary<string, WorkCenter> Centers { get; } = [];

    public RoutingCtx()
    {
        D = new Ds01(Plant.Id);
        F.Of<Shift>().AddRange([D.Ca1, D.Ca2, D.Ca3]);
        F.Calendars.Add(D.Cal5D);
        F.WorkingTime.Build = _ => D.Calculator;

        Center("WC-ASM", WorkCenterType.Labor, 3, 100, 100);
        Center("WC-PNT", WorkCenterType.Machine, 1, 80, 100);
        Center("WC-PKG", WorkCenterType.Labor, 2, 100, 100);
        Center("WC-CUT", WorkCenterType.Machine, 2, 100, 90);
        Center("WC-OUT", WorkCenterType.Subcontract, 1, 100, 100, vendor: Guid.NewGuid());
    }

    private void Center(string code, WorkCenterType type, int resources, decimal eff, decimal util, Guid? vendor = null)
    {
        var center = WorkCenter.Create(Plant.Id, code, Ds01.CenterValues(D.Cal5D.Id, type, resources, eff, util, 50_000, 20_000, vendor));
        F.Of<WorkCenter>().Add(center);
        Centers[code] = center;
    }

    public StandardOperation Standard(string code, string center, decimal setup, decimal run, RunTimeBasis basis = RunTimeBasis.PerUnit, decimal? batch = null)
    {
        var op = StandardOperation.Create(Plant.Id, code, "Standard " + code, Centers[center].Id, setup, run, basis, batch, "Do it well");
        F.Of<StandardOperation>().Add(op);
        return op;
    }

    // ── Handlers ────────────────────────────────────────────────────────────────────────────────────────────
    public Task<RoutingDto> NewRoutingAsync(string item, DateOnly? from = null, DateOnly? to = null)
        => new CreateRoutingHandler(Access, F.RoutingRepo, F.ProductClient, F.Uow).Handle(new CreateRoutingCommand(Plant.Id, Items[item].Id, from ?? Jan1, to, null), default);

    public static RoutingOperationBody Op(int? seq, string name, Guid? center, decimal setup, decimal run, RunTimeBasis basis = RunTimeBasis.PerUnit, decimal? batch = null,
        int queue = 0, int move = 0, bool subcontract = false)
        => new(seq, null, name, center, setup, run, basis, batch, queue, move, subcontract, null);

    public Task<RoutingDto> AddOpAsync(Guid routingId, RoutingOperationBody body)
        => new AddRoutingOperationHandler(Access, F.RoutingRepo, F.Catalog, F.PlanRepo, F.Uow).Handle(new AddRoutingOperationCommand(routingId, body), default);

    public Task<RoutingDto> AddOpAsync(Guid routingId, int seq, string name, string center, decimal setup, decimal run, RunTimeBasis basis = RunTimeBasis.PerUnit, decimal? batch = null,
        int queue = 0, int move = 0)
        => AddOpAsync(routingId, Op(seq, name, Centers[center].Id, setup, run, basis, batch, queue, move));

    public Task<RoutingDto> ApproveRoutingAsync(Guid routingId)
        => new ApproveRoutingHandler(Access, F.RoutingRepo, F.BomRepo, F.Catalog, User, F.Uow).Handle(new ApproveRoutingCommand(routingId), default);

    public Task<RoutingDto> CopyRoutingAsync(Guid routingId, DateOnly from)
        => new CopyRoutingHandler(Access, F.RoutingRepo, F.Uow).Handle(new CopyRoutingCommand(routingId, from), default);

    public OperationTimeService Time => new(F.Catalog, F.WorkingTime);

    /// <summary>The routing of TP-BAN01 in DS-01 §1.6, approved: 10 Assembly (15 + 10/pc, WC-ASM), 20 Painting (30 + 6/pc, WC-PNT), 30 Packing (0 + 2/pc, WC-PKG).</summary>
    public async Task<RoutingDto> SeedTopRoutingAsync()
    {
        var routing = await NewRoutingAsync("TP-BAN01");
        await AddOpAsync(routing.Id, 10, "Assembly", "WC-ASM", 15, 10);
        await AddOpAsync(routing.Id, 20, "Painting", "WC-PNT", 30, 6);
        await AddOpAsync(routing.Id, 30, "Packing", "WC-PKG", 0, 2);
        return await ApproveRoutingAsync(routing.Id);
    }

    public ImportBomHandler BomImport => new(Access, F.BomRepo, F.ProfileRepo, F.ProductClient, F.UnitClient, Snapshots, F.Uow);
    public ImportRoutingHandler RoutingImport => new(Access, F.RoutingRepo, F.ProductClient, F.Catalog, F.Uow);

    public static MemoryStream Csv(params string[] lines) => new(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
}
