using System;

namespace ERP.Identity.Application.DTOs.Auth
{
    public record VerifyTwoFactorRequest(
        Guid ChallengeId, string Code,
        string? DeviceId = null, string? DeviceName = null, string? DeviceType = null, string? Platform = null);
    public record ResendTwoFactorRequest(Guid ChallengeId);
}
