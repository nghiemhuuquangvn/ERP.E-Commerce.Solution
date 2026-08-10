using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;

namespace ERP.CHB.Infrastructure.Tools;

public class ErpToolRegistry : IErpToolRegistry
{
    private readonly IReadOnlyList<IErpTool> _tools;

    public ErpToolRegistry(IEnumerable<IErpTool> tools)
    {
        _tools = tools.ToList();
    }

    public IReadOnlyList<IErpTool> GetAvailableTools(UserContext? caller, ConversationChannel channel, bool isGuest)
    {
        return _tools.Where(tool =>
        {
            if (!tool.Channels.Contains(channel))
                return false;

            if (isGuest)
                return tool.AllowGuest;

            if (tool.RequiredPermission is not null && (caller is null || !caller.HasPermission(tool.RequiredPermission)))
                return false;

            return true;
        }).ToList();
    }

    public IErpTool? Find(string name) => _tools.FirstOrDefault(t => t.Definition.Name == name);
}
