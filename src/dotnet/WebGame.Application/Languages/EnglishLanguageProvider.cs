using WebGame.Application.Languages.Assets;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Languages;

public class EnglishLanguageProvider : IGameLanguageProvider
{
    public GameLanguage Language => GameLanguage.English;
    
    private readonly IReadOnlyList<LetterTileItem> _tileDefinitions;

    public EnglishLanguageProvider()
    {
        var tileAssets = EnglishLanguageAssets.TileDefinitions;

        _tileDefinitions = tileAssets.Select((d, index) => new LetterTileItem(
            ValueId: d.Id,
            ValueText: d.Text,
            BasePoints: d.Points,
            Weight: d.Weight
        )).ToList();
    }
    
    public bool IsWordInLanguage(string word)
    {
        return EnglishLanguageAssets.Dictionary.IsValid(word);
    }

    public IReadOnlyList<LetterTileItem> GetTileDefinitions()
    {
        return _tileDefinitions;
    }
}