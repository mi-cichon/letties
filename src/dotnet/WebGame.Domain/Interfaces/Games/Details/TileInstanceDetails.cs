namespace WebGame.Domain.Interfaces.Games.Details;

public record TileInstanceDetails(
    Guid TileId,
    Guid ValueId,
    Guid? SelectedValueId);