using System.Collections.Concurrent;

namespace WebGame.Services;

public class PlayerTracker(NotificationService notificationService)
{
    private readonly ConcurrentDictionary<string, string> _players = new();
    
    public async Task LoginPlayer(string user, string connectionId)
    {
        _players.TryAdd(connectionId, user);
        await notificationService.NotifyAllPlayers($"{user} has joined the game.");
    }

    public string GetPlayerNickname(string connectionId)
    {
        return _players[connectionId];
    }
    
    public async Task PlayerDisconnected(string connectionId)
    {
        if(_players.TryRemove(connectionId, out var nickname))
        {
            await notificationService.NotifyAllPlayers($"{nickname} has left the game.");
        }
    }
}