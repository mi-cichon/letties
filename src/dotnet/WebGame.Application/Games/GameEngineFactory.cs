using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Application.Games;

public class GameEngineFactory(
    IGameLanguageProviderFactory gameLanguageProviderFactory,
    IBoardGenerator boardGenerator) : IGameEngineFactory
{
    public ILetterGameEngine CreateEngine(LobbySettings settings, List<LobbyPlayerDetails> players, Action onStateChanged, Action<GameFinishedDetails> onGameFinished)
    {
        return new LetterGameEngine(
            gameLanguageProviderFactory,
            boardGenerator,
            settings, 
            players,
            onStateChanged,
            onGameFinished);
    }
}