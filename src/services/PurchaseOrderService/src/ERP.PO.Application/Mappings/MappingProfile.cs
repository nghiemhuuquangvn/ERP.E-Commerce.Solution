using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<PurchaseOrder, PurchaseOrderDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => ToUpperSnakeCase(src.Status.ToString())))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount.Amount))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.TotalAmount.Currency));

        CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => ToUpperSnakeCase(src.Status.ToString())))
            .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice.Amount))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice.Amount));

        CreateMap<Supplier, SupplierDto>()
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address));

        CreateMap<Address, AddressDto>();

        CreateMap<SupplierItem, SupplierItemDto>();
    }

    /// <summary>
    /// The Admin frontend's PurchaseOrderStatus type/comparisons (e.g.
    /// `po.status === 'SUBMITTED'`) have always expected UPPER_SNAKE_CASE
    /// status strings, but this mapping only ever emitted the raw PascalCase
    /// enum name (e.g. "Submitted") — so status-gated UI (the Approve button,
    /// status filter) could never have matched. "PartiallyReceived" ->
    /// "PARTIALLY_RECEIVED", "PendingFinanceApproval" -> "PENDING_FINANCE_APPROVAL".
    /// </summary>
    private static string ToUpperSnakeCase(string pascalCase)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < pascalCase.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascalCase[i]))
                sb.Append('_');
            sb.Append(pascalCase[i]);
        }
        return sb.ToString().ToUpperInvariant();
    }
}
