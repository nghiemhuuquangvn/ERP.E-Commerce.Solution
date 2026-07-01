namespace ERP.Identity.Application.Interfaces.Services
{
    public interface ITwoFactorCodeGenerator
    {
        string GenerateCode(int length);
    }
}
