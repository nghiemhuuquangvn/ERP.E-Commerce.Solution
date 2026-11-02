using ERP.Shared.Middleware;
using FluentAssertions;
using Xunit;

namespace ERP.Shared.UnitTests.Middleware;

/// <summary>R99: a refusal thrown by our own code as InvalidOperationException is a 400; the same type from the framework is a real fault (500).</summary>
public class ExceptionMappingTests
{
    private static Exception Thrown(Action throwing)
    {
        try { throwing(); }
        catch (Exception ex) { return ex; }
        throw new InvalidOperationException("nothing thrown");
    }

    [Fact]
    public void InvalidOperationException_thrown_by_our_code_is_a_business_refusal()
    {
        var ex = Thrown(() => throw new InvalidOperationException("Role 'X' already exists for this tenant"));

        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(ex).Should().BeTrue();
    }

    [Fact]
    public async Task It_stays_a_refusal_when_thrown_inside_an_async_method()
    {
        static async Task Refuse() { await Task.Yield(); throw new InvalidOperationException("Bill is not in Pending status."); }
        var ex = await Record.ExceptionAsync(Refuse);

        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(ex!).Should().BeTrue();
    }

    [Fact]
    public void ArgumentException_of_our_code_is_a_refusal_but_ArgumentNull_is_a_bug()
    {
        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(Thrown(() => throw new ArgumentException("Quantity must be positive"))).Should().BeTrue();
        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(Thrown(() => throw new ArgumentNullException("request"))).Should().BeFalse();
    }

    [Fact]
    public void The_same_exception_types_raised_by_the_framework_stay_server_errors()
    {
        var empty = Thrown(() => _ = new List<int>().First()); // System.Linq: "Sequence contains no elements"
        var disposed = Thrown(() => throw new ObjectDisposedException("x"));
        var parse = Thrown(() => _ = Enum.Parse<DayOfWeek>("Notaday")); // ArgumentException from the BCL

        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(empty).Should().BeFalse();
        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(disposed).Should().BeFalse();
        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(parse).Should().BeFalse();
    }

    [Fact]
    public void An_exception_that_was_never_thrown_has_no_origin_and_stays_a_server_error()
    {
        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(new InvalidOperationException("never thrown")).Should().BeFalse();
    }
}
