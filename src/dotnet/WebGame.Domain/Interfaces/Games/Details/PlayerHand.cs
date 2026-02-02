namespace WebGame.Domain.Interfaces.Games.Details;

public class PlayerHand(List<TileInstanceDetails> tiles, TimeSpan remainingTime, bool isOnline, bool timeDepleted)
{
    public List<TileInstanceDetails> Tiles { get; set; } = tiles;
    public TimeSpan RemainingTime { get; set; } = remainingTime;
    public bool IsOnline { get; set; } = isOnline;
    public bool TimeDepleted { get; set; } = timeDepleted;
};