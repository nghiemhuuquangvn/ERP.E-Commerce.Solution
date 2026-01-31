using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IRoleRepository
    {
        Task<IReadOnlyList<Role>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    }
}
