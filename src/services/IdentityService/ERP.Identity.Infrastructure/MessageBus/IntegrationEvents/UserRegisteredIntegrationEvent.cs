namespace ERP.Identity.Infrastructure.MessageBus.IntegrationEvents
{
    public class UserRegisteredIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string Email { get; }
        public string UserType { get; }
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public UserRegisteredIntegrationEvent(Guid userId, Guid tenantId, string email, string userType = "Customer")
        {
            UserId = userId;
            TenantId = tenantId;
            Email = email;
            UserType = userType;
        }
    }
}