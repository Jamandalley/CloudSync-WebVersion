using CloudSync.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;

namespace CloudSync2.Services.Implementation;
public class AuthenticationService : IAuthenticationService
{
    private readonly GoogleAuthorizationCodeFlow _flow;
    private readonly string _redirectUri;

    public AuthenticationService(IConfiguration configuration)
    {
        var clientSecrets = new ClientSecrets
        {
            ClientId = configuration["Google:ClientId"],
            ClientSecret = configuration["Google:ClientSecret"]
        };

        _flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = clientSecrets,
            Scopes = new[] { DriveService.Scope.DriveFile },
            DataStore = new FileDataStore("token_store")
        });

        _redirectUri = configuration["Google:RedirectUri"];
    }

    public async Task<bool> IsUserAuthenticatedAsync(string email)
    {
        var token = await _flow.LoadTokenAsync(email, CancellationToken.None);
        return token != null && !string.IsNullOrEmpty(token.AccessToken);
    }

    public async Task<string> GetAuthorizationUrlAsync(string email)
    {
    var authorizationUrl = new AuthorizationCodeRequestUrl(new Uri(_flow.AuthorizationServerUrl))
    {
        ClientId = _flow.ClientSecrets.ClientId,
        RedirectUri = _redirectUri,
        ResponseType = "code",
        Scope = string.Join(" ", _flow.Scopes),
        State = email
    };
    return authorizationUrl.Build().ToString();
}

    public async Task ExchangeCodeForTokenAsync(string code, string email)
    {
        var token = await _flow.ExchangeCodeForTokenAsync(email, code, _redirectUri, CancellationToken.None);
        await _flow.DataStore.StoreAsync<TokenResponse>(email, token);
    }
}