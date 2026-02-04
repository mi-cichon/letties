using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Domain.Interfaces.Lobbies;

public interface ILobbyManager
{
    Result<IReadOnlyList<GameLobbyItem>> GetLobbies();
    Task<Result<JoinDetails>> AssignPlayerToLobby(Guid playerId, Guid lobbyId, string playerConnectionId, string playerName);
    Result<LobbyStateDetails> GetLobbyState(Guid playerId, string playerConnectionId);
    Task<Result> LeaveLobby(Guid playerId, string playerConnectionId);
    Task<Result> PlayerDisconnected(string playerConnectionId);
    Task<Result> SendMessage(string playerConnectionId, string playerName, string message);
    Task<Result> JoinSeat(string playerConnectionId, Guid playerId, Guid seatId);
    Task<Result> LeaveSeat(string playerConnectionId, Guid playerId);
    Task<Result> UpdateLobbySettings(string playerConnectionId, Guid playerId, LobbySettingsModel settingsModel);
    Result<GameDetails> GetGameDetails(string playerConnectionId, Guid playerId);
    Task<Result> StartGame(string playerConnectionId, Guid playerId);
    Result<MoveResult> HandleMove(string playerConnectionId, Guid playerId, MoveRequestModel request);
    Result HandleSwapTile(string playerConnectionId, Guid playerId, List<Guid> tileIdsToSwap);
    Result HandleSkipTurn(string playerConnectionId, Guid playerId);
    Task<Result> AddBotToLobby(string playerConnectionId, Guid playerId, Guid seatId, BotDifficulty difficulty);
    Task<Result> RemoveBotFromLobby(string playerConnectionId, Guid playerId, Guid seatId);
}