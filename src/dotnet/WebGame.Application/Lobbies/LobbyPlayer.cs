using WebGame.Domain.Interfaces.Bots;

namespace WebGame.Application.Lobbies;

public record LobbyPlayer(Guid PlayerId, string PlayerConnectionId, string PlayerName, bool IsBot, BotDifficulty? BotDifficulty);