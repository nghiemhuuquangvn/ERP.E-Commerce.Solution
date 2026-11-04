using ERP.POS.Application.Commands;
using ERP.POS.Application.EventHandlers;
using ERP.POS.Application.Handlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.POS.UnitTests.Transactions;

/// <summary>R113: POS prices its own lines from the product cache; the price the terminal sends is ignored. Offline sales keep the price the customer paid and report a difference.</summary>
public class ServerPricingTests
{
    private static readonly Guid Shirt = Guid.NewGuid();

    private readonly Mock<IPosUnitOfWork> _uow = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IPosProductCacheRepository> _cache = new();
    private readonly Mock<ICashShiftRepository> _shifts = new();

    public ServerPricingTests()
    {
        _uow.SetupGet(u => u.Transactions).Returns(_transactions.Object);
        _uow.SetupGet(u => u.ProductCache).Returns(_cache.Object);
        _uow.SetupGet(u => u.CashShifts).Returns(_shifts.Object);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private void Cached(decimal price, bool active = true) =>
        _cache.Setup(c => c.GetByIdAsync(Shirt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosProductCache { ProductId = Shirt, Name = "Shirt", Sku = "TS-1", Price = price, IsActive = active });

    private Transaction OpenTransaction()
    {
        var t = Transaction.Open(Guid.NewGuid(), Guid.NewGuid());
        _transactions.Setup(r => r.GetByIdAsync(t.Id, It.IsAny<CancellationToken>())).ReturnsAsync(t);
        return t;
    }

    private Task<ERP.POS.Application.DTOs.TransactionDto> Add(Transaction t, decimal sentPrice) =>
        new AddTransactionItemCommandHandler(_uow.Object).Handle(
            new AddTransactionItemCommand { TransactionId = t.Id, ProductId = Shirt, ProductName = "Shirt", Quantity = 2, UnitPrice = sentPrice, TaxRate = 10 }, CancellationToken.None);

    [Fact]
    public async Task The_cache_price_is_booked_whatever_the_terminal_sent()
    {
        Cached(25m);
        var t = OpenTransaction();

        var reply = await Add(t, sentPrice: 0.01m);

        reply.Items.Should().ContainSingle().Which.UnitPrice.Should().Be(25m);
        reply.SubTotal.Should().Be(50m);
        reply.TaxAmount.Should().Be(5m);
    }

    [Fact]
    public async Task A_product_the_cache_does_not_know_cannot_be_sold_and_the_transaction_stays_empty()
    {
        _cache.Setup(c => c.GetByIdAsync(Shirt, It.IsAny<CancellationToken>())).ReturnsAsync((PosProductCache?)null);
        var t = OpenTransaction();

        var act = () => Add(t, sentPrice: 25m);

        var error = await act.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Message.Should().Contain("Shirt").And.Contain("no price");
        t.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_product_with_a_zero_price_is_not_sold_as_free()
    {
        Cached(0m);
        var t = OpenTransaction();

        var act = () => Add(t, sentPrice: 25m);

        await act.Should().ThrowAsync<BusinessRuleException>();
        t.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_product_switched_off_in_the_cache_is_not_sold()
    {
        Cached(25m, active: false);
        var t = OpenTransaction();

        var act = () => Add(t, sentPrice: 25m);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Adding_the_same_product_again_keeps_the_cache_price()
    {
        Cached(25m);
        var t = OpenTransaction();
        await Add(t, 1m);

        var reply = await Add(t, 99m);

        reply.Items.Should().ContainSingle().Which.Quantity.Should().Be(4);
        reply.Items.Single().UnitPrice.Should().Be(25m);
    }

    // ── offline sync ──────────────────────────────────────────────────────

    private async Task<OfflineSyncResultDto> Sync(decimal soldAt)
    {
        _transactions.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);
        var request = new OfflineTransactionRequest
        {
            ClientTransactionId = Guid.NewGuid(),
            TerminalId = Guid.NewGuid(),
            CashierId = Guid.NewGuid(),
            Items = new List<OfflineTransactionItemRequest> { new() { ProductId = Shirt, ProductName = "Shirt", Quantity = 2, UnitPrice = soldAt, TaxRate = 0 } },
            Tenders = new List<TenderRequest> { new() { PaymentMethod = "Cash", Amount = soldAt * 2 } },
        };

        var results = await new SyncOfflineTransactionsCommandHandler(_uow.Object)
            .Handle(new SyncOfflineTransactionsCommand { Transactions = new List<OfflineTransactionRequest> { request } }, CancellationToken.None);
        return results.Single();
    }

    [Fact]
    public async Task An_offline_sale_at_a_different_price_is_booked_at_the_price_paid_and_flagged()
    {
        Cached(25m);

        var result = await Sync(soldAt: 20m);

        result.Error.Should().BeNull();
        result.Transaction!.TotalAmount.Should().Be(40m);
        result.PriceWarnings.Should().ContainSingle().Which.Should().Contain("20.00").And.Contain("25.00");
    }

    [Fact]
    public async Task An_offline_sale_at_the_system_price_has_no_warning()
    {
        Cached(25m);

        var result = await Sync(soldAt: 25m);

        result.PriceWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task An_offline_sale_of_a_product_with_no_known_price_is_booked_and_flagged()
    {
        _cache.Setup(c => c.GetByIdAsync(Shirt, It.IsAny<CancellationToken>())).ReturnsAsync((PosProductCache?)null);

        var result = await Sync(soldAt: 20m);

        result.Error.Should().BeNull();
        result.PriceWarnings.Should().ContainSingle().Which.Should().Contain("no system price");
    }
}
