using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Interfaces.Lobbies.Models;
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

    public LobbyStateDetails GetLobbyDetails()
    {
        return lobbyManager.GetLobbyState(Context.GetPlayerId(), Context.ConnectionId);
    }
    
    public async Task LeaveLobby()
    {
        var playerId = Context.GetPlayerId();
        await lobbyManager.LeaveLobby(playerId, Context.ConnectionId);
    }
    
    public async Task<bool> EnterSeat(Guid seatId)
    {
        return await lobbyManager.JoinSeat(Context.ConnectionId, Context.GetPlayerId(), seatId);
    }
    
    public async Task LeaveSeat()
    {
        var playerId = Context.GetPlayerId();
        await lobbyManager.LeaveSeat(Context.ConnectionId, playerId);
    }
    
    public async Task SendMessage(string message)
    {
        var username = Context.GetUsername();
        await lobbyManager.SendMessage(Context.ConnectionId, username, message);
    }

    public async Task UpdateLobbySettings(LobbySettingsModel settingsModel)
    {
        await lobbyManager.UpdateLobbySettings(Context.ConnectionId, Context.GetPlayerId(), settingsModel);
    }

    public async Task StartGame()
    {
        await lobbyManager.StartGame(Context.ConnectionId, Context.GetPlayerId());
    }

    public GameDetails GetGameDetails()
    {
        return lobbyManager.GetGameDetails(Context.ConnectionId, Context.GetPlayerId());
    }

    public MoveResult HandleMove(MoveRequestModel request)
    {
        return lobbyManager.HandleMove(Context.ConnectionId, Context.GetPlayerId(), request);
    }
    
    public void SwapTiles(List<Guid> tileIdsToSwap)
    {
        lobbyManager.HandleSwapTile(Context.ConnectionId, Context.GetPlayerId(), tileIdsToSwap);
    }
    
    public void SkipTurn()
    {
        lobbyManager.HandleSkipTurn(Context.ConnectionId, Context.GetPlayerId());
    }

    public async Task AddBot(Guid seatId, BotDifficulty difficulty)
    {
        await lobbyManager.AddBotToLobby(Context.ConnectionId, Context.GetPlayerId(), seatId, difficulty);
    }

    public async Task RemoveBot(Guid seatId)
    {
        await lobbyManager.RemoveBotFromLobby(Context.ConnectionId, Context.GetPlayerId(), seatId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await lobbyManager.PlayerDisconnected(Context.ConnectionId);
    }
}

public record JoinResponse(Guid PlayerId, LobbyStateDetails LobbyState);