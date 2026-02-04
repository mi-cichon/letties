using Microsoft.AspNetCore.SignalR;
using WebGame.Application.Constants;
using WebGame.Domain.Common;
using WebGame.Domain.Interfaces;

namespace WebGame.Hubs;

public class GameContextService(IHubContext<GameHub> hubContext)  : IGameContextService
{
    public async Task<Result> AddToGroup(string playerConnectionId, string groupId)
    {  
        await hubContext.Groups.AddToGroupAsync(playerConnectionId, groupId);
        return Result.Success();
    }
    
    public async Task<Result> RemoveFromGroup(string playerConnectionId, string groupId)
    {
        await hubContext.Groups.RemoveFromGroupAsync(playerConnectionId, groupId);
        return Result.Success();
    }
    
    public async Task<Result> SendToGroup<T>(string groupName, string method, T data)
    {
        await hubContext.Clients.Group(groupName).SendAsync(method, data);
        return Result.Success();
    }
    
    public async Task<Result> SendToPlayer<T>(string playerConnectionId, string method, T data)
    {
        await hubContext.Clients.Client(playerConnectionId).SendAsync(method, data);
        return Result.Success();
    }
    
    public async Task<Result> NotifyGroup(string groupId, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, "Server", message);
        return Result.Success();
    }

    public async Task<Result> SendChatMessageToGroup(string groupId, string playerName, string message)
    {
        await hubContext.Clients.Group(groupId).SendAsync(GameMethods.ReceiveMessage, playerName, message);
        return Result.Success();
    }
}