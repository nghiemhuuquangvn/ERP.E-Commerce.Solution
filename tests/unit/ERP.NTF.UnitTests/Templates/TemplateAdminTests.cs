using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Templates;

/// <summary>ROADMAP P2-1b: a shop edits its own templates, overrides the platform defaults and never changes them directly.</summary>
public class TemplateAdminTests
{
    private static readonly Guid Shop = Guid.NewGuid();
    private static readonly Guid OtherShop = Guid.NewGuid();

    private readonly Mock<ITemplateRepository> _repo = new();
    private readonly Mock<ICurrentTenantAccessor> _tenant = new();
    private readonly List<NotificationTemplate> _added = new();

    public TemplateAdminTests()
    {
        _tenant.SetupGet(t => t.CurrentTenantId).Returns(Shop);
        _repo.Setup(r => r.AddAsync(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationTemplate, CancellationToken>((t, _) => { t.TenantId = Shop; _added.Add(t); }).Returns(Task.CompletedTask);
    }

    private NotificationTemplate Template(Guid tenant, string code = "ORDER_SHIPPED")
    {
        var t = NotificationTemplate.Create(code, "Order shipped", NotificationChannel.Email, "Order {orderNumber} shipped", "Hello {customerName}, tracking {trackingNumber}", "orderNumber,customerName,trackingNumber");
        t.TenantId = tenant;
        _repo.Setup(r => r.GetByIdAsync(t.Id, It.IsAny<CancellationToken>())).ReturnsAsync(t);
        return t;
    }

    private UpdateTemplateCommandHandler Updater() => new(_repo.Object, _tenant.Object);

    private static UpdateTemplateCommand Edit(Guid id) => new() { TemplateId = id, Name = "Shipped!", Subject = "On its way: {orderNumber}", Body = "Hi {customerName}", Variables = "orderNumber, customerName, orderNumber" };

    [Fact]
    public async Task A_shop_edits_its_own_template_and_the_variables_are_tidied()
    {
        var own = Template(Shop);

        var dto = await Updater().Handle(Edit(own.Id), default);

        dto.Name.Should().Be("Shipped!");
        own.Subject.Should().Be("On its way: {orderNumber}");
        own.Variables.Should().Be("orderNumber,customerName");   // trimmed, no repeats
        dto.IsPlatformDefault.Should().BeFalse();
        _repo.Verify(r => r.UpdateAsync(own, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_shop_cannot_edit_or_switch_off_the_platform_default_it_overrides_it()
    {
        var platform = Template(WellKnownTenants.Platform);

        var edit = () => Updater().Handle(Edit(platform.Id), default);
        var off = () => new SetTemplateActiveCommandHandler(_repo.Object, _tenant.Object).Handle(new SetTemplateActiveCommand { TemplateId = platform.Id, IsActive = false }, default);

        (await edit.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("Override");
        await off.Should().ThrowAsync<BusinessRuleException>();
        platform.Name.Should().Be("Order shipped");
        platform.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task The_platform_tenant_edits_the_platform_default_directly()
    {
        _tenant.SetupGet(t => t.CurrentTenantId).Returns(WellKnownTenants.Platform);
        var platform = Template(WellKnownTenants.Platform);

        await Updater().Handle(Edit(platform.Id), default);

        platform.Name.Should().Be("Shipped!");
    }

    [Theory]
    [InlineData("", "s", "b")]            // no name
    [InlineData("n", "s", "   ")]         // no text
    public async Task A_template_needs_a_name_and_a_text(string name, string subject, string body)
    {
        var own = Template(Shop);
        var act = () => Updater().Handle(new UpdateTemplateCommand { TemplateId = own.Id, Name = name, Subject = subject, Body = body }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Switching_a_shops_own_template_off_and_on_works_and_an_unknown_id_is_not_found()
    {
        var own = Template(Shop);
        var handler = new SetTemplateActiveCommandHandler(_repo.Object, _tenant.Object);

        (await handler.Handle(new SetTemplateActiveCommand { TemplateId = own.Id, IsActive = false }, default)).IsActive.Should().BeFalse();
        (await handler.Handle(new SetTemplateActiveCommand { TemplateId = own.Id, IsActive = true }, default)).IsActive.Should().BeTrue();

        var act = () => handler.Handle(new SetTemplateActiveCommand { TemplateId = Guid.NewGuid(), IsActive = true }, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Deleting_a_shops_own_template_is_allowed_but_a_platform_template_is_never_deleted()
    {
        var own = Template(Shop);
        var platform = Template(WellKnownTenants.Platform, "PAYMENT_RECEIPT");
        var handler = new DeleteTemplateCommandHandler(_repo.Object);

        await handler.Handle(new DeleteTemplateCommand { TemplateId = own.Id }, default);
        _repo.Verify(r => r.DeleteAsync(own, It.IsAny<CancellationToken>()), Times.Once);

        var act = () => handler.Handle(new DeleteTemplateCommand { TemplateId = platform.Id }, default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*switch it off*");
        _repo.Verify(r => r.DeleteAsync(platform, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── override ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Overriding_a_platform_default_makes_the_shop_its_own_copy()
    {
        var platform = Template(WellKnownTenants.Platform);

        var dto = await new OverrideTemplateCommandHandler(_repo.Object, _tenant.Object).Handle(new OverrideTemplateCommand { TemplateId = platform.Id }, default);

        var copy = _added.Should().ContainSingle().Subject;
        copy.Code.Should().Be("ORDER_SHIPPED");
        copy.Body.Should().Be(platform.Body);
        copy.Id.Should().NotBe(platform.Id);
        dto.IsPlatformDefault.Should().BeFalse();
    }

    [Fact]
    public async Task A_template_can_be_overridden_once_and_only_when_it_is_a_platform_default()
    {
        var platform = Template(WellKnownTenants.Platform);
        var own = Template(Shop, "OTHER");
        var handler = new OverrideTemplateCommandHandler(_repo.Object, _tenant.Object);

        (await ((Func<Task>)(() => handler.Handle(new OverrideTemplateCommand { TemplateId = own.Id }, default))).Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("already your own");

        _repo.Setup(r => r.ExistsForTenantAsync("ORDER_SHIPPED", Shop, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await ((Func<Task>)(() => handler.Handle(new OverrideTemplateCommand { TemplateId = platform.Id }, default))).Should().ThrowAsync<BusinessRuleException>();

        _tenant.SetupGet(t => t.CurrentTenantId).Returns(WellKnownTenants.Platform);
        await ((Func<Task>)(() => handler.Handle(new OverrideTemplateCommand { TemplateId = platform.Id }, default))).Should().ThrowAsync<BusinessRuleException>();
        _added.Should().BeEmpty();
    }

    // ── create ────────────────────────────────────────────────────────────

    private CreateTemplateCommandHandler Creator() => new(_repo.Object, _tenant.Object);

    [Fact]
    public async Task A_new_template_gets_a_normalised_code()
    {
        var dto = await Creator().Handle(new CreateTemplateCommand { Code = " gift_card ", Name = "Gift card", Channel = NotificationChannel.Email, Subject = "Gift", Body = "Enjoy {name}" }, default);

        dto.Code.Should().Be("GIFT_CARD");
        _added.Should().ContainSingle();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("has space")]
    [InlineData("lower-case-dash")]
    public async Task A_bad_code_is_refused(string code)
    {
        var act = () => Creator().Handle(new CreateTemplateCommand { Code = code, Name = "n", Subject = "s", Body = "b" }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_code_the_shop_already_has_is_refused_but_a_platform_code_is_not_a_clash()
    {
        _repo.Setup(r => r.ExistsForTenantAsync("GIFT_CARD", Shop, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Creator().Handle(new CreateTemplateCommand { Code = "GIFT_CARD", Name = "n", Subject = "s", Body = "b" }, default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already exists*");

        await Creator().Handle(new CreateTemplateCommand { Code = "ORDER_SHIPPED", Name = "n", Subject = "s", Body = "b" }, default);   // the platform has it, the shop does not
        _added.Should().ContainSingle();
    }

    // ── preview ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_preview_fills_the_samples_ignoring_case_and_lists_what_nothing_covers()
    {
        var result = await new PreviewTemplateCommandHandler().Handle(new PreviewTemplateCommand
        {
            Subject = "Order {OrderNumber}",
            Body = "Hi {customerName}, {unknown} and {ORDERNUMBER}",
            Variables = new() { ["orderNumber"] = "SO-7", ["customerName"] = "An" }
        }, default);

        result.Subject.Should().Be("Order SO-7");
        result.Body.Should().Be("Hi An, {unknown} and SO-7");
        result.UnknownPlaceholders.Should().Equal("unknown");
    }

    // ── sending ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_message_whose_template_was_switched_off_is_skipped_not_an_error()
    {
        _repo.Setup(r => r.GetByCodeAsync("ORDER_SHIPPED", It.IsAny<CancellationToken>())).ReturnsAsync((NotificationTemplate?)null);
        _repo.Setup(r => r.IsSwitchedOffAsync("ORDER_SHIPPED", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new SendNotificationCommandHandler(Mock.Of<INotificationRepository>(), _repo.Object, Mock.Of<IPreferenceRepository>(),
            Mock.Of<IEmailSender>(), Mock.Of<ISmsSender>(), Mock.Of<IPushSender>(), Mock.Of<IRealtimeNotifier>());

        var result = await handler.Handle(new SendNotificationCommand { RecipientId = "x", RecipientEmail = "a@b.c", Channel = NotificationChannel.Email, TemplateCode = "ORDER_SHIPPED" }, default);

        result.Status.Should().Be("Skipped");

        _repo.Setup(r => r.IsSwitchedOffAsync("NO_SUCH", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var unknown = () => handler.Handle(new SendNotificationCommand { RecipientId = "x", RecipientEmail = "a@b.c", Channel = NotificationChannel.Email, TemplateCode = "NO_SUCH" }, default);
        await unknown.Should().ThrowAsync<InvalidOperationException>();
    }
}
