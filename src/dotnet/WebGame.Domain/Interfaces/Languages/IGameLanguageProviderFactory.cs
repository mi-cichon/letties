using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Languages;

public interface IGameLanguageProviderFactory
{
    IGameLanguageProvider CreateProvider(GameLanguage language);
}