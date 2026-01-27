using WebGame.Lobbies.Models;

namespace WebGame.Lobbies;

public class LobbyManager(GameLobby gameLobby)
{
    public async Task<LobbyStateDetails> AssignPlayerToLobby(Guid playerId, string playerConnectionId, string playerName)
    {
        return await gameLobby.AssignPlayer(playerId, playerConnectionId, playerName);
    }
    
    public async Task LeaveLobby(string playerId)
    {
        await gameLobby.PlayerDisconnected(playerId);
    }

    public async Task SendMessage(string playerName, string message)
    {
        await gameLobby.SendMessage(playerName, message);
    }

    public async Task<bool> JoinSeat(Guid playerId, Guid seatId)
    {
        return await gameLobby.JoinSeat(playerId, seatId);
    }
}