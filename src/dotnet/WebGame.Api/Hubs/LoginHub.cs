using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using WebGame.Domain.Common;
using WebGame.Hubs.Models;
using IAuthorizationService = WebGame.Domain.Interfaces.IAuthorizationService;

namespace WebGame.Hubs;

[SignalRHub]
public class LoginHub(IAuthorizationService authorizationService) : Hub
{
    [AllowAnonymous]
    public Result<LoginData> Login(string userName)
    {
        var loginData = authorizationService.GenerateJwtToken(userName);
        return new LoginData(loginData.PlayerId, userName, loginData.JwtToken);
    }

    [AllowAnonymous]
    public Result<bool> Validate(string accessToken)
    {
        return authorizationService.IsTokenValidForAtLeastOneDay(accessToken);
    }
}