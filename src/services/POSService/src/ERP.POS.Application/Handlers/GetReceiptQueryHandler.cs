using System.Text;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using MediatR;

namespace ERP.POS.Application.Handlers;

/// <summary>
/// Formats a plain-text receipt sized for a standard 42-column thermal printer
/// (the common width for 58mm rolls) — this service has no physical printer/ESC-POS
/// driver integration, so it produces the print-ready text a printer spooler or a
/// thermal-print library on the terminal client would send as-is, rather than a fake
/// placeholder string.
/// </summary>
public class GetReceiptQueryHandler : IRequestHandler<GetReceiptQuery, string>
{
    private const int Width = 42;
    private readonly ITransactionRepository _transactions;

    public GetReceiptQueryHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<string> Handle(GetReceiptQuery request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transaction {request.TransactionId} not found.");

        var sb = new StringBuilder();
        sb.AppendLine(Center("RECEIPT"));
        sb.AppendLine(new string('-', Width));
        sb.AppendLine($"Transaction: {t.Id.ToString("N")[..8].ToUpperInvariant()}");
        sb.AppendLine($"Date: {(t.CompletedAt ?? t.CreatedAt):yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('-', Width));

        foreach (var item in t.Items)
        {
            sb.AppendLine(item.ProductName);
            sb.AppendLine(TwoColumns($"  {item.Quantity} x {item.UnitPrice:N0}", item.LineTotal.ToString("N0")));
        }

        sb.AppendLine(new string('-', Width));
        sb.AppendLine(TwoColumns("Subtotal", t.SubTotal.ToString("N0")));
        if (t.DiscountAmount > 0)
            sb.AppendLine(TwoColumns("Discount", $"-{t.DiscountAmount:N0}"));
        if (t.TaxAmount > 0)
            sb.AppendLine(TwoColumns("Tax", t.TaxAmount.ToString("N0")));
        sb.AppendLine(TwoColumns("TOTAL", t.TotalAmount.ToString("N0")));
        sb.AppendLine(new string('-', Width));

        if (t.Tenders.Any())
        {
            foreach (var tender in t.Tenders)
                sb.AppendLine(TwoColumns(tender.PaymentMethod, tender.Amount.ToString("N0")));
        }
        else if (t.PaymentMethod != null)
        {
            sb.AppendLine(TwoColumns(t.PaymentMethod, t.TotalAmount.ToString("N0")));
        }

        sb.AppendLine(new string('-', Width));
        sb.AppendLine(Center("Thank you!"));

        return sb.ToString();
    }

    private static string Center(string text) =>
        text.Length >= Width ? text : new string(' ', (Width - text.Length) / 2) + text;

    private static string TwoColumns(string left, string right)
    {
        var padding = Width - left.Length - right.Length;
        return padding > 0 ? left + new string(' ', padding) + right : $"{left} {right}";
    }
}
