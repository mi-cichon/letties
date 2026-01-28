using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Extensions;

namespace WebGame.Hubs;

[Authorize]
[SignalRHub]
public class GameHub(ILobbyManager lobbyManager) : Hub
{
    public IReadOnlyList<GameLobbyItem> GetLobbies()
    {
        return lobbyManager.GetLobbies();
    }
    
    public async Task<JoinResponse> Join(Guid lobbyId)
    {
        var playerName = Context.GetUsername();
        var playerId = Context.GetPlayerId();
        
        var lobbyState = await lobbyManager.AssignPlayerToLobby(playerId, lobbyId, Context.ConnectionId, playerName);
        return new JoinResponse(playerId, lobbyState);
    }
    
    public async Task LeaveLobby()
    {
        var playerId = Context.GetPlayerId();
        await lobbyManager.LeaveLobby(playerId);
    }
    
    public async Task<bool> EnterSeat(Guid seatId)
    {
        return await lobbyManager.JoinSeat(Context.ConnectionId, Context.GetPlayerId(), seatId);
    }
    
    public async Task LeaveSeat()
    {
        await lobbyManager.LeaveSeat(Context.ConnectionId, Context.GetPlayerId());
    }
    
    public async Task SendMessage(string message)
    {
        var username = Context.GetUsername();
        await lobbyManager.SendMessage(Context.ConnectionId, username, message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await lobbyManager.PlayerDisconnected(Context.ConnectionId);
    }
}

public record JoinResponse(Guid PlayerId, LobbyStateDetails LobbyState);