using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;

namespace WebGame.Domain.Interfaces.Games;

public interface ILetterGameEngine
{
    Result<GameDetails> GetGameDetails(Guid requestingPlayerId);
    Result<MoveResult> HandleMove(Guid playerId, MoveRequestModel request);
    Result HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap);
    Result HandleSkipTurn(Guid playerId);
    Result SetPlayerOnline(Guid playerId, bool isOnline);
    Result CheckGameRules();
}