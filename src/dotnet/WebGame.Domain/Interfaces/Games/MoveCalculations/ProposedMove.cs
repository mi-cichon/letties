using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Languages;

namespace WebGame.Domain.Interfaces.Games.MoveCalculations;

public record ProposedMove(
    BoardCellDetails Cell,
    TileInstanceDetails Tile,
    LetterTileItem BaseDef,
    LetterTileItem DisplayDef);