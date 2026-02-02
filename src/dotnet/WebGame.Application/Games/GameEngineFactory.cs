using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.MoveCalculations;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Bots;

namespace WebGame.Application.Games;

public class GameEngineFactory(
    IGameLanguageProviderFactory gameLanguageProviderFactory,
    IBoardGenerator boardGenerator,
    IMoveValueCalculator moveValueCalculator,
    IEnumerable<IBotStrategy> botStrategies) : IGameEngineFactory
{
    public ILetterGameEngine CreateEngine(LobbySettings settings, List<LobbyPlayerDetails> players, Action onStateChanged, Action<GameFinishedDetails> onGameFinished)
    {
        return new LetterGameEngine(
            gameLanguageProviderFactory,
            boardGenerator,
            moveValueCalculator,
            settings, 
            players,
            onStateChanged,
            onGameFinished,
            botStrategies);
    }
}