namespace WebGame.Domain.Interfaces.Lobbies;

public interface IGameLobby
{
    int PlayerCount { get; }
    Guid LobbyId { get; }
    GameLobbyState State { get; }
    
    Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName);
    Task LeaveLobby(Guid playerId);
    Task PlayerDisconnected(string playerId);
    Task SendMessage(string playerName, string message);
    Task<bool> JoinSeat(Guid playerId, Guid seatId);
    Task LeaveSeat(Guid playerId);
    bool IsPlayerInLobby(Guid playerId);
}