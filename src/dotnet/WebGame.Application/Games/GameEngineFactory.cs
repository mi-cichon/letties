using Microsoft.Extensions.Logging;
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
    IMoveSimulator moveSimulator,
    IEnumerable<IBotStrategy> botStrategies,
    ILoggerFactory loggerFactory) : IGameEngineFactory
{
    public ILetterGameEngine CreateEngine(LobbySettings settings, List<LobbyPlayerDetails> players, Action onStateChanged, Action<GameFinishedDetails> onGameFinished)
    {
        var logger = loggerFactory.CreateLogger<LetterGameEngine>();
        return new LetterGameEngine(
            gameLanguageProviderFactory,
            boardGenerator,
            moveValueCalculator,
            moveSimulator,
            settings, 
            players,
            onStateChanged,
            onGameFinished,
            botStrategies,
            logger);
    }
}