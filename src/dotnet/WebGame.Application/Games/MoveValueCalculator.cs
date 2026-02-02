using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Games.MoveCalculations;
using WebGame.Domain.Interfaces.Languages;

namespace WebGame.Application.Games;

public class MoveValueCalculator : IMoveValueCalculator
{
    public MoveCalculationResult ScanForWords(
        BoardLayoutDetails boardLayout, 
        Dictionary<Guid, LetterTileItem> tileDefById,
        List<PlacedTileDetails> placedTiles,
        List<ProposedMove> proposedMoves
        )
    {
        var words = new List<ScannedWord>();

        var virtualBoard = placedTiles.Select(pt =>
        {
            var cell = boardLayout.Cells.First(c => c.Id == pt.CellId);

            var baseDef = tileDefById[pt.ValueId];
            var displayValueId = pt.SelectedValueId ?? pt.ValueId;

            return new TempTile(cell.X, cell.Y, baseDef.BasePoints, displayValueId, cell.Type, false);
        }).Concat(proposedMoves.Select(m =>
            new TempTile(m.Cell.X, m.Cell.Y, m.BaseDef.BasePoints, m.DisplayDef.ValueId, m.Cell.Type, true)
        )).ToDictionary(t => (t.X, t.Y));

        if (proposedMoves.Count == 1)
        {
            var hWord = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, true, virtualBoard, tileDefById);
            var vWord = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, false, virtualBoard, tileDefById);

            if (hWord != null) words.Add(hWord);
            if (vWord != null) words.Add(vWord);

            return new MoveCalculationResult(words, CalculatePoints(words, proposedMoves));
        }

        var mainIsHorizontal = proposedMoves.Count <= 1 || proposedMoves[0].Cell.Y == proposedMoves[1].Cell.Y;

        var main = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, mainIsHorizontal, virtualBoard, tileDefById);
        if (main != null) words.Add(main);

        words.AddRange(proposedMoves
            .Select(move => ScanLine(move.Cell.X, move.Cell.Y, !mainIsHorizontal, virtualBoard, tileDefById))
            .OfType<ScannedWord>());
        
        return new MoveCalculationResult(words, CalculatePoints(words, proposedMoves));
    }

    public int CalculatePoints(List<ScannedWord> words, List<ProposedMove> proposedMoves)
    {
        var pointsEarned = words.Sum(w => w.Points);

        if (proposedMoves.Count == 7)
        {
            pointsEarned += 50;
        }

        return pointsEarned;
    }
    
    private ScannedWord? ScanLine(
        int x, 
        int y, 
        bool horizontal, 
        Dictionary<(int, int), TempTile> board,
        Dictionary<Guid, LetterTileItem> tileDefById)
    {
        var dx = horizontal ? 1 : 0;
        var dy = horizontal ? 0 : 1;

        int startX = x, startY = y;
        while (board.ContainsKey((startX - dx, startY - dy)))
        {
            startX -= dx;
            startY -= dy;
        }

        var text = "";
        var wordScore = 0;
        var wordMultiplier = 1;
        var count = 0;

        int currX = startX, currY = startY;
        while (board.TryGetValue((currX, currY), out var tile))
        {
            text += tileDefById[tile.DisplayValueId].ValueText;

            var tilePoints = tile.Points;

            if (tile.IsNew)
            {
                tilePoints *= GetLetterMultiplier(tile.Type);
                wordMultiplier *= GetWordMultiplier(tile.Type);
            }

            wordScore += tilePoints;
            currX += dx;
            currY += dy;
            count++;
        }

        return count > 1 ? new ScannedWord(text, wordScore * wordMultiplier, horizontal) : null;
    }
    
    private int GetLetterMultiplier(LetterCellType type)
    {
        return type switch
        {
            LetterCellType.DoubleLetter => 2,
            LetterCellType.TripleLetter => 3,
            _ => 1
        };
    }

    private int GetWordMultiplier(LetterCellType type)
    {
        return type switch
        {
            LetterCellType.DoubleWord => 2,
            LetterCellType.TripleWord => 3,
            LetterCellType.Center => 2,
            _ => 1
        };
    }
    
    private record TempTile(int X, int Y, int Points, Guid DisplayValueId, LetterCellType Type, bool IsNew);
}