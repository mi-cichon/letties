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
    Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName);
    Task LeaveLobby(Guid playerId);
    Task PlayerDisconnected(string playerConnectionId);
    Task SendMessage(string playerName, string message);
    Task<bool> JoinSeat(Guid playerId, Guid seatId);
    bool IsPlayerInLobby(Guid playerId);
    Task LeaveSeat(Guid playerId);
    Task UpdateLobbySettings(Guid playerId, LobbySettingsModel settingsModel);
    #endregion
    #region Game State
    Task StartGame(Guid playerId);
    GameDetails GetGameDetails(Guid playerId);
    MoveResult HandleMove(Guid playerId, MoveRequestModel moveRequest);
    void HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap);
    void HandleSkipTurn(Guid playerId);
    #endregion
}