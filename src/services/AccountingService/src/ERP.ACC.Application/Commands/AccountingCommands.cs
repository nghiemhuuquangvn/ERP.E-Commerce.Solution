using ERP.ACC.Application.DTOs;
using MediatR;

namespace ERP.ACC.Application.Commands
{
    public class CreateAccountCommand : IRequest<AccountDto>
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AccountType { get; set; }
        public int NormalBalance { get; set; }
    }

    public class CreateInvoiceCommand : IRequest<InvoiceDto>
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class MarkInvoicePaidCommand : IRequest<InvoiceDto>
    {
        public Guid InvoiceId { get; set; }
    }

    public class CreateBillCommand : IRequest<BillDto>
    {
        public string BillNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid POId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class ApproveBillCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }
    }

    public class MarkBillPaidCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }
    }

    public class PostJournalEntryCommand : IRequest<JournalEntryDto>
    {
        public string EntryNumber { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<JournalLineDto> Lines { get; set; } = new();
    }

    public class OpenFiscalPeriodCommand : IRequest<FiscalPeriodDto>
    {
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class CloseFiscalPeriodCommand : IRequest<FiscalPeriodDto>
    {
        public Guid FiscalPeriodId { get; set; }
    }
}
