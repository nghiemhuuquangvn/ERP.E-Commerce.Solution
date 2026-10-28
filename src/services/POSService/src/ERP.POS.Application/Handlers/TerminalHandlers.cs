using ERP.POS.Application.Commands;
using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using ERP.POS.Domain.Entities;
using MediatR;

namespace ERP.POS.Application.Handlers;

public class CreateTerminalCommandHandler : IRequestHandler<CreateTerminalCommand, TerminalDto>
{
    private readonly IPosUnitOfWork _uow;
    public CreateTerminalCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TerminalDto> Handle(CreateTerminalCommand request, CancellationToken cancellationToken)
    {
        var terminal = Terminal.Create(request.Name, request.LocationId);
        await _uow.Terminals.AddAsync(terminal, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
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
    private readonly IPosUnitOfWork _uow;
    public SetTerminalActiveCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TerminalDto> Handle(SetTerminalActiveCommand request, CancellationToken cancellationToken)
    {
        var terminal = await _uow.Terminals.GetByIdAsync(request.TerminalId, cancellationToken)
            ?? throw new InvalidOperationException($"Terminal {request.TerminalId} not found.");

        if (request.IsActive) terminal.Activate(); else terminal.Deactivate();
        await _uow.Terminals.UpdateAsync(terminal, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateTerminalCommandHandler.MapToDto(terminal);
    }
}

public class RenameTerminalCommandHandler : IRequestHandler<RenameTerminalCommand, TerminalDto>
{
    private readonly IPosUnitOfWork _uow;
    public RenameTerminalCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TerminalDto> Handle(RenameTerminalCommand request, CancellationToken cancellationToken)
    {
        var terminal = await _uow.Terminals.GetByIdAsync(request.TerminalId, cancellationToken)
            ?? throw new InvalidOperationException($"Terminal {request.TerminalId} not found.");

        terminal.Rename(request.Name);
        await _uow.Terminals.UpdateAsync(terminal, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreateTerminalCommandHandler.MapToDto(terminal);
    }
}

public class GetTerminalQueryHandler : IRequestHandler<GetTerminalQuery, TerminalDto?>
{
    private readonly ITerminalRepository _terminals;
    public GetTerminalQueryHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<TerminalDto?> Handle(GetTerminalQuery request, CancellationToken cancellationToken)
    {
        var terminal = await _terminals.GetByIdAsync(request.TerminalId, cancellationToken);
        return terminal == null ? null : CreateTerminalCommandHandler.MapToDto(terminal);
    }
}

public class ListTerminalsQueryHandler: IRequestHandler<ListTerminalsQuery, List<TerminalDto>>
{
    private readonly ITerminalRepository _terminals;
    public ListTerminalsQueryHandler(ITerminalRepository terminals) => _terminals = terminals;

    public async Task<List<TerminalDto>> Handle(ListTerminalsQuery request, CancellationToken cancellationToken)
    {
        var terminals = await _terminals.GetAllAsync(request.IsActive, cancellationToken);
        return terminals.Select(CreateTerminalCommandHandler.MapToDto).ToList();
    }
}
