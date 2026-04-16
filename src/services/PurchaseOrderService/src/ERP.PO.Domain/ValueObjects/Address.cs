namespace ERP.PO.Domain.ValueObjects;

public record Address
{
    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street cannot be empty.", nameof(street));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City cannot be empty.", nameof(city));
        if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country cannot be empty.", nameof(country));

        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
