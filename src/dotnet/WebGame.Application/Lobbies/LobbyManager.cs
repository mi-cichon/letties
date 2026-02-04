using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Application.Lobbies;

public class LobbyManager(IEnumerable<IGameLobby> gameLobbies, ILogger<LobbyManager> logger) : ILobbyManager
{
    private readonly ConcurrentDictionary<string, IGameLobby> _playerAssignedLobbies = new();
    
    public Result<IReadOnlyList<GameLobbyItem>> GetLobbies()
    {
        return gameLobbies
            .Select(lobby => new GameLobbyItem(lobby.LobbyId, lobby.State, lobby.PlayerCount))
            .ToList();
    }
    
    public async Task<Result<JoinDetails>> AssignPlayerToLobby(Guid playerId, Guid lobbyId, string playerConnectionId, string playerName)
    {
        var gameLobby = GetLobbyById(lobbyId);
        if (gameLobby == null)
        {
            logger.LogWarning("Player {PlayerId} attempted to join unknown lobby {LobbyId}", playerId, lobbyId);
            return Result<JoinDetails>.Failure(Error.InvalidState);
        }
        
        logger.LogInformation("Player {PlayerId} ({PlayerName}) joining lobby {LobbyId}", playerId, playerName, lobbyId);
        var result = await gameLobby.AssignPlayer(playerId, playerConnectionId, playerName);
        _playerAssignedLobbies.TryAdd(playerConnectionId, gameLobby);
        return result;
    }

    public Result<LobbyStateDetails> GetLobbyState(Guid playerId, string playerConnectionId)
    {
        if (!_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            gameLobby = _playerAssignedLobbies.Values.FirstOrDefault(lobby => lobby.IsPlayerInLobby(playerId).Value);
        }

        if (gameLobby == null)
        {
            return Result<LobbyStateDetails>.Failure(Error.InvalidState);
        }

        return gameLobby.GetLobbyState();
    }

    public async Task<Result> PlayerDisconnected(string playerConnectionId)
    {
        if (_playerAssignedLobbies.TryRemove(playerConnectionId, out var gameLobby))
        {
            logger.LogInformation("Player connection {ConnectionId} disconnected from lobby {LobbyId}", playerConnectionId, gameLobby.LobbyId);
            return await gameLobby.PlayerDisconnected(playerConnectionId);
        }
        
        return Result.Success();
    }

    public async Task<Result> LeaveLobby(Guid playerId, string playerConnectionId)
    {
        var lobby = gameLobbies.FirstOrDefault(lobby => lobby.IsPlayerInLobby(playerId).Value);

        if (lobby == null)
        {
            logger.LogWarning("Player {PlayerId} attempted to leave lobby but is not in one.", playerId);
            return Result.Failure(Error.InvalidState);
        }
        
        logger.LogInformation("Player {PlayerId} leaving lobby {LobbyId}", playerId, lobby.LobbyId);
        var result = await lobby.LeaveLobby(playerId);
        _playerAssignedLobbies.TryRemove(playerConnectionId, out _);
        return result;
    }

    public async Task<Result> SendMessage(string playerConnectionId, string playerName, string message)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            await gameLobby.SendMessage(playerName, message);
        }
        
        return Result.Success();
    }

    public async Task<Result> JoinSeat(string playerConnectionId, Guid playerId, Guid seatId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.JoinSeat(playerId, seatId);
        }

        return Result.Failure(Error.InvalidState);
    }
    
    public async Task<Result> LeaveSeat(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.LeaveSeat(playerId);
        }
        
        return Result.Failure(Error.InvalidState);
    }

    public async Task<Result> UpdateLobbySettings(string playerConnectionId, Guid playerId, LobbySettingsModel settingsModel)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.UpdateLobbySettings(playerId, settingsModel);
        }
        
        return Result.Failure(Error.InvalidState);
    }

    public Result<GameDetails> GetGameDetails(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return gameLobby.GetGameDetails(playerId);
        }
        
        return Result<GameDetails>.Failure(Error.InvalidState);
    }

    public async Task<Result> StartGame(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            logger.LogInformation("Player {PlayerId} starting game in lobby {LobbyId}", playerId, gameLobby.LobbyId);
            return await gameLobby.StartGame(playerId);
        }
        
        logger.LogWarning("Player {PlayerId} attempted to start game but is not in a lobby.", playerId);
        return Result.Failure(Error.InvalidState);
    }

    public Result<MoveResult> HandleMove(string playerConnectionId, Guid playerId, MoveRequestModel request)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return gameLobby.HandleMove(playerId, request);
        }
        
        return Result<MoveResult>.Failure(Error.InvalidState);
    }

    public Result HandleSwapTile(string playerConnectionId, Guid playerId, List<Guid> tileIdsToSwap)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return gameLobby.HandleSwapTiles(playerId, tileIdsToSwap);
        }
        
        return Result.Failure(Error.InvalidState);
    }
    
    public Result HandleSkipTurn(string playerConnectionId, Guid playerId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return gameLobby.HandleSkipTurn(playerId);
        }
        
        return Result.Failure(Error.InvalidState);
    }

    public async Task<Result> AddBotToLobby(string playerConnectionId, Guid playerId, Guid seatId, BotDifficulty difficulty)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.AddBotToLobby(playerId, seatId, difficulty);
        }
        
        return Result.Failure(Error.InvalidState);
    }

    public async Task<Result> RemoveBotFromLobby(string playerConnectionId, Guid playerId, Guid seatId)
    {
        if (_playerAssignedLobbies.TryGetValue(playerConnectionId, out var gameLobby))
        {
            return await gameLobby.RemoveBotFromLobby(playerId, seatId);
        }
        
        return Result.Failure(Error.InvalidState);
    }

    private IGameLobby? GetLobbyById(Guid lobbyId)
    {
        return gameLobbies.FirstOrDefault(lobby => lobby.LobbyId == lobbyId);
    }
}