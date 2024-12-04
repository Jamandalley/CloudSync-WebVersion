namespace CloudSync.Services;

public interface IAuthenticationService
{
    Task<bool> IsUserAuthenticatedAsync(string email);
    Task<string> GetAuthorizationUrlAsync(string email);
    Task ExchangeCodeForTokenAsync(string code, string email);
}