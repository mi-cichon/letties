namespace WebGame.Domain.Interfaces.Lobbies;

public record GameLobbyItem(Guid LobbyId, GameLobbyState State, int PlayerCount);