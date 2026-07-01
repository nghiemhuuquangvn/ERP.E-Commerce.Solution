namespace ERP.Identity.Application.DTOs.Auth
{
    public record TwitterAuthorizeUrlResponse(string AuthorizeUrl, string State);
    public record TwitterCallbackRequest(string Code, string State);
}
