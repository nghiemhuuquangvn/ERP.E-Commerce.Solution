using ERP.Identity.Domain.Common;
using ERP.Identity.Domain.Events;

namespace ERP.Identity.Domain.Entities
{
    public class EmailConfirmToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiredAt;
        public bool IsConfirmed => ConfirmedAt.HasValue;

        public EmailConfirmToken()
        {
        }

        public static EmailConfirmToken Create(Guid userId, string token, DateTime expiredAt)
        {
            return new EmailConfirmToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                ExpiredAt = expiredAt,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Queues delivery of the confirmation link (built by the caller from the plaintext
        /// token — <see cref="Token"/> holds only its hash) via the Outbox.
        /// </summary>
        public void RaiseRequested(Guid tenantId, string email, string confirmLink, int expiryHours)
        {
            AddDomainEvent(new EmailConfirmationRequestedDomainEvent(UserId, tenantId, email, confirmLink, expiryHours));
        }

        public void Confirm()
        {
            if (IsConfirmed)
                throw new InvalidOperationException("Token has already been used");
            if (IsExpired)
                throw new InvalidOperationException("Token has expired");

            ConfirmedAt = DateTime.UtcNow;
        }
    }
}
