using ERP.ACC.Application.Commands;
using FluentValidation;

namespace ERP.ACC.Application.Validators
{
    public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
    {
        public CreateInvoiceCommandValidator()
        {
            RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.OrderId).NotEmpty();
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.DueDate).GreaterThan(DateTime.UtcNow);
        }
    }

    public class PostJournalEntryCommandValidator : AbstractValidator<PostJournalEntryCommand>
    {
        public PostJournalEntryCommandValidator()
        {
            RuleFor(x => x.EntryNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.FiscalPeriodId).NotEmpty();
            RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Lines).NotEmpty().WithMessage("Journal entry must have at least one line.");
            RuleFor(x => x.Lines)
                .Must(lines => lines != null && lines.Sum(l => l.Debit) == lines.Sum(l => l.Credit))
                .WithMessage("Journal entry must be balanced: total debits must equal total credits.");
        }
    }

    public class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
    {
        public CreateBillCommandValidator()
        {
            RuleFor(x => x.BillNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.SupplierId).NotEmpty();
            RuleFor(x => x.POId).NotEmpty();
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.DueDate).GreaterThan(DateTime.UtcNow);
        }
    }

    public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
    {
        public CreateAccountCommandValidator()
        {
            RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.AccountType).InclusiveBetween(1, 5);
            RuleFor(x => x.NormalBalance).InclusiveBetween(1, 2);
        }
    }

    public class OpenFiscalPeriodCommandValidator : AbstractValidator<OpenFiscalPeriodCommand>
    {
        public OpenFiscalPeriodCommandValidator()
        {
            RuleFor(x => x.PeriodName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
                .WithMessage("End date must be after start date.");
        }
    }
}
