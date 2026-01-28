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
}