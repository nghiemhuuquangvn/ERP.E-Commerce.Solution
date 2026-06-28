using ERP.NTF.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Infrastructure.Services;

public class NullPushSender : IPushSender
{
    private readonly ILogger<NullPushSender> _logger;
    public NullPushSender(ILogger<NullPushSender> logger) => _logger = logger;

    public Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV] Push to {Token}: {Title}", deviceToken, title);
        return Task.CompletedTask;
    }
}
