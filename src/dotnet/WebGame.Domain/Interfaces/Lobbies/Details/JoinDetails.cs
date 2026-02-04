namespace WebGame.Domain.Interfaces.Lobbies.Details;

public record JoinDetails(Guid PlayerId, LobbyStateDetails LobbyState);