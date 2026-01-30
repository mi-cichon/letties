using System.Collections.Concurrent;
using WebGame.Application.Constants;
using WebGame.Domain.Interfaces;
using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Application.Lobbies;

public class GameLobby(IGameContextService gameContextService, IGameEngineFactory gameEngineFactory) : IGameLobby
{
    private readonly ConcurrentDictionary<Guid, LobbyPlayer> _players = new();
    
    public Guid LobbyId { get; } = Guid.CreateVersion7();
    
    public int PlayerCount => _players.Count;
    
    public GameLobbyState State { get; private set; } = GameLobbyState.Lobby;
    
    private string LobbyGroupName => LobbyId.ToString("N");
    
    private ILetterGameEngine? GameEngine { get; set; }
    
    private readonly ConcurrentDictionary<Guid, GameLobbySeat> _seats =
        new()
        {
            [Guid.CreateVersion7()] = new GameLobbySeat(null, true, 1),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 2),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 3),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 4)
        };

    #region Lobby State
    
    private LobbySettings _lobbySettings = new(10, GameLanguage.Polish, 100, BoardType.Classic);
    
    private static readonly (int Min, int Max) SettingsTileRange = (50, 200);
    
    private static readonly (int Min, int Max) SettingsTimeBankRange = (3, 60);
    
    public async Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName)
    {
        var lobbyPlayer = new LobbyPlayer(playerId, playerConnectionId, playerName);
        _players.TryAdd(playerId, lobbyPlayer);
        
        await UpdateGroupWithLobbyState();
        await gameContextService.AddToGroup(playerConnectionId, LobbyGroupName);
        await gameContextService.NotifyGroup(LobbyGroupName, $"{playerName} has joined the lobby.");
        
        
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        var currentState = new LobbyStateDetails(LobbyId, playerDetails, seatDetails, _lobbySettings, State);
        return currentState;
    }

    public async Task LeaveLobby(Guid playerId)
    {
        await gameContextService.RemoveFromGroup(_players[playerId].PlayerConnectionId, LobbyGroupName);
        
        if(_players.TryRemove(playerId, out var player))
        {
            await gameContextService.NotifyGroup(LobbyGroupName, $"{player.PlayerName} has left the lobby.");
        }
        
        var playerSeat = _seats.Select(x => new
        {
            SeatId = x.Key, 
            Seat = x.Value
        }).SingleOrDefault(x => x.Seat.PlayerId == playerId);

        if (playerSeat != null)
        {
            playerSeat.Seat.PlayerId = null;
        }
        
        await UpdateGroupWithLobbyState();
    }

    public async Task SendMessage(string playerName, string message)
    {
        await gameContextService.SendChatMessageToGroup(LobbyGroupName, playerName, message);
    }

    public async Task<bool> JoinSeat(Guid playerId, Guid seatId)
    {
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId != null)
        {
            return false;
        }
        
        var currentSeat = _seats
            .Select(x => new { SeatId = x.Key, Seat = x.Value })
            .FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (currentSeat != null)
        {
            currentSeat.Seat.PlayerId = null;
        }
        
        seat.PlayerId = playerId;
        await UpdateGroupWithLobbyState();
        return true;
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        var playerId = _players.SingleOrDefault(x => x.Value.PlayerConnectionId == connectionId).Key;
        
        await LeaveLobby(playerId);
    }
    
    public async Task LeaveSeat(Guid playerId)
    {
        var seat = _seats.Select(x => new {SeatId = x.Key, Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (seat == null)
        {
            return;
        }
        
        seat.Seat.PlayerId = null;
        await UpdateGroupWithLobbyState();
    }

    public bool IsPlayerInLobby(Guid playerId)
    {
        _players.TryGetValue(playerId, out var lobbyPlayer);
        return lobbyPlayer != null;
    }

    public async Task UpdateLobbySettings(Guid playerId, LobbySettingsModel settingsModel)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            throw new InvalidOperationException("Only lobby admins can update lobby settings.");
        }

        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Lobby settings can only be updated while lobby is in lobby state.");
        }
        
        if (!ValidateLobbySettings(settingsModel))
        {
            throw new InvalidOperationException("Invalid lobby settings.");
        }
        
        _lobbySettings = new LobbySettings(settingsModel.TimeBank, settingsModel.Language, settingsModel.TilesCount, settingsModel.BoardType);
        await UpdateGroupWithLobbyState();
    }

    private bool ValidateLobbySettings(LobbySettingsModel settingsModel)
    {
        if (settingsModel.TimeBank >= SettingsTimeBankRange.Max || settingsModel.TimeBank <= SettingsTimeBankRange.Min)
        {
            return false;
        }
        
        if(settingsModel.TilesCount >= SettingsTileRange.Max || settingsModel.TilesCount <= SettingsTileRange.Min)
        {
            return false;
        }

        return true;
    }
    
    #endregion
    
    #region Game State

    public async Task StartGame(Guid playerId)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            throw new InvalidOperationException("Only lobby admins can start the game.");
        }
        
        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Game can only be started while lobby is in lobby state.");
        }

        if (_seats.Count(x => x.Value.PlayerId != null) < 2)
        {
            throw new InvalidOperationException("Not enough players to start the game.");
        }
        
        State = GameLobbyState.Game;
        
        await gameContextService.NotifyGroup(LobbyGroupName, "Game has started.");
        
        GameEngine = gameEngineFactory.CreateEngine(
            _lobbySettings, 
            _players.Keys.ToList(), 
            () => _ = UpdateGroupWithGameState());
        
        await UpdateGroupWithLobbyState();
        await UpdateGroupWithGameState();
    }

    public MoveResult HandleMove(Guid playerId, MoveRequestModel moveRequest)
    {
        if (State != GameLobbyState.Game || GameEngine == null)
        {
            return new MoveResult(false, MoveErrors.WrongTurn, "Not your turn!");
        }
        
        return GameEngine.HandleMove(playerId, moveRequest);
    }

    public GameDetails GetGameDetails(Guid playerId)
    {
        if (State != GameLobbyState.Game || GameEngine == null)
        {
            throw new InvalidOperationException("Game details can only be retrieved while game is in progress.");
        }

        return GameEngine.GetGameDetails(playerId);
    }
    
    #endregion

    private bool IsPlayerLobbyAdmin(Guid playerId)
    {
        return _seats.Select(x => new {Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId)?.Seat.IsAdmin ?? false;
    }

    private async Task UpdateGroupWithLobbyState()
    {
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        var currentState = new LobbyStateDetails(LobbyId, playerDetails, seatDetails, _lobbySettings, State);
        
        await gameContextService.SendToGroup(LobbyGroupName, GameMethods.LobbyUpdated, currentState);
    }

    private async Task UpdateGroupWithGameState()
    {
        var updateTasks = _players.Select(x => gameContextService.SendToPlayer(x.Value.PlayerConnectionId,
            GameMethods.GameUpdated, GameEngine!.GetGameDetails(x.Key)));
        
        await Task.WhenAll(updateTasks);
    }
}