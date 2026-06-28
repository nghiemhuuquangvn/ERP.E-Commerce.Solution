using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Infrastructure.Services;

public class NullSmsSender : ISmsSender
{
    private readonly ILogger<NullSmsSender> _logger;
    public NullSmsSender(ILogger<NullSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] SMS to {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
