namespace WebGame.Application.Lobbies;

public record LobbyPlayer(Guid PlayerId, string PlayerConnectionId, string PlayerName);