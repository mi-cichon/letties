namespace WebGame.Domain.Interfaces.Games.Details;

public record GameFinishedDetails(Guid WinningPlayerId, int PlayerPoints, TimeSpan GameDuration, DateTimeOffset FinishedAt);