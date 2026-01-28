using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebGame.Extensions;
using WebGame.Lobbies;
using WebGame.Lobbies.Models;
using WebGame.Services;

namespace WebGame.Hubs;

[Authorize]
public class GameHub(LobbyManager lobbyManager, NotificationService notificationService) : Hub
{
    public async Task<JoinResponse> Join()
    {
        var playerName = Context.GetUsername();
        var playerId = Context.GetPlayerId();
        
        var lobbyState = await lobbyManager.AssignPlayerToLobby(playerId, Context.ConnectionId, playerName);
        return new JoinResponse(playerId, lobbyState);
    }

    public async Task<bool> EnterSeat(Guid seatId)
    {
        return await lobbyManager.JoinSeat(Context.GetPlayerId(), seatId);
    }
    
    public async Task SendMessage(string message)
    {
        var username = Context.GetUsername();
        await lobbyManager.SendMessage(username, message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await lobbyManager.LeaveLobby(Context.ConnectionId);
    }
}

public record JoinResponse(Guid PlayerId, LobbyStateDetails LobbyState);