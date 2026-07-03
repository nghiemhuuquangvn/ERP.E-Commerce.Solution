using ERP.POS.Application.Commands;
using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using ERP.POS.Domain.Entities;
using MediatR;

namespace ERP.POS.Application.Handlers;

public class CreateTerminalCommandHandler : IRequestHandler<CreateTerminalCommand, TerminalDto>
{
    private readonly ITerminalRepository _terminals;
    public CreateTerminalCommandHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<TerminalDto> Handle(CreateTerminalCommand request, CancellationToken cancellationToken)
    {
        var terminal = Terminal.Create(request.Name, request.LocationId);
        await _terminals.AddAsync(terminal, cancellationToken);
        return MapToDto(terminal);
    }

    internal static TerminalDto MapToDto(Terminal t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        LocationId = t.LocationId,
        IsActive = t.IsActive,
        LastSyncAt = t.LastSyncAt,
        CreatedAt = t.CreatedAt
    };
}

public class SetTerminalActiveCommandHandler : IRequestHandler<SetTerminalActiveCommand, TerminalDto>
{
    private readonly ITerminalRepository _terminals;
    public SetTerminalActiveCommandHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<TerminalDto> Handle(SetTerminalActiveCommand request, CancellationToken cancellationToken)
    {
        var terminal = await _terminals.GetByIdAsync(request.TerminalId, cancellationToken)
            ?? throw new InvalidOperationException($"Terminal {request.TerminalId} not found.");

        if (request.IsActive) terminal.Activate(); else terminal.Deactivate();
        await _terminals.UpdateAsync(terminal, cancellationToken);
        return CreateTerminalCommandHandler.MapToDto(terminal);
    }
}

public class RenameTerminalCommandHandler : IRequestHandler<RenameTerminalCommand, TerminalDto>
{
    private readonly ITerminalRepository _terminals;
    public RenameTerminalCommandHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<TerminalDto> Handle(RenameTerminalCommand request, CancellationToken cancellationToken)
    {
        var terminal = await _terminals.GetByIdAsync(request.TerminalId, cancellationToken)
            ?? throw new InvalidOperationException($"Terminal {request.TerminalId} not found.");

        terminal.Rename(request.Name);
        await _terminals.UpdateAsync(terminal, cancellationToken);
        return CreateTerminalCommandHandler.MapToDto(terminal);
    }
}

public class ListTerminalsQueryHandler : IRequestHandler<ListTerminalsQuery, List<TerminalDto>>
{
    private readonly ITerminalRepository _terminals;
    public ListTerminalsQueryHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<List<TerminalDto>> Handle(ListTerminalsQuery request, CancellationToken cancellationToken)
    {
        var terminals = await _terminals.GetAllAsync(request.IsActive, cancellationToken);
        return terminals.Select(CreateTerminalCommandHandler.MapToDto).ToList();
    }
}
