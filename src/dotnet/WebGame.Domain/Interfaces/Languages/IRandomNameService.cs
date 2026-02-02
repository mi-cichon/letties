using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Languages;

public interface IRandomNameService
{
    string GetRandomBotName(GameLanguage locale);
}