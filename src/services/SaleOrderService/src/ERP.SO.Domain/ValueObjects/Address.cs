namespace ERP.SO.Domain.ValueObjects
{
    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "US";

        public Address()
        {
        }

        public Address(string street, string city, string state, string postalCode, string country = "US")
        {
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }

        public override string ToString() => $"{Street}, {City}, {State} {PostalCode}";
    }
}
