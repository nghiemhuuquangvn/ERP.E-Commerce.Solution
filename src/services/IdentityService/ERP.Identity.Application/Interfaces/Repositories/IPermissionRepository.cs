using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IPermissionRepository
    {
        Task<IReadOnlyList<string>> GetByRoleIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct);
    }
}
