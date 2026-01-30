using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Lobbies.Models;

public record LobbySettingsModel(
    int TimeBank,
    GameLanguage Language,
    int TilesCount,
    BoardType BoardType);