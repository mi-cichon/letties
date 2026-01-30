using WebGame.Domain.Interfaces.Games.Enums;

namespace WebGame.Domain.Interfaces.Languages;

public record LetterTileItem(    
    Guid ValueId,
    string ValueText,
    int BasePoints, 
    double Weight);