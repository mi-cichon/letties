using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Domain.Interfaces.Bots;

public interface IBotStrategy
{
    BotDifficulty Difficulty { get; }
    
    Task<BotAction> GetNextMove(
        BoardLayoutDetails boardLayout, 
        List<PlacedTileDetails> placedTiles, 
        PlayerHandDetails botHand,
        GameLanguage language);
}