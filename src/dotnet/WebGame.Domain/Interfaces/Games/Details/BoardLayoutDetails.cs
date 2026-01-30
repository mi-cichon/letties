using WebGame.Domain.Interfaces.Games.Enums;

namespace WebGame.Domain.Interfaces.Games.Details;

public record BoardLayoutDetails(
    int Width,
    int Height,
    List<BoardCellDetails> Cells
);

public record BoardCellDetails(
    Guid Id,
    int X,
    int Y,
    LetterCellType Type
);