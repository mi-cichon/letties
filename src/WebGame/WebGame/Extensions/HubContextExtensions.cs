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
}