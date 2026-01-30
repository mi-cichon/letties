namespace WebGame.Domain.Interfaces.Games.Models;

public record MoveRequestModel(
    List<TilePlacementModel> Placements
);

public record TilePlacementModel(
    Guid TileId,
    Guid CellId
);