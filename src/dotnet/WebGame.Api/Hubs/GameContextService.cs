using Microsoft.AspNetCore.SignalR;
using WebGame.Application.Constants;
using WebGame.Domain.Interfaces;

namespace WebGame.Hubs;

public class GameContextService(IHubContext<GameHub> hubContext)  : IGameContextService
{
    public async Task AddToGroup(string playerConnectionId, string groupId)
    {  
        await hubContext.Groups.AddToGroupAsync(playerConnectionId, groupId);
    }
    
    public async Task RemoveFromGroup(string playerConnectionId, string groupId)
    {
        await hubContext.Groups.RemoveFromGroupAsync(playerConnectionId, groupId);
    }
    
    public async Task SendToGroup<T>(string groupName, string method, T data)
    {
        await hubContext.Clients.Group(groupName).SendAsync(method, data);
    }
    
    public async Task SendToPlayer<T>(string playerConnectionId, string method, T data)
    {
        await hubContext.Clients.Client(playerConnectionId).SendAsync(method, data);
    }
    
    public async Task NotifyGroup(string groupId, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, "Server", message);
    }

    public async Task SendChatMessageToGroup(string groupId, string playerName, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, playerName, message);
    }
}