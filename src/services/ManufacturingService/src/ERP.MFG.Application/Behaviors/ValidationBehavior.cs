using ERP.Shared.Infrastructure.Exceptions;
using FluentValidation;
using MediatR;

namespace ERP.MFG.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator of a request before its handler. MediatR does not do this by itself, and
/// a raw <see cref="ValidationException"/> would end as HTTP 500, so failures become a business-rule refusal (400):
/// the rule name is the validator's error code when it set one (an MFG_* code), else <c>MFG_VALIDATION</c>.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public const string DefaultRule = "MFG_VALIDATION";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();
            if (failures.Count > 0)
            {
                var coded = failures.FirstOrDefault(f => !string.IsNullOrEmpty(f.ErrorCode) && f.ErrorCode.StartsWith("MFG_", StringComparison.Ordinal));
                throw new BusinessRuleException(
                    coded?.ErrorCode ?? DefaultRule,
                    string.Join(" ", failures.Select(f => f.ErrorMessage).Distinct()));
            }
        }
        return await next();
    }
}
