using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Lobbies.Details;

public record GameLobbyItem(Guid LobbyId, GameLobbyState State, int PlayerCount);