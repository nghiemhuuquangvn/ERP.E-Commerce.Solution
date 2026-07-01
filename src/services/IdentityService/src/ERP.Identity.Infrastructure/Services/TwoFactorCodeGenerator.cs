using ERP.Identity.Application.Interfaces.Services;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Identity.Infrastructure.Services
{
    public class TwoFactorCodeGenerator : ITwoFactorCodeGenerator
    {
        public string GenerateCode(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                var digit = RandomNumberGenerator.GetInt32(0, 10);
                sb.Append(digit);
            }

            return sb.ToString();
        }
    }
}
