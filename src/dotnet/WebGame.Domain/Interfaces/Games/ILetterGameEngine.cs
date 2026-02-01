using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;

namespace WebGame.Domain.Interfaces.Games;

public interface ILetterGameEngine
{
    GameDetails GetGameDetails(Guid requestingPlayerId);
    MoveResult HandleMove(Guid playerId, MoveRequestModel request);
    void HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap);
    void HandleSkipTurn(Guid playerId);
    void SetPlayerOnline(Guid playerId, bool isOnline);
    void CheckGameRules();
}