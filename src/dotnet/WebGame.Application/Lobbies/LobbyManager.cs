using System.Collections.Concurrent;
using WebGame.Domain.Interfaces.Lobbies;

namespace WebGame.Application.Lobbies;

public class LobbyManager(IEnumerable<IGameLobby> gameLobbies) : ILobbyManager
{
    private readonly ConcurrentDictionary<string, IGameLobby> _playerAssignedLobbies = new();
    
    public IReadOnlyList<GameLobbyItem> GetLobbies()
    {
        return gameLobbies
            .Select(lobby => new GameLobbyItem(lobby.LobbyId, lobby.State, lobby.PlayerCount))
            .ToList();
    }
    
    public async Task<LobbyStateDetails> AssignPlayerToLobby(Guid playerId, Guid lobbyId, string playerConnectionId, string playerName)
    {
        var gameLobby = GetLobbyById(lobbyId);
        if (gameLobby == null)
        {
            throw new InvalidOperationException($"Lobby with id {lobbyId} not found.");
        }
        
        var result = await gameLobby.AssignPlayer(playerId, playerConnectionId, playerName);
        _playerAssignedLobbies.TryAdd(playerConnectionId, gameLobby);
        return result;
    }

    public async Task PlayerDisconnected(string playerConnectionId)
    {
        if (_playerAssignedLobbies.TryRemove(playerConnectionId, out var gameLobby))
        {
            await gameLobby.PlayerDisconnected(playerConnectionId);
        }
    }

    public async Task LeaveLobby(Guid playerId)
    {
        var playerLobby = gameLobbies.SingleOrDefault(x => x.IsPlayerInLobby(playerId));

        if (playerLobby == null)
        {
            return;
        }

        await playerLobby.LeaveLobby(playerId);
    }

    public async Task SendMessage(string playerConnectionId, string playerName, string message)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            await gameLobby.SendMessage(playerName, message);
        }
    }

    public async Task<bool> JoinSeat(string playerConnectionId, Guid playerId, Guid seatId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.JoinSeat(playerId, seatId);
        }

        return false;
    }
    
    public async Task LeaveSeat(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            await gameLobby.LeaveSeat(playerId);
        }
    }
    
    private IGameLobby? GetLobbyById(Guid lobbyId)
    {
        return gameLobbies.FirstOrDefault(lobby => lobby.LobbyId == lobbyId);
    }
}