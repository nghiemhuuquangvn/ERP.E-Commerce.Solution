using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ERP.Identity.Infrastructure.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;

        public PasswordPolicyService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        public async Task ValidateNewPasswordAsync(Guid userId, string newPlaintextPassword, CancellationToken ct)
        {
            var minLength = GetConfigInt("PasswordPolicy:MinLength", 8);
            if (string.IsNullOrWhiteSpace(newPlaintextPassword) || newPlaintextPassword.Length < minLength)
                throw new BusinessRuleException("PasswordTooShort", $"Password must be at least {minLength} characters long.");

            var historyCount = GetConfigInt("PasswordPolicy:HistoryCount", 5);
            if (historyCount <= 0)
                return;

            var recent = await _unitOfWork.PasswordHistories.GetRecentAsync(userId, historyCount, ct);
            if (recent.Any(h => _passwordHasher.VerifyPassword(h.PasswordHash, newPlaintextPassword)))
                throw new BusinessRuleException("PasswordRecentlyUsed", $"Password must not match any of your last {historyCount} passwords.");
        }

        public async Task RecordPasswordHistoryAsync(Guid userId, string previousPasswordHash, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(previousPasswordHash))
                return;

            var entry = PasswordHistoryEntry.Create(userId, previousPasswordHash);
            await _unitOfWork.PasswordHistories.AddAsync(entry, ct);

            var historyCount = GetConfigInt("PasswordPolicy:HistoryCount", 5);
            var all = await _unitOfWork.PasswordHistories.GetRecentAsync(userId, historyCount + 50, ct);
            foreach (var stale in all.Skip(historyCount))
                await _unitOfWork.PasswordHistories.DeleteAsync(stale, ct);
        }

        private int GetConfigInt(string key, int fallback)
        {
            var raw = _configuration[key];
            return int.TryParse(raw, out var value) ? value : fallback;
        }
    }
}
