using WebGame.Domain.Common;

namespace WebGame.Domain.Interfaces;

public interface IGameContextService
{
    Task<Result> AddToGroup(string playerConnectionId, string groupId);
    Task<Result> RemoveFromGroup(string playerConnectionId, string groupId);
    Task<Result> SendToGroup<T>(string groupName, string method, T data);
    Task<Result> SendToPlayer<T>(string playerConnectionId, string method, T data);
    Task<Result> NotifyGroup(string groupId, string message);
    Task<Result> SendChatMessageToGroup(string groupId, string playerName, string message);
}