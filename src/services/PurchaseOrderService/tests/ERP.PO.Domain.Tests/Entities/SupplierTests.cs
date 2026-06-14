using Xunit;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Domain.Tests.Entities;

public class SupplierTests
{
    [Fact]
    public void Create_ValidParameters_CreatesSupplierInActiveStatus()
    {
        var code = "SUP-001";
        var name = "Supplier Company";

        var supplier = Supplier.Create(code, name);

        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.Equal(code, supplier.Code);
        Assert.Equal(name, supplier.Name);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public void Create_WithAllParameters_CreatesSupplierWithAllData()
    {
        var code = "SUP-002";
        var name = "Supplier Company";
        var contactPerson = "John Doe";
        var email = "john@supplier.com";
        var phone = "123-456-7890";
        var address = new Address("123 Main St", "New York", "NY", "10001", "USA");

        var supplier = Supplier.Create(code, name, contactPerson, email, phone, address);

        Assert.Equal(code, supplier.Code);
        Assert.Equal(name, supplier.Name);
        Assert.Equal(contactPerson, supplier.ContactPerson);
        Assert.Equal(email, supplier.Email);
        Assert.Equal(phone, supplier.Phone);
        Assert.NotNull(supplier.Address);
    }

    [Fact]
    public void Update_ValidParameters_UpdatesSupplierData()
    {
        var supplier = Supplier.Create("SUP-003", "Old Name");
        var newName = "New Name";
        var newContact = "Jane Smith";
        var newEmail = "jane@supplier.com";
        var newPhone = "987-654-3210";
        var newAddress = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "USA");

        supplier.Update(newName, newContact, newEmail, newPhone, newAddress);

        Assert.Equal(newName, supplier.Name);
        Assert.Equal(newContact, supplier.ContactPerson);
        Assert.Equal(newEmail, supplier.Email);
        Assert.Equal(newPhone, supplier.Phone);
        Assert.NotNull(supplier.Address);
    }

    [Fact]
    public void Deactivate_ActiveSupplier_ChangesIsActiveToFalse()
    {
        var supplier = Supplier.Create("SUP-004", "Active Supplier");

        supplier.Deactivate();

        Assert.False(supplier.IsActive);
    }

    [Fact]
    public void Activate_InactiveSupplier_ChangesIsActiveToTrue()
    {
        var supplier = Supplier.Create("SUP-005", "Inactive Supplier");
        supplier.Deactivate();

        supplier.Activate();

        Assert.True(supplier.IsActive);
    }

    [Fact]
    public void Update_UpdatesTimestamp()
    {
        var supplier = Supplier.Create("SUP-006", "Supplier");
        var originalUpdatedAt = supplier.UpdatedAt;

        System.Threading.Thread.Sleep(100);
        supplier.Update("Updated Name", null, null, null, null);

        Assert.True(supplier.UpdatedAt > originalUpdatedAt);
    }
}
