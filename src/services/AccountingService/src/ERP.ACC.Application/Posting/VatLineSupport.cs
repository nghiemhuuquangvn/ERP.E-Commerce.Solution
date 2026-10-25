using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Posting
{
    internal static class VatLineSupport
    {
        public static VatLinesSplit SplitOrRefuse(IReadOnlyCollection<VatLineInput> lines, bool pricesIncludeTax)
        {
            try
            {
                return VatCalculator.SplitLines(lines, pricesIncludeTax);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }
        }

        public static IEnumerable<VatLine> ToEntities(int documentType, Guid documentId, VatLinesSplit split) =>
            split.Lines.Select(l => VatLine.Create(documentType, documentId, l.Category, l.RatePercent, l.Net, l.Tax, l.Description));

        /// <summary>Lines given for a document that already has a gross amount: they must add up to it (the amount owed does not change).</summary>
        public static VatLinesSplit SplitOfExistingGross(IReadOnlyCollection<VatLineInput> lines, decimal gross)
        {
            var split = SplitOrRefuse(lines, pricesIncludeTax: true);
            if (Math.Abs(split.Gross - gross) > 0.005m)
                throw new BusinessRuleException("Accounting", $"The VAT lines add up to {split.Gross:0.##}, not to the document amount {gross:0.##}.");
            return split;
        }
    }

    public class GetVatLinesQueryHandler : IRequestHandler<Commands.GetVatLinesQuery, List<VatLineDto>>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IVatLineRepository _lines;

        public GetVatLinesQueryHandler(IAccountingUnitOfWork uow, IVatLineRepository lines)
        {
            _uow = uow;
            _lines = lines;
        }

        public async Task<List<VatLineDto>> Handle(Commands.GetVatLinesQuery request, CancellationToken cancellationToken)
        {
            var stored = await _lines.ListAsync(request.DocumentType, new[] { request.DocumentId }, cancellationToken);
            if (stored.Count > 0)
                return stored.Select(l => new VatLineDto
                {
                    Category = l.Category == VatLine.NotTaxable ? "NotTaxable" : "Taxable", RatePercent = l.RatePercent, Net = l.Net, Tax = l.Tax, Gross = l.Net + l.Tax, Description = l.Text,
                }).ToList();

            // no stored lines: the document's own split is the one line
            decimal net, rate, tax;
            if (request.DocumentType == VatLine.InvoiceDocument)
            {
                var i = await _uow.Invoices.GetByIdAsync(request.DocumentId, cancellationToken) ?? throw new KeyNotFoundException($"Invoice {request.DocumentId} not found.");
                (net, rate, tax) = (i.EffectiveNet, i.TaxRatePercent, i.TaxAmount);
            }
            else
            {
                var b = await _uow.Bills.GetByIdAsync(request.DocumentId, cancellationToken) ?? throw new KeyNotFoundException($"Bill {request.DocumentId} not found.");
                (net, rate, tax) = (b.EffectiveNet, b.TaxRatePercent, b.TaxAmount);
            }
            return new List<VatLineDto> { new() { Category = "Taxable", RatePercent = rate, Net = net, Tax = tax, Gross = net + tax } };
        }
    }
}
