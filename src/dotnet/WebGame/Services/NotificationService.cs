using Microsoft.AspNetCore.SignalR;
using WebGame.Constants;
using WebGame.Hubs;

namespace WebGame.Services;

public class NotificationService(IHubContext<GameHub> hubContext)
{
    public async Task NotifyGroup(string groupId, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, "Server", message);
    }

    public async Task SendChatMessageToGroup(string groupId, string playerName, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, playerName, message);
    }
}