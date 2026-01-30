using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Languages;

public class GameLanguageProviderFactory : IGameLanguageProviderFactory
{
    private readonly Dictionary<GameLanguage, IGameLanguageProvider> _languageProviders = new();

    public GameLanguageProviderFactory(IEnumerable<IGameLanguageProvider> languageProviders)
    {
        foreach (var provider in languageProviders)
        {
            _languageProviders.Add(provider.Language, provider);
        }
    }
    
    public IGameLanguageProvider CreateProvider(GameLanguage language)
    {
        if(!_languageProviders.TryGetValue(language, out var provider))
        {
            throw new ArgumentException($"Language {language} is not supported");
        }
        
        return provider;
    }
}