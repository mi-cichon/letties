using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebGame.Services;

namespace WebGame.Hubs;

public class LoginHub(AuthorizationService authorizationService) : Hub
{
    [AllowAnonymous]
    public string Login(string userName)
    {
        var jwtToken = authorizationService.GenerateJwtToken(userName);
        return jwtToken;
    }

    [AllowAnonymous]
    public bool Validate(string accessToken)
    {
        return authorizationService.IsTokenValidForAtLeastOneDay(accessToken);
    }
}