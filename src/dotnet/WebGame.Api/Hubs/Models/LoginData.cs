namespace WebGame.Hubs.Models;

public record LoginData(Guid PlayerId, string Username, string? JwtToken);