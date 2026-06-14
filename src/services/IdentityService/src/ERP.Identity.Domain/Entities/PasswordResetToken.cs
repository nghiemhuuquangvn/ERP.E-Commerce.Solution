using ERP.Identity.Domain.Common;

namespace ERP.Identity.Domain.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public Guid UserId { get; private set; }
        public string Token { get; private set; } = default!;
        public DateTime ExpiredAt { get; private set; }
        public DateTime? UsedAt { get; private set; }

        public bool IsUsed => UsedAt.HasValue;
        public bool IsExpired => DateTime.UtcNow > ExpiredAt;
        public bool IsValid => !IsUsed && !IsExpired;

        public PasswordResetToken() { }

        public static PasswordResetToken Create(Guid userId, string token, DateTime expiredAt)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID is required");
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required");
            if (expiredAt <= DateTime.UtcNow)
                throw new ArgumentException("Expiration time must be in the future");

            return new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                ExpiredAt = expiredAt,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
        }

        public void MarkUsed()
        {
            if (IsUsed)
                throw new InvalidOperationException("Token has already been used");
            if (IsExpired)
                throw new InvalidOperationException("Token has expired");

            UsedAt = DateTime.UtcNow;
        }
    }
}
