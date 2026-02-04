using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Domain.Interfaces.Lobbies;

public interface IGameLobby
{
    int PlayerCount { get; }
    Guid LobbyId { get; }
    GameLobbyState State { get; }
    #region Lobby State
    Task<Result<JoinDetails>> AssignPlayer(Guid playerId, string playerConnectionId, string playerName);
    Result<LobbyStateDetails> GetLobbyState();
    Task<Result> LeaveLobby(Guid playerId);
    Task<Result> PlayerDisconnected(string playerConnectionId);
    Task<Result> SendMessage(string playerName, string message);
    Task<Result> JoinSeat(Guid playerId, Guid seatId);
    Result<bool> IsPlayerInLobby(Guid playerId);
    Task<Result> LeaveSeat(Guid playerId);
    Task<Result> UpdateLobbySettings(Guid playerId, LobbySettingsModel settingsModel);
    Task<Result> AddBotToLobby(Guid addingPlayerId, Guid seatId, BotDifficulty difficulty);
    Task<Result> RemoveBotFromLobby(Guid removingPlayerId, Guid seatId);
    #endregion
    #region Game State
    Task<Result> StartGame(Guid playerId);
    Result<GameDetails> GetGameDetails(Guid playerId);
    Result<MoveResult> HandleMove(Guid playerId, MoveRequestModel moveRequest);
    Result HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap);
    Result HandleSkipTurn(Guid playerId);
    Result CheckGameRules();

    #endregion
}