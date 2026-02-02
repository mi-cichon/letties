using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Structures;

namespace WebGame.Domain.Interfaces.Languages;

public interface IGameLanguageProvider
{
    GameLanguage Language { get; }
    bool IsWordInLanguage(string word);
    IReadOnlyList<LetterTileItem> GetTileDefinitions();
    WordTrie GetWordTrie();
}