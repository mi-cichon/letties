using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebGame.Constants;
using WebGame.Extensions;
using WebGame.Services;

namespace WebGame.Hubs;

[Authorize]
public class GameHub(PlayerTracker playerTracker, NotificationService notificationService) : Hub
{
    public async Task<string> Join()
    {
        var username = Context.GetUsername();
        await playerTracker.LoginPlayer(username, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ServerGroups.GameTable);
        
        return Context.ConnectionId;
    }
    
    public async Task SendMessage(string user, string message)
    {
        var nickname = playerTracker.GetPlayerNickname(Context.ConnectionId);
        await notificationService.SendMessageToPlayers(nickname, message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await playerTracker.PlayerDisconnected(Context.ConnectionId);
    }
}