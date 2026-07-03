using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// One trusted-device row per (UserId, DeviceId) — maps to the DeviceSessions
    /// table from 001_CreateAuthDB.sql. Upserted on every successful login so a user
    /// can see/revoke "where am I logged in" the same way most consumer apps do.
    /// </summary>
    public class DeviceSession
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid TenantId { get; private set; }
        public string DeviceId { get; private set; } = default!;
        public string? DeviceName { get; private set; }
        public string? DeviceType { get; private set; }
        public string? Platform { get; private set; }
        public string? IpAddress { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime LastActiveAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private DeviceSession() { }

        public static DeviceSession Create(
            Guid userId, Guid tenantId, string deviceId,
            string? deviceName, string? deviceType, string? platform, string? ipAddress)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required", nameof(userId));
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceId is required", nameof(deviceId));

            var now = DateTime.UtcNow;
            return new DeviceSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                DeviceId = deviceId,
                DeviceName = deviceName,
                DeviceType = deviceType,
                Platform = platform,
                IpAddress = ipAddress,
                IsActive = true,
                LastActiveAt = now,
                CreatedAt = now
            };
        }

        public void Touch(string? ipAddress)
        {
            LastActiveAt = DateTime.UtcNow;
            IsActive = true;
            if (!string.IsNullOrWhiteSpace(ipAddress))
                IpAddress = ipAddress;
        }

        public void Revoke()
        {
            IsActive = false;
        }
    }
}
