using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Models;
using WebGame.Extensions;

namespace WebGame.Hubs;

[Authorize]
[SignalRHub]
public class GameHub(ILobbyManager lobbyManager) : Hub
{
    public Result<IReadOnlyList<GameLobbyItem>> GetLobbies()
    {
        return lobbyManager.GetLobbies();
    }
    
    public async Task<Result<JoinDetails>> Join(Guid lobbyId)
    {
        var playerName = Context.GetUsername();
        var playerId = Context.GetPlayerId();
        
        return await lobbyManager.AssignPlayerToLobby(playerId, lobbyId, Context.ConnectionId, playerName);
    }

    public Result<LobbyStateDetails> GetLobbyDetails()
    {
        return lobbyManager.GetLobbyState(Context.GetPlayerId(), Context.ConnectionId);
    }
    
    public async Task<Result> LeaveLobby()
    {
        var playerId = Context.GetPlayerId();
        return await lobbyManager.LeaveLobby(playerId, Context.ConnectionId);
    }
    
    public async Task<Result> EnterSeat(Guid seatId)
    {
        return await lobbyManager.JoinSeat(Context.ConnectionId, Context.GetPlayerId(), seatId);
    }
    
    public async Task<Result> LeaveSeat()
    {
        var playerId = Context.GetPlayerId();
        return await lobbyManager.LeaveSeat(Context.ConnectionId, playerId);
    }
    
    public async Task<Result> SendMessage(string message)
    {
        var username = Context.GetUsername();
        return await lobbyManager.SendMessage(Context.ConnectionId, username, message);
    }

    public async Task<Result> UpdateLobbySettings(LobbySettingsModel settingsModel)
    {
        return await lobbyManager.UpdateLobbySettings(Context.ConnectionId, Context.GetPlayerId(), settingsModel);
    }

    public async Task<Result> StartGame()
    {
        return await lobbyManager.StartGame(Context.ConnectionId, Context.GetPlayerId());
    }

    public Result<GameDetails> GetGameDetails()
    {
        return lobbyManager.GetGameDetails(Context.ConnectionId, Context.GetPlayerId());
    }

    public Result<MoveResult> HandleMove(MoveRequestModel request)
    {
        return lobbyManager.HandleMove(Context.ConnectionId, Context.GetPlayerId(), request);
    }
    
    public Result SwapTiles(List<Guid> tileIdsToSwap)
    {
        return lobbyManager.HandleSwapTile(Context.ConnectionId, Context.GetPlayerId(), tileIdsToSwap);
    }
    
    public Result SkipTurn()
    {
        return lobbyManager.HandleSkipTurn(Context.ConnectionId, Context.GetPlayerId());
    }

    public async Task<Result> AddBot(Guid seatId, BotDifficulty difficulty)
    {
        return await lobbyManager.AddBotToLobby(Context.ConnectionId, Context.GetPlayerId(), seatId, difficulty);
    }

    public async Task<Result> RemoveBot(Guid seatId)
    {
        return await lobbyManager.RemoveBotFromLobby(Context.ConnectionId, Context.GetPlayerId(), seatId);
    }

    public override async Task<Result> OnDisconnectedAsync(Exception? exception)
    {
        return await lobbyManager.PlayerDisconnected(Context.ConnectionId);
    }
}