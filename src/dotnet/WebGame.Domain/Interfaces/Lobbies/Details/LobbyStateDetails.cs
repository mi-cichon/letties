using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Lobbies.Details;

public record LobbyStateDetails(Guid LobbyId, LobbyPlayerDetails[] Players, LobbySeatDetails[] Seats, LobbySettings Settings, GameLobbyState State, GameFinishedDetails? GameFinishedDetails);

public record LobbyPlayerDetails(Guid PlayerId, string PlayerName, bool IsBot, BotDifficulty? BotDifficulty);

public record LobbySeatDetails(Guid SeatId, Guid? PlayerId, bool IsAdmin, int Order);