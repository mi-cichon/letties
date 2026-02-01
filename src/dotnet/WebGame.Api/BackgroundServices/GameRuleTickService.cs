using WebGame.Domain.Interfaces.Lobbies;

namespace WebGame.BackgroundServices;

public sealed class GameRulesTickService(IEnumerable<IGameLobby> lobbies, ILogger<GameRulesTickService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var lobby in lobbies)
                {
                    try
                    {
                        lobby.CheckGameRules();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "CheckGameRules failed for lobby {LobbyId}", lobby.LobbyId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}