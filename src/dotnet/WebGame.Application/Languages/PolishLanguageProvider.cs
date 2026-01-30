using WebGame.Application.Languages.Assets;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Languages;

public class PolishLanguageProvider : IGameLanguageProvider
{
    public GameLanguage Language => GameLanguage.Polish;
    
    private readonly IReadOnlyList<LetterTileItem> _tileDefinitions;

    public PolishLanguageProvider()
    {
        var tileAssets = PolishLanguageAssets.TileDefinitions;

        _tileDefinitions = tileAssets.Select((d, index) => new LetterTileItem(
            ValueId: d.Id,
            ValueText: d.Text,
            BasePoints: d.Points,
            Weight: d.Weight
        )).ToList();
    }
    
    public bool IsWordInLanguage(string word)
    {
        return PolishLanguageAssets.Dictionary.IsValid(word);
    }

    public IReadOnlyList<LetterTileItem> GetTileDefinitions()
    {
        return _tileDefinitions;
    }
}