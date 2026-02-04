namespace WebGame.Domain.Interfaces.Games.Details;

public class GameFinishedDetails(
    List<GameFinishedPlayerDetails> players,
    List<MoveHistoricDetails> moveHistory,
    TimeSpan gameDuration,
    DateTimeOffset finishedAt)
{
    public List<GameFinishedPlayerDetails> Players { get; set; } = players;
    public List<MoveHistoricDetails> MoveHistory { get; set; } = moveHistory;
    public TimeSpan GameDuration { get; set; } = gameDuration;
    public DateTimeOffset FinishedAt { get; set; } = finishedAt;
    public int PostGameDurationSeconds { get; set; }
}

public record GameFinishedPlayerDetails(Guid PlayerId, string PlayerName, int PlayerPoints);

public record MoveHistoricDetails(Guid PlayerId, string PlayerName, int GainedPoints, int TotalPoints, List<string> Words, BestMoveHistoricDetails BestMove, DateTimeOffset MoveTime);

public record BestMoveHistoricDetails(List<string> Word, int Points);