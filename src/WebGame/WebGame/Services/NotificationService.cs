using Microsoft.AspNetCore.SignalR;
using WebGame.Hubs;

namespace WebGame.Services;

public class NotificationService(IHubContext<GameHub> hubContext)
{
    public async Task NotifyAllPlayers(string message)
    {
        await hubContext.Clients.All.SendAsync("ReceiveMessage", "Server", message);
    }

    public async Task SendMessageToPlayers(string username, string message)
    {
        await hubContext.Clients.All.SendAsync("ReceiveMessage", username, message);
    }
}