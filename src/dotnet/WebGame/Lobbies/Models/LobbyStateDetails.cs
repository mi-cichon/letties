namespace WebGame.Lobbies.Models;

public record LobbyStateDetails(LobbyPlayerDetails[] Players, LobbySeatDetails[] Seats, string LobbyId);

public record LobbyPlayerDetails(Guid PlayerId, string PlayerName);

public record LobbySeatDetails(Guid SeatId, Guid? PlayerId, bool IsAdmin, int Order);