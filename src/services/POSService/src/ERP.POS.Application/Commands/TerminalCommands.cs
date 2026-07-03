using ERP.POS.Application.DTOs;
using MediatR;

namespace ERP.POS.Application.Commands;

public class CreateTerminalCommand : IRequest<TerminalDto>
{
    public string Name { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
}

public class SetTerminalActiveCommand : IRequest<TerminalDto>
{
    public Guid TerminalId { get; set; }
    public bool IsActive { get; set; }
}

public class RenameTerminalCommand : IRequest<TerminalDto>
{
    public Guid TerminalId { get; set; }
    public string Name { get; set; } = string.Empty;
}
