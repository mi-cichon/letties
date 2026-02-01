using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Lobbies.Details;

public record LobbyStateDetails(Guid LobbyId, LobbyPlayerDetails[] Players, LobbySeatDetails[] Seats, LobbySettings Settings, GameLobbyState State, GameFinishedDetails? GameFinishedDetails);

public record LobbyPlayerDetails(Guid PlayerId, string PlayerName);

public record LobbySeatDetails(Guid SeatId, Guid? PlayerId, bool IsAdmin, int Order);