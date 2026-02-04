using WebGame.Domain.Interfaces.Bots;

namespace WebGame.Application.Lobbies;

public class LobbyPlayer(Guid playerId, string playerConnectionId, string playerName, bool isBot, BotDifficulty? botDifficulty)
{
    public Guid PlayerId { get; } = playerId;
    public string PlayerConnectionId { get; set; } = playerConnectionId;
    public string PlayerName { get; } = playerName;
    public bool IsBot { get; } = isBot;
    public BotDifficulty? BotDifficulty { get; } = botDifficulty;
}