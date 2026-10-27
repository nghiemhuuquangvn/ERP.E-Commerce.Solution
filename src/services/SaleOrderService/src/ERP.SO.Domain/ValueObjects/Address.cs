namespace ERP.SO.Domain.ValueObjects
{
    public class Address
    {
        public string Street { get; set; } = string.Empty;
        /// <summary>Ward (phuong / xa): GHTK and Viettel Post refuse an address without it. Empty on orders made before P1-11b.</summary>
        public string Ward { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "US";

        public Address()
        {
        }

        public Address(string street, string city, string state, string postalCode, string country = "US", string? ward = null)
        {
            Street = street;
            Ward = ward?.Trim() ?? string.Empty;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }

        public override string ToString() => $"{Street}, {City}, {State} {PostalCode}";
    }
}
