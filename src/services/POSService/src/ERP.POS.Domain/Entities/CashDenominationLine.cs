namespace ERP.POS.Domain.Entities;

public enum DenominationStage
{
    Opening = 0,
    Closing = 1
}

public class CashDenominationLine
{
    public Guid Id { get; private set; }
    public Guid CashShiftId { get; private set; }
    public DenominationStage Stage { get; private set; }
    public decimal Denomination { get; private set; }
    public int Count { get; private set; }
    public decimal LineTotal => Denomination * Count;

    private CashDenominationLine() { }

    public CashDenominationLine(Guid cashShiftId, DenominationStage stage, decimal denomination, int count)
    {
        Id = Guid.NewGuid();
        CashShiftId = cashShiftId;
        Stage = stage;
        Denomination = denomination;
        Count = count;
    }
}
