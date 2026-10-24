using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2e: the report profile (company header, signatories, amount unit).</summary>
public class ReportProfileTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private ReportProfile? _stored;
    private readonly Mock<IReportProfileRepository> _repo = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();

    public ReportProfileTests()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored);
        _repo.Setup(r => r.AddAsync(It.IsAny<ReportProfile>(), It.IsAny<CancellationToken>()))
            .Callback<ReportProfile, CancellationToken>((p, _) => _stored = p).Returns(Task.CompletedTask);
        _uow.SetupGet(u => u.ReportProfiles).Returns(_repo.Object);
    }

    private UpdateReportProfileCommandHandler Update()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Tenant);
        return new UpdateReportProfileCommandHandler(_uow.Object, tenant.Object);
    }

    [Fact]
    public async Task A_tenant_with_no_profile_gets_an_empty_header_and_plain_vnd()
    {
        var dto = await new GetReportProfileQueryHandler(_repo.Object).Handle(new GetReportProfileQuery(), default);

        dto.CompanyName.Should().BeEmpty();
        dto.AmountUnit.Should().Be("Vnd");
    }

    [Fact]
    public async Task The_first_save_creates_the_tenants_row_and_a_second_save_updates_it()
    {
        await Update().Handle(new UpdateReportProfileCommand
        {
            CompanyName = "  Công ty TNHH Mẫu ", TaxCode = "0312345678", Address = "1 Lê Lợi, Q1", PreparedBy = "Nguyễn A", ChiefAccountant = "Trần B", Director = "Lê C", AmountUnit = "thousand",
        }, default);

        var first = _stored!;
        first.TenantId.Should().Be(Tenant);
        first.CompanyName.Should().Be("Công ty TNHH Mẫu"); // trimmed
        first.AmountUnit.Should().Be((int)AmountUnit.Thousand);

        var dto = await Update().Handle(new UpdateReportProfileCommand { CompanyName = "Mới", AmountUnit = "Million" }, default);

        _stored.Should().BeSameAs(first);
        dto.CompanyName.Should().Be("Mới");
        dto.TaxCode.Should().BeEmpty(); // a save replaces the whole profile
        dto.AmountUnit.Should().Be("Million");
        _repo.Verify(r => r.AddAsync(It.IsAny<ReportProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData("Dong")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("7")]
    public async Task An_unknown_unit_is_refused(string unit) =>
        await FluentActions.Awaiting(() => Update().Handle(new UpdateReportProfileCommand { AmountUnit = unit }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Vnd, Thousand or Million*");

    [Fact]
    public async Task Too_long_text_is_refused_and_nothing_is_saved()
    {
        await FluentActions.Awaiting(() => Update().Handle(new UpdateReportProfileCommand { CompanyName = new string('x', 201), AmountUnit = "Vnd" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Company name*");
        await FluentActions.Awaiting(() => Update().Handle(new UpdateReportProfileCommand { TaxCode = new string('1', 21), AmountUnit = "Vnd" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Tax code*");

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
