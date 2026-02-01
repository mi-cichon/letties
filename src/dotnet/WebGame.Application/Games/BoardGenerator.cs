using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Lobbies.Enums;

public class BoardGenerator : IBoardGenerator
{
    public BoardLayoutDetails GenerateBoard(BoardType boardType)
    {
        return boardType switch
        {
            BoardType.Classic => GenerateFromTemplate(GetClassicTemplate()),
            BoardType.Arena => GenerateFromTemplate(GetArenaTemplate()),
            BoardType.Wildlands => GenerateFromTemplate(GetWildlandsTemplate()),
            BoardType.Widelands => GenerateFromTemplate(GetWidelandsTemplate()),
            _ => throw new ArgumentException($"Unknown board type: {boardType}")
        };
    }

    private BoardLayoutDetails GenerateFromTemplate(string[] template)
    {
        var height = template.Length;
        var width = template[0].Length;
        var cells = new List<BoardCellDetails>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var charType = template[y][x];
                cells.Add(new BoardCellDetails(
                    Id: Guid.NewGuid(),
                    X: x,
                    Y: y,
                    Type: MapCharToCellType(charType)
                ));
            }
        }

        return new BoardLayoutDetails(width, height, cells);
    }

    private static LetterCellType MapCharToCellType(char c) => c switch
    {
        '3' => LetterCellType.TripleWord,
        '2' => LetterCellType.DoubleWord,
        'T' => LetterCellType.TripleLetter,
        'D' => LetterCellType.DoubleLetter,
        '*' => LetterCellType.Center,
        'X' => LetterCellType.Blocked,
        _ => LetterCellType.Normal
    };

    private static string[] GetClassicTemplate() => [
        "3..D...3...D..3",
        ".2...T...T...2.",
        "..2...D.D...2..",
        "D..2...D...2..D",
        "....2.....2....",
        ".T...T...T...T.",
        "..D...D.D...D..",
        "3..D...*...D..3",
        "..D...D.D...D..",
        ".T...T...T...T.",
        "....2.....2....",
        "D..2...D...2..D",
        "..2...D.D...2..",
        ".2...T...T...2.",
        "3..D...3...D..3"
    ];

    private static string[] GetArenaTemplate() => [
        "XXXXX.....XXXXX",
        "X3...D...D...3X",
        "X.2...D.D...2.X",
        "X..2...D...2..X",
        "X...*.....*...X",
        "....D..*..D....",
        "..D...D.D...D..",
        "..D..*.*.*..D..",
        "..D...D.D...D..",
        "....D..*..D....",
        "X...*.....*...X",
        "X..2...D...2..X",
        "X.2...D.D...2.X",
        "X3...D...D...3X",
        "XXXXX.....XXXXX"
    ];

    private static string[] GetWildlandsTemplate() => [
        "3....T...T....3",
        ".D...2...2...D.",
        "..D....2....D..",
        ".T.D...*...D.T.",
        "...2.D...D.2...",
        "T.....D.D.....T",
        "...2.*...*.2...",
        "3..D...*...D..3",
        "...2.*...*.2...",
        "T.....D.D.....T",
        "...2.D...D.2...",
        ".T.D...*...D.T.",
        "..D....2....D..",
        ".D...2...2...D.",
        "3....T...T....3"
    ];

    private static string[] GetWidelandsTemplate() =>
    [
        "3..D....3....D..3",
        ".2....T...T....2.",
        "..2....D.D....2..",
        "D..2....D....2..D",
        "....2.......2....",
        ".T....T...T....T.",
        "..D....D.D....D..",
        "..D....D.D....D..",
        "3..D....*....D..3",
        "..D....D.D....D..",
        "..D....D.D....D..",
        ".T....T...T....T.",
        "....2.......2....",
        "D..2....D....2..D",
        "..2....D.D....2..",
        ".2....T...T....2.",
        "3..D....3....D..3"
    ];
}