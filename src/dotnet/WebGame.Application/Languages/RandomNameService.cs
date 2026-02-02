using Bogus;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Languages;

public class RandomNameService : IRandomNameService
{
    private static readonly Faker FakerPolish = new("pl");
    private static readonly Faker FakerEnglish = new("en");
    
    public string GetRandomBotName(GameLanguage locale)
    {
        var faker = locale switch
        {
            GameLanguage.Polish => FakerPolish,
            GameLanguage.English => FakerEnglish,
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, null)
        };
        
        return faker.Name.FirstName();
    }
}