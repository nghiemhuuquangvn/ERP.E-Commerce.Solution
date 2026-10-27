using ERP.SHP.Infrastructure.EventBus;
using FluentAssertions;
using Xunit;

namespace ERP.SHP.UnitTests.EventBus;

public class SagaCreateShipmentCommandTests
{
    [Fact]
    public void Maps_the_order_address_and_lines_onto_the_shipment()
    {
        var productId = Guid.NewGuid();
        var message = new SagaCreateShipmentCommand
        {
            OrderId = Guid.NewGuid(),
            RecipientName = "Nguyen Van A",
            RecipientPhone = "0911111111",
            OrderTotal = 300000m,
            CodAmount = 300000m,
            ShippingAddress = new SagaShippingAddress
            {
                Street = "12 Le Loi", City = "Quan 1", State = "Ho Chi Minh", PostalCode = "700000", Country = "Vietnam"
            },
            Items = { new SagaShipmentLine { ProductId = productId, ProductName = "Ao thun", Quantity = 3, UnitPrice = 100000m } }
        };

        var command = message.ToCreateShipmentCommand();

        message.HasDeliveryAddress.Should().BeTrue();
        command.OrderId.Should().Be(message.OrderId);
        command.CarrierId.Should().BeNull("the carrier is chosen while booking");
        command.CodAmount.Should().Be(300000m);
        command.DeclaredValue.Should().Be(300000m);
        command.Recipient.RecipientPhone.Should().Be("0911111111");
        command.Recipient.RecipientName.Should().Be("Nguyen Van A");
        command.Recipient.AddressStreet.Should().Be("12 Le Loi");
        command.Recipient.AddressDistrict.Should().Be("Quan 1");
        command.Recipient.AddressProvince.Should().Be("Ho Chi Minh");
        command.Recipient.AddressPostalCode.Should().Be("700000");
        command.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 3 && i.UnitPrice == 100000m);
    }

    [Fact]
    public void A_command_without_an_address_is_not_deliverable()
    {
        new SagaCreateShipmentCommand { OrderId = Guid.NewGuid() }.HasDeliveryAddress.Should().BeFalse();
        new SagaCreateShipmentCommand { ShippingAddress = new SagaShippingAddress() }.HasDeliveryAddress.Should().BeFalse();
    }
}
