using System.Collections.Concurrent;
using WebGame.Application.Constants;
using WebGame.Domain.Interfaces;
using WebGame.Domain.Interfaces.Lobbies;

namespace WebGame.Application.Lobbies;

public class GameLobby(IGameContextService gameContextService) : IGameLobby
{
    private readonly ConcurrentDictionary<Guid, LobbyPlayer> _players = new();
    
    public Guid LobbyId { get; } = Guid.CreateVersion7();
    
    public int PlayerCount => _players.Count;
    
    public GameLobbyState State { get; private set; } = GameLobbyState.Lobby;

    private readonly ConcurrentDictionary<Guid, GameLobbySeat> _seats =
        new()
        {
            [Guid.CreateVersion7()] = new GameLobbySeat(null, true, 1),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 2),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 3),
            [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 4)
        };

    private string LobbyGroupName => LobbyId.ToString("N");
    
    public async Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName)
    {
        var lobbyPlayer = new LobbyPlayer(playerId, playerConnectionId, playerName);
        _players.TryAdd(playerId, lobbyPlayer);
        await gameContextService.AddToGroup(playerConnectionId, LobbyGroupName);
        await gameContextService.NotifyGroup(LobbyGroupName, $"{playerName} has joined the lobby.");
        
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
            var currentSeatModel = new LobbySeatDetails(currentSeat.SeatId, currentSeat.Seat.PlayerId, currentSeat.Seat.IsAdmin, currentSeat.Seat.Order);
            await SendToGroup(GameMethods.PlayerLeftSeat, currentSeatModel);
        }
        
        seat.PlayerId = playerId;
        var seatModel = new LobbySeatDetails(seatId, seat.PlayerId, seat.IsAdmin, seat.Order);
        await SendToGroup(GameMethods.PlayerEnteredSeat, seatModel);
        return true;
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        var playerId = _players.SingleOrDefault(x => x.Value.PlayerConnectionId == connectionId).Key;
        
        if (_players.TryRemove(playerId, out var lobbyPlayer))
        {
            await gameContextService.RemoveFromGroup(connectionId, LobbyGroupName);
            await LeaveSeat(playerId);
            await gameContextService.NotifyGroup(LobbyGroupName, $"{lobbyPlayer.PlayerName} has left the game.");
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

    public bool IsPlayerInLobby(Guid playerId)
    {
        _players.TryGetValue(playerId, out var lobbyPlayer);
        return lobbyPlayer != null;
    }

    private async Task SendToGroup<T>(string method, T data)
    {
        await gameContextService.SendToGroup(LobbyGroupName, method, data);
    }
}