namespace WebGame.Domain.Interfaces.Games.MoveCalculations;

public record MoveCalculationResult(List<ScannedWord> FormedWords, int PointsEarned);