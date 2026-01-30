using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Domain.Interfaces.Games;

public interface IGameEngineFactory
{
    ILetterGameEngine CreateEngine(LobbySettings settings, List<Guid> playerIds, Action onStateChanged);
}