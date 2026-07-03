namespace ERP.WH.Domain.Events;

public class StocktakeCompletedEvent
{
    public Guid StocktakeId { get; set; }
    public string StocktakeNumber { get; set; }
    public int LinesWithVariance { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}
