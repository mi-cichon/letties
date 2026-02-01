namespace WebGame.Domain.Interfaces.Games.Details;

public record PlayerHandDetails(
    List<TileInstanceDetails> Tiles
);

public record PlayerScoreDto(
    Guid PlayerId,
    string PlayerName,
    int TotalPoints,
    int TilesRemainingInHand,
    TimeSpan TimeRemaining,
    bool TimeDepleted
);