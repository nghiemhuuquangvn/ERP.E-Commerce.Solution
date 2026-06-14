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
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount.Amount))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.TotalAmount.Currency));

        CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice.Amount));

        CreateMap<Supplier, SupplierDto>()
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address));

        CreateMap<Address, AddressDto>();
    }
}
