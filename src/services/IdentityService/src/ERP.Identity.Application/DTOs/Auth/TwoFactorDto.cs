using System;

namespace ERP.Identity.Application.DTOs.Auth
{
    public record VerifyTwoFactorRequest(Guid ChallengeId, string Code);
    public record ResendTwoFactorRequest(Guid ChallengeId);
}
