using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.ACC.Domain.Events;
using FluentAssertions;
using Xunit;

namespace ERP.ACC.UnitTests.Domain;

public class JournalEntryTests
{
    private static readonly Guid Period = Guid.NewGuid();
    private static readonly Guid Ar = Guid.NewGuid();
    private static readonly Guid Revenue = Guid.NewGuid();

    private static JournalEntry Invoice(decimal amount = 1250.50m) =>
        JournalEntry.CreatePosted("JE-2026-000001", Period, new DateTime(2026, 10, 23, 14, 30, 0), "Invoice INV-1",
            "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Ar, amount, 0), new JournalLineDraft(Revenue, 0, amount) });

    [Fact]
    public void CreatePosted_BuildsABalancedPostedEntry_WithItsSourceAndDate()
    {
        var sourceId = Guid.NewGuid();

        var entry = JournalEntry.CreatePosted("JE-2026-000001", Period, new DateTime(2026, 10, 23, 14, 30, 0), "Invoice INV-1",
            "Invoice", sourceId, new[] { new JournalLineDraft(Ar, 100m, 0), new JournalLineDraft(Revenue, 0, 100m) });

        entry.Status.Should().Be((int)JournalStatus.Posted);
        entry.EntryDate.Should().Be(new DateTime(2026, 10, 23), "an accounting date has no time");
        entry.TotalDebit.Should().Be(100m);
        entry.TotalCredit.Should().Be(100m);
        entry.ReferenceType.Should().Be("Invoice");
        entry.ReferenceId.Should().Be(sourceId);
        entry.SourceKey.Should().Be($"Invoice:{sourceId:N}");
        entry.PostedAt.Should().NotBeNull();
        entry.Lines.Should().HaveCount(2);
        entry.DomainEvents.OfType<JournalEntryPostedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void CreatePosted_RoundsToCents_BeforeCheckingTheBalance()
    {
        var entry = JournalEntry.CreatePosted("JE-1", Period, DateTime.UtcNow, "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Ar, 10.005m, 0), new JournalLineDraft(Revenue, 0, 10.005m) });

        entry.TotalDebit.Should().Be(10.01m);
        entry.TotalCredit.Should().Be(10.01m);
    }

    [Fact]
    public void CreatePosted_RefusesAnUnbalancedEntry()
    {
        var act = () => JournalEntry.CreatePosted("JE-1", Period, DateTime.UtcNow, "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Ar, 100m, 0), new JournalLineDraft(Revenue, 0, 99m) });

        act.Should().Throw<InvalidOperationException>().WithMessage("*not balanced*");
    }

    [Theory]
    [InlineData(0, 0)]      // empty line
    [InlineData(10, 10)]    // both sides
    [InlineData(-5, 0)]     // negative
    public void CreatePosted_RefusesALineWithoutExactlyOnePositiveSide(double debit, double credit)
    {
        var act = () => JournalEntry.CreatePosted("JE-1", Period, DateTime.UtcNow, "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Ar, (decimal)debit, (decimal)credit), new JournalLineDraft(Revenue, 0, 10m) });

        act.Should().Throw<InvalidOperationException>().WithMessage("*exactly one positive amount*");
    }

    [Fact]
    public void CreatePosted_NeedsAtLeastTwoLines()
    {
        var act = () => JournalEntry.CreatePosted("JE-1", Period, DateTime.UtcNow, "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Ar, 10m, 0) });

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least two lines*");
    }

    [Fact]
    public void Reverse_MirrorsEveryLine_KeepsTheOriginalInTheBooks_AndKeysItself()
    {
        var original = Invoice(300m);

        var reversal = original.Reverse("JE-2026-000002", Period, new DateTime(2026, 10, 24), "Order cancelled");

        original.Status.Should().Be((int)JournalStatus.Reversed);
        original.VoidedAt.Should().NotBeNull();
        original.VoidReason.Should().Be("Order cancelled");
        reversal.Status.Should().Be((int)JournalStatus.Posted);
        reversal.Lines.Single(l => l.AccountId == Ar).Credit.Should().Be(300m);
        reversal.Lines.Single(l => l.AccountId == Revenue).Debit.Should().Be(300m);
        reversal.ReferenceType.Should().Be("InvoiceReversal");
        reversal.ReferenceId.Should().Be(original.ReferenceId);
        reversal.SourceKey.Should().Be($"InvoiceReversal:{original.ReferenceId:N}");
        reversal.Description.Should().Contain("JE-2026-000001").And.Contain("Order cancelled");
    }

    [Fact]
    public void Reverse_CannotBeDoneTwice_OrWithoutAReason()
    {
        var entry = Invoice();
        entry.Reverse("JE-2", Period, DateTime.UtcNow, "cancelled");

        var again = () => entry.Reverse("JE-3", Period, DateTime.UtcNow, "again");
        var noReason = () => Invoice().Reverse("JE-4", Period, DateTime.UtcNow, " ");

        again.Should().Throw<InvalidOperationException>().WithMessage("*not posted*");
        noReason.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheManualPostPath_NowMarksTheEntryPosted()
    {
        var entry = JournalEntry.Create("JE-M1", Period, "manual");
        entry.AddLine(Ar, 50m, 0);
        entry.AddLine(Revenue, 0, 50m);

        entry.Post(50m, 50m);

        entry.Status.Should().Be((int)JournalStatus.Posted);
        entry.PostedAt.Should().NotBeNull();
        entry.SourceKey.Should().BeNull("manual entries have no idempotency key");
    }

    [Fact]
    public void Invoice_ApplyPayment_MarksPaidOnlyWhenFullyCovered_AndCancelledOnesTakeNothing()
    {
        var invoice = ERP.ACC.Domain.Entities.Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(30));

        invoice.ApplyPayment(40m).Should().BeTrue();
        invoice.Status.Should().Be((int)InvoiceStatus.Unpaid);
        invoice.ApplyPayment(60m).Should().BeTrue();
        invoice.Status.Should().Be((int)InvoiceStatus.Paid);
        invoice.ApplyPayment(10m).Should().BeFalse("a paid invoice takes no more");

        var cancelled = ERP.ACC.Domain.Entities.Invoice.Create("INV-2", Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(30));
        cancelled.Cancel();
        cancelled.ApplyPayment(100m).Should().BeFalse();
        cancelled.Status.Should().Be((int)InvoiceStatus.Cancelled);
        invoice.Cancel(); // the order was cancelled after payment: the cash stays as a credit until the refund is booked
        invoice.Status.Should().Be((int)InvoiceStatus.Cancelled);
        invoice.AmountPaid.Should().Be(100m);
    }

    [Fact]
    public void PostingAccountMapping_OnlyAcceptsKnownKeys()
    {
        PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.Cash, Guid.NewGuid()).PostingKey.Should().Be("Cash");

        var act = () => PostingAccountMapping.Create(Guid.NewGuid(), "Nonsense", Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
        PostingKeys.DefaultAccountNumbers.Keys.Should().BeEquivalentTo(PostingKeys.All, "every key has a default account");
    }
}
