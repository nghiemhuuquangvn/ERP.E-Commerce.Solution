using ERP.SO.Application.DTOs;
using MediatR;

namespace ERP.SO.Application.Queries
{
    public class GetOrderQuery : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }

        public GetOrderQuery(Guid orderId)
        {
            OrderId = orderId;
        }
    }

    public class ListOrdersQuery : IRequest<ListOrdersResponse>
    {
        public Guid? CustomerId { get; set; }
        public string? Status { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }

    public class ListOrdersResponse
    {
        public int Total { get; set; }
        public List<SalesOrderSummaryDto> Items { get; set; } = new();
    }

    public class GetOrdersByCustomerQuery : IRequest<List<SalesOrderSummaryDto>>
    {
        public Guid CustomerId { get; set; }

        public GetOrdersByCustomerQuery(Guid customerId)
        {
            CustomerId = customerId;
        }
    }
}
