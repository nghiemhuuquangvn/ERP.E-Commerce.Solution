using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

/// <summary>
/// A user who may work in a plant (BR-GEN-09). Holders of <c>manufacturing:settings:manage</c> see every plant without a row;
/// everybody else sees only the plants they were granted.
/// </summary>
public class PlantUserAccess : BaseEntity
{
    private PlantUserAccess() { }

    public Guid PlantId { get; private set; }
    public Guid UserId { get; private set; }

    public static PlantUserAccess Grant(Guid plantId, Guid userId) => new() { Id = Guid.NewGuid(), PlantId = plantId, UserId = userId };
}
