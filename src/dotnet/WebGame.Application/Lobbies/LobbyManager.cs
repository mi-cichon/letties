using System.Collections.Concurrent;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Models;

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

    public async Task UpdateLobbySettings(string playerConnectionId, Guid playerId, LobbySettingsModel settingsModel)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            await gameLobby.UpdateLobbySettings(playerId, settingsModel);
        }
    }

    public GameDetails GetGameDetails(string playerConnectionId, Guid playerId)
    {
        return _playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby) 
            ? gameLobby.GetGameDetails(playerId)
            : throw new InvalidOperationException("Player is not in a lobby.");
    }

    public async Task StartGame(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            await gameLobby.StartGame(playerId);
        }
    }

    public MoveResult HandleMove(string playerConnectionId, Guid playerId, MoveRequestModel request)
    {
        return _playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby) 
            ? gameLobby.HandleMove(playerId, request)
            : throw new InvalidOperationException("Player is not in a lobby.");
    }

    public void HandleSwapTile(string playerConnectionId, Guid playerId, List<Guid> tileIdsToSwap)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            gameLobby.HandleSwapTiles(playerId, tileIdsToSwap);
        }
    }
    
    public void HandleSkipTurn(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            gameLobby.HandleSkipTurn(playerId);
        }
    }

    private IGameLobby? GetLobbyById(Guid lobbyId)
    {
        return gameLobbies.FirstOrDefault(lobby => lobby.LobbyId == lobbyId);
    }
}