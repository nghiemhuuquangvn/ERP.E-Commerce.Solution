
using Microsoft.AspNetCore.Identity;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _passwordHasher = new();
    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
            return false;
        try
        {
            // Loại bỏ khoảng trắng thừa (nếu có do kiểu dữ liệu CHAR)
            var cleanedHash = hashedPassword.Trim();
            
            var result = _passwordHasher.VerifyHashedPassword(null!, cleanedHash, providedPassword);
            return result == PasswordVerificationResult.Success;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}