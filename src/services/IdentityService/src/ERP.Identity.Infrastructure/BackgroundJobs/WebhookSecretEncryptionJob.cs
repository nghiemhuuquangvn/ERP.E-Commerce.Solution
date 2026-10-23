using ERP.Identity.Infrastructure.Persistence;
using ERP.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Identity.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs once at startup: webhook secrets saved before encryption was introduced are still plaintext in
    /// <c>WebhookEndpoints.Secret</c>; this seals them (<see cref="SecretEnvelope"/>). It needs the application key, so it
    /// cannot be a SQL migration. Idempotent: sealed values are skipped, and a failure only means the next start tries again
    /// (a plaintext secret keeps working for signing in the meantime).
    /// </summary>
    public class WebhookSecretEncryptionJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WebhookSecretEncryptionJob> _logger;

        public WebhookSecretEncryptionJob(IServiceProvider serviceProvider, ILogger<WebhookSecretEncryptionJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var count = await EncryptLegacySecretsAsync(
                    scope.ServiceProvider.GetRequiredService<AuthDbContext>(),
                    scope.ServiceProvider.GetRequiredService<ISecretProtector>(),
                    stoppingToken);

                if (count > 0)
                    _logger.LogInformation("Encrypted {Count} legacy webhook secret(s)", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not encrypt legacy webhook secrets; plaintext ones stay usable until the next start");
            }
        }

        public static async Task<int> EncryptLegacySecretsAsync(AuthDbContext db, ISecretProtector protector, CancellationToken ct)
        {
            var legacy = await db.WebhookEndpoints
                .Where(w => w.Secret != null && w.Secret != "" && !w.Secret.StartsWith(SecretEnvelope.Prefix))
                .ToListAsync(ct);

            foreach (var endpoint in legacy)
                endpoint.SetStoredSecret(SecretEnvelope.Seal(protector, endpoint.Secret!));

            if (legacy.Count > 0)
                await db.SaveChangesAsync(ct);

            return legacy.Count;
        }
    }
}
