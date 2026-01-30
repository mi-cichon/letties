using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Application.Games;

public class GameEngineFactory(
    IGameLanguageProviderFactory gameLanguageProviderFactory,
    IBoardGenerator boardGenerator) : IGameEngineFactory
{
    public ILetterGameEngine CreateEngine(LobbySettings settings, List<Guid> playerIds, Action onStateChanged)
    {
        return new LetterGameEngine(
            gameLanguageProviderFactory,
            boardGenerator,
            settings, 
            playerIds,
            onStateChanged);
    }
}