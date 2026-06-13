using ERP.Identity.Domain.Common;

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

        public void Confirm()
        {
            if (IsExpired)
                throw new InvalidOperationException("Token has expired");
            
            ConfirmedAt = DateTime.UtcNow;
        }
    }
}
