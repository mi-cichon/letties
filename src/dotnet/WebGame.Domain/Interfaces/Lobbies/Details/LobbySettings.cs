using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Lobbies.Details;

public record LobbySettings(
    int TimeBank,
    GameLanguage Language,
    int TilesCount,
    BoardType BoardType);