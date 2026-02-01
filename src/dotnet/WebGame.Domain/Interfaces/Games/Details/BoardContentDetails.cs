namespace WebGame.Domain.Interfaces.Games.Details;

public record BoardContentDetails(
    List<PlacedTileDetails> PlacedTiles
);

public record PlacedTileDetails(
    Guid CellId,
    Guid TileId,
    Guid ValueId,
    Guid PlayerId,
    Guid? SelectedValueId);