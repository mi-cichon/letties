namespace WebGame.Lobbies;

public class GameLobbySeat(Guid? playerId, bool isAdmin, int order)
{
    public Guid? PlayerId { get; set; } = playerId;
    public bool IsAdmin { get; set; } = isAdmin;
    public int Order { get; set;  } = order;
}