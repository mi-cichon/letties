namespace WebGame.Domain.Interfaces.Games.Details;

public record PlayerHandDetails(
    List<TileInstanceDetails> Tiles
);

public record PlayerScoreDto(
    Guid PlayerId,
    int TotalPoints,
    int TilesRemainingInHand
);