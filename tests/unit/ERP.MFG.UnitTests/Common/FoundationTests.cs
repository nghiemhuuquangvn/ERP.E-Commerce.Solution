using System.Text.RegularExpressions;
using ERP.MFG.Application.Behaviors;
using ERP.MFG.Domain.Common;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace ERP.MFG.UnitTests.Common;

public class FoundationTests
{
    // ── TC-GEN-009: refusals carry the MFG_* code and the Vietnamese message of the BA spec ────────────────

    [Fact]
    public void TC_GEN_009_Refusal_CarriesCodeAndVietnameseMessage()
    {
        var ex = MfgRules.Fail(MfgErrors.Mat003, ("available", 12.5m));

        ex.Should().BeOfType<BusinessRuleException>();
        ex.RuleName.Should().Be("MFG_MAT_003");
        ex.Message.Should().Be("Tồn kho khả dụng không đủ (còn 12.5).");
    }

    [Fact]
    public void Every_Code_Constant_Has_A_Message_And_Every_Message_Has_A_Constant()
    {
        var constants = typeof(MfgErrors).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        constants.Should().OnlyHaveUniqueItems();
        MfgErrors.Messages.Keys.Should().BeEquivalentTo(constants);
        constants.Should().OnlyContain(c => Regex.IsMatch(c, @"^MFG_[A-Z]+_\d{3}$"));
    }

    [Fact]
    public void Placeholders_Left_Unfilled_Stay_Visible_Not_Silently_Dropped()
    {
        MfgErrors.Format(MfgErrors.Gen006).Should().Contain("{code}");
        MfgErrors.Format(MfgErrors.Gen006, ("code", "WC-CUT")).Should().Be("Mã WC-CUT đã tồn tại.");
    }

    [Fact]
    public void ThrowIf_Only_Throws_When_Violated()
    {
        var ok = () => MfgRules.ThrowIf(false, MfgErrors.Po001);
        var bad = () => MfgRules.ThrowIf(true, MfgErrors.Po001);

        ok.Should().NotThrow();
        bad.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_PO_001");
    }

    // ── TC-GEN-007: document number format ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MO", 2026, 11, 1, "MO-2611-00001")]
    [InlineData("MI", 2027, 1, 42, "MI-2701-00042")]
    [InlineData("NCR", 2026, 12, 123456, "NCR-2612-123456")]
    public void TC_GEN_007_DocumentNumber_Format(string type, int year, int month, long seq, string expected)
        => DocumentTypes.Format(type, new DateTime(year, month, 15), seq).Should().Be(expected);

    // ── Validation pipeline: failures are a 400 refusal, never an unhandled ValidationException ───────────

    private sealed record Ping(int Qty) : IRequest<string>;

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Qty).GreaterThan(0).WithErrorCode(MfgErrors.Po001).WithMessage(MfgErrors.Format(MfgErrors.Po001));
    }

    private sealed class PlainValidator : AbstractValidator<Ping>
    {
        public PlainValidator() => RuleFor(x => x.Qty).LessThan(100).WithMessage("Too big.");
    }

    [Fact]
    public async Task Validation_Failure_Becomes_BusinessRule_With_The_Validators_MFG_Code()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator()]);

        var act = () => behavior.Handle(new Ping(0), () => Task.FromResult("handled"), default);

        var ex = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        ex.RuleName.Should().Be("MFG_PO_001");
        ex.Message.Should().Be("Số lượng sản xuất phải lớn hơn 0.");
    }

    [Fact]
    public async Task Validation_Failure_Without_A_Code_Uses_The_Default_Rule()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PlainValidator()]);

        var act = () => behavior.Handle(new Ping(500), () => Task.FromResult("handled"), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(ValidationBehavior<Ping, string>.DefaultRule);
    }

    [Fact]
    public async Task Valid_Request_Reaches_The_Handler()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator(), new PlainValidator()]);

        (await behavior.Handle(new Ping(5), () => Task.FromResult("handled"), default)).Should().Be("handled");
    }

    [Fact]
    public async Task No_Validators_Means_The_Handler_Runs()
    {
        var behavior = new ValidationBehavior<Ping, string>([]);

        (await behavior.Handle(new Ping(-1), () => Task.FromResult("handled"), default)).Should().Be("handled");
    }
}
