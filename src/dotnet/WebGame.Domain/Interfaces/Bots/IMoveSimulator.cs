using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Bots;

public interface IMoveSimulator
{
    List<SimulatedMove> SimulateMoves(
        GameLanguage language, 
        int maxLetters, 
        BoardLayoutDetails boardLayout,
        List<PlacedTileDetails> placedTiles,
        List<TileInstanceDetails> availableTiles);
}

public record SimulatedMove(List<SimulatedMoveLetter> Placements);
public record SimulatedMoveLetter(Guid TileId, Guid CellId, Guid? SelectedValueId);