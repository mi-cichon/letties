using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Domain.Interfaces.Games;

public interface IGameEngineFactory
{
    ILetterGameEngine CreateEngine(LobbySettings settings, List<LobbyPlayerDetails> players, Action onStateChanged, Action<GameFinishedDetails> onGameFinished);
}