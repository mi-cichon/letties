using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace WebGame.Extensions;

public static class HubContextExtensions
{
    public static string GetUsername(this HubCallerContext context)
    {
        var username = context.User?.Identity?.Name;

        if (username == null)
        {
            throw new InvalidOperationException("The name identity was not found.");
        }
        
        return username;
    }

    public static Guid GetPlayerId(this HubCallerContext context)
    {
        var playerId = context.User?.Claims.SingleOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

        if (playerId == null)
        {
            throw new InvalidOperationException("The player id claim was not found.");
        }
        
        return Guid.Parse(playerId);
    }
}