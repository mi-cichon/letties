namespace WebGame.Domain.Interfaces.Games.Details;

public record GameDetails(
    BoardLayoutDetails Layout,
    List<TileDefinitionDetails> TileDefinitions,
    BoardContentDetails BoardContent,
    List<PlayerScoreDto> Scores,
    Guid CurrentTurnPlayerId,
    DateTimeOffset CurrentTurnStartedAt,
    PlayerHandDetails? MyHand,
    int TilesRemainingInBag
);