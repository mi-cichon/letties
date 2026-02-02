using WebGame.Domain.Interfaces.Games.Models;

namespace WebGame.Domain.Interfaces.Bots;

public abstract record BotAction;

public record MakeMoveAction(MoveRequestModel Move) : BotAction;

public record SwapTilesAction(List<Guid> TileIds) : BotAction;

public record SkipTurnAction : BotAction;