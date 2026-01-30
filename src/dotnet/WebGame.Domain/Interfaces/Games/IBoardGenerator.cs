using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Games;

public interface IBoardGenerator
{
    public BoardLayoutDetails GenerateBoard(BoardType boardType);
}