using WebGame.Domain.Interfaces.Games.Enums;

namespace WebGame.Domain.Interfaces.Games.Details;

public record MoveResult(bool IsSuccess, MoveErrors? ErrorCode, string? ErrorMessage);