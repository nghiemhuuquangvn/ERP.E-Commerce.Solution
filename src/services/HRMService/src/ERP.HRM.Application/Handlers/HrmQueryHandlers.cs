using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Handlers;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// Department/Employee/Headcount query handlers moved to SharedMasterDataService.

// ─── Leave Request Query Handlers ──────────────────────────────────────────

public class GetLeaveRequestQueryHandler : IRequestHandler<GetLeaveRequestQuery, LeaveRequestDto?>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;

    public GetLeaveRequestQueryHandler(ILeaveRequestRepository leaveRequestRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
    }

    public async Task<LeaveRequestDto?> Handle(GetLeaveRequestQuery request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken);
        return leaveRequest is null ? null : SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class ListLeaveRequestsQueryHandler : IRequestHandler<ListLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;

    public ListLeaveRequestsQueryHandler(ILeaveRequestRepository leaveRequestRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
    }

    public async Task<PagedResult<LeaveRequestDto>> Handle(ListLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _leaveRequestRepository.ListAsync(
            request.EmployeeId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(SubmitLeaveRequestCommandHandler.MapToDto).ToList();
        return new PagedResult<LeaveRequestDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── PaySlip Query Handlers ────────────────────────────────────────────────

public class GetPaySlipQueryHandler : IRequestHandler<GetPaySlipQuery, PaySlipDto?>
{
    private readonly IPaySlipRepository _paySlipRepository;

    public GetPaySlipQueryHandler(IPaySlipRepository paySlipRepository)
    {
        _paySlipRepository = paySlipRepository;
    }

    public async Task<PaySlipDto?> Handle(GetPaySlipQuery request, CancellationToken cancellationToken)
    {
        var paySlip = await _paySlipRepository.GetByIdAsync(request.PaySlipId, cancellationToken);
        return paySlip is null ? null : GeneratePaySlipCommandHandler.MapToDto(paySlip);
    }
}

public class ListPaySlipsQueryHandler : IRequestHandler<ListPaySlipsQuery, PagedResult<PaySlipDto>>
{
    private readonly IPaySlipRepository _paySlipRepository;

    public ListPaySlipsQueryHandler(IPaySlipRepository paySlipRepository)
    {
        _paySlipRepository = paySlipRepository;
    }

    public async Task<PagedResult<PaySlipDto>> Handle(ListPaySlipsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _paySlipRepository.ListAsync(
            request.EmployeeId,
            request.Year,
            request.Month,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(GeneratePaySlipCommandHandler.MapToDto).ToList();
        return new PagedResult<PaySlipDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
