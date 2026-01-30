using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;

namespace WebGame.Domain.Interfaces.Games;

public interface ILetterGameEngine
{
    GameDetails GetGameDetails(Guid requestingPlayerId);
    MoveResult HandleMove(Guid playerId, MoveRequestModel request);
}