using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebGame.Services;

namespace WebGame.Hubs;

[AllowAnonymous]
public class LoginHub(PlayerTracker playerTracker, AuthorizationService authorizationService) : Hub
{
    public string Login(string userName)
    {
        var jwtToken = authorizationService.GenerateJwtToken(userName);
        return jwtToken;
    }
}