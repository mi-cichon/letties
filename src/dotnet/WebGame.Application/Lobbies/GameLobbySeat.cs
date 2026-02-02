using WebGame.Domain.Interfaces.Bots;

namespace WebGame.Application.Lobbies;

public class GameLobbySeat(Guid? playerId, bool isAdmin, int order, bool isBot, BotDifficulty? botDifficulty)
{
    public Guid? PlayerId { get; set; } = playerId;
    public bool IsAdmin { get; set; } = isAdmin;
    public int Order { get; set;  } = order;
    public bool IsBot { get; set; } = isBot;
    public BotDifficulty? BotDifficulty { get; set; } = botDifficulty;
}