using WebGame.Domain.Interfaces.Games.Enums;

namespace WebGame.Domain.Interfaces.Games.Details;

public record TileDefinitionDetails(
    Guid ValueId,
    string ValueText,
    int BasePoints
);