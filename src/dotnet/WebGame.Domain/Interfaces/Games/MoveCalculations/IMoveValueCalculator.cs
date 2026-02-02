using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Languages;

namespace WebGame.Domain.Interfaces.Games.MoveCalculations;

public interface IMoveValueCalculator
{
    public MoveCalculationResult ScanForWords(
        BoardLayoutDetails boardLayout,
        Dictionary<Guid, LetterTileItem> tileDefById,
        List<PlacedTileDetails> placedTiles,
        List<ProposedMove> proposedMoves
    );

    int CalculatePoints(List<ScannedWord> words, List<ProposedMove> proposedMoves);
}