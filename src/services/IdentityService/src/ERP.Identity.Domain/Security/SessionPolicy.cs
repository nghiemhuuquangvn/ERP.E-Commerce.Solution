using System;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Domain.Security
{
    /// <summary>
    /// How long a sign-in lasts (security review 2026-11, SEC-M3; docs/business/Identity/Sessions.md).
    /// A session ends after <see cref="IdleTimeout"/> without a refresh, and in any case <see cref="AbsoluteLifetime"/>
    /// after the sign-in that started it. Back-office users and customers have their own values, set per tenant
    /// (never above the platform ceiling). The refresh token's own expiry carries both limits, and the access token
    /// lives a third of the idle time, so an active client always refreshes inside the idle window.
    /// </summary>
    public sealed record SessionPolicy(TimeSpan IdleTimeout, TimeSpan AbsoluteLifetime)
    {
        public static readonly TimeSpan MinimumAccessLifetime = TimeSpan.FromMinutes(1);

        public static SessionPolicy For(bool isCustomer, TenantAuthSettings settings) => isCustomer
            ? new SessionPolicy(TimeSpan.FromMinutes(settings.CustomerSessionTimeoutMinutes), TimeSpan.FromDays(settings.CustomerSessionAbsoluteDays))
            : new SessionPolicy(TimeSpan.FromMinutes(settings.SessionTimeoutMinutes), TimeSpan.FromHours(settings.SessionAbsoluteHours));

        public static bool IsCustomer(User user) =>
            string.Equals(user.UserTypeCode, nameof(UserType.Customer), StringComparison.OrdinalIgnoreCase);

        /// <summary>When the session's absolute limit is reached.</summary>
        public DateTime EndsAt(DateTime sessionStartedAt) => sessionStartedAt + AbsoluteLifetime;

        /// <summary>Expiry of a refresh token issued now: the idle window, never past the session's end.</summary>
        public DateTime RefreshExpiresAt(DateTime now, DateTime sessionStartedAt)
        {
            var idleEnd = now + IdleTimeout;
            var end = EndsAt(sessionStartedAt);
            return idleEnd < end ? idleEnd : end;
        }

        /// <summary>
        /// Lifetime of an access token issued now: the configured maximum, a third of the idle time, and what is left
        /// of the session - whichever is shortest, but at least one minute.
        /// </summary>
        public TimeSpan AccessTokenLifetime(TimeSpan configuredMaximum, DateTime now, DateTime sessionStartedAt)
        {
            var lifetime = configuredMaximum;
            var third = TimeSpan.FromTicks(IdleTimeout.Ticks / 3);
            if (third < lifetime) lifetime = third;
            var remaining = EndsAt(sessionStartedAt) - now;
            if (remaining < lifetime) lifetime = remaining;
            return lifetime < MinimumAccessLifetime ? MinimumAccessLifetime : lifetime;
        }
    }
}
