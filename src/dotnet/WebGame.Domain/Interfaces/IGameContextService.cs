namespace WebGame.Domain.Interfaces;

public interface IGameContextService
{
    Task AddToGroup(string playerConnectionId, string groupId);
    Task RemoveFromGroup(string playerConnectionId, string groupId);
    Task SendToGroup<T>(string groupName, string method, T data);
    Task SendToPlayer<T>(string playerConnectionId, string method, T data);
    Task NotifyGroup(string groupId, string message);
    Task SendChatMessageToGroup(string groupId, string playerName, string message);
}