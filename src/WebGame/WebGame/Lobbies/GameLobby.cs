using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using WebGame.Constants;
using WebGame.Hubs;
using WebGame.Lobbies.Models;
using WebGame.Services;

namespace WebGame.Lobbies;

public class GameLobby(NotificationService notificationService, IHubContext<GameHub> gameContext)
{
    private readonly ConcurrentDictionary<Guid, LobbyPlayer> _players = new();
    private readonly Guid _lobbyId = Guid.CreateVersion7();

    private readonly ConcurrentDictionary<Guid, GameLobbySeat> _seats =
        new()
        {
            [Guid.CreateVersion7()] = new GameLobbySeat(null, true, 1),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 2),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 3),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 4)
        };

    private string LobbyGroupName => _lobbyId.ToString("N");
    
    public GameLobbyState State { get; private set; } = GameLobbyState.Lobby;
    
    public async Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName)
    {
        var lobbyPlayer = new LobbyPlayer(playerId, playerConnectionId, playerName);
        _players.TryAdd(playerId, lobbyPlayer);
        await gameContext.Groups.AddToGroupAsync(playerConnectionId, LobbyGroupName);
        await notificationService.NotifyGroup(LobbyGroupName, $"{playerName} has joined the lobby.");
        
        await SendToGroup(GameMethods.PlayerJoined, new PlayerJoined(playerId, playerName));
        
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        var currentState = new LobbyStateDetails(playerDetails, seatDetails, LobbyGroupName);
        return currentState;
    }

    public async Task SendMessage(string playerName, string message)
    {
        await notificationService.SendChatMessageToGroup(LobbyGroupName, playerName, message);
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
            var currentSeatModel = new LobbySeatDetails(currentSeat.SeatId, currentSeat.Seat.PlayerId, currentSeat.Seat.IsAdmin, currentSeat.Seat.Order);
            await SendToGroup(GameMethods.PlayerLeftSeat, currentSeatModel);
        }
        
        seat.PlayerId = playerId;
        var seatModel = new LobbySeatDetails(seatId, seat.PlayerId, seat.IsAdmin, seat.Order);
        await SendToGroup(GameMethods.PlayerEnteredSeat, seatModel);
        return true;
    }

    public async Task LeaveSeat(Guid playerId)
    {
        var seat = _seats.Select(x => new {SeatId = x.Key, Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (seat == null)
        {
            return;
        }
        
        seat.Seat.PlayerId = null;
        var seatDetails = new LobbySeatDetails(seat.SeatId, seat.Seat.PlayerId, seat.Seat.IsAdmin, seat.Seat.Order);
        await SendToGroup(GameMethods.PlayerLeftSeat, seatDetails);
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        var playerId = _players.SingleOrDefault(x => x.Value.PlayerConnectionId == connectionId).Key;
        
        if (_players.TryRemove(playerId, out var lobbyPlayer))
        {
            await gameContext.Groups.RemoveFromGroupAsync(connectionId, LobbyGroupName);
            await LeaveSeat(playerId);
            await notificationService.NotifyGroup(LobbyGroupName, $"{lobbyPlayer.PlayerName} has left the game.");
        }
    }

    private async Task SendToGroup<T>(string method, T data)
    {
        await gameContext.Clients.Group(LobbyGroupName).SendAsync(method, data);
    }
}