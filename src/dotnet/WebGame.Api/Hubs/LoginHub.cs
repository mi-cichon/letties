using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using WebGame.Hubs.Models;
using IAuthorizationService = WebGame.Domain.Interfaces.IAuthorizationService;

namespace WebGame.Hubs;

[SignalRHub]
public class LoginHub(IAuthorizationService authorizationService) : Hub
{
    [AllowAnonymous]
    public LoginData Login(string userName)
    {
        var loginData = authorizationService.GenerateJwtToken(userName);
        return new LoginData(loginData.PlayerId, userName, loginData.JwtToken);
    }

    [AllowAnonymous]
    public bool Validate(string accessToken)
    {
        return authorizationService.IsTokenValidForAtLeastOneDay(accessToken);
    }
}