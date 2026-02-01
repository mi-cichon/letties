namespace WebGame.Domain.Interfaces.Games.Details;

public class GameFinishedDetails(
    List<GameFinishedPlayerDetails> players,
    TimeSpan gameDuration,
    DateTimeOffset finishedAt)
{
    public List<GameFinishedPlayerDetails> Players { get; set; } = players;
    public TimeSpan GameDuration { get; set; } = gameDuration;
    public DateTimeOffset FinishedAt { get; set; } = finishedAt;
    public int PostGameDurationSeconds { get; set; }
}

public record GameFinishedPlayerDetails(Guid PlayerId, string PlayerName, int PlayerPoints);