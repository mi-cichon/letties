using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Domain.Interfaces.Lobbies;

public interface ILobbyManager
{
    IReadOnlyList<GameLobbyItem> GetLobbies();
    Task<LobbyStateDetails> AssignPlayerToLobby(Guid playerId, Guid lobbyId, string playerConnectionId, string playerName);
    Task LeaveLobby(Guid playerId);
    Task PlayerDisconnected(string playerConnectionId);
    Task SendMessage(string playerConnectionId, string playerName, string message);
    Task<bool> JoinSeat(string playerConnectionId, Guid playerId, Guid seatId);
    Task LeaveSeat(string playerConnectionId, Guid playerId);
    Task UpdateLobbySettings(string playerConnectionId, Guid playerId, LobbySettingsModel settingsModel);
    GameDetails GetGameDetails(string playerConnectionId, Guid playerId);
    Task StartGame(string playerConnectionId, Guid playerId);
    MoveResult HandleMove(string playerConnectionId, Guid playerId, MoveRequestModel request);
}