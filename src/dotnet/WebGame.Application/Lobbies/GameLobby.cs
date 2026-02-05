using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WebGame.Application.Constants;
using WebGame.Domain.Common;
using WebGame.Domain.Interfaces;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Domain.Interfaces.Lobbies.Details;
using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Interfaces.Lobbies.Models;

namespace WebGame.Application.Lobbies;

public class GameLobby : IGameLobby
{
    private readonly ConcurrentDictionary<Guid, LobbyPlayer> _players = new();
    
    public Guid LobbyId { get; } = Guid.CreateVersion7();
    
    public int PlayerCount => _players.Count;
    
    public GameLobbyState State { get; private set; } = GameLobbyState.Lobby;
    
    private string LobbyGroupName => LobbyId.ToString("N");
    
    private ILetterGameEngine? GameEngine { get; set; }
    
    private GameFinishedDetails? GameFinishedDetails { get; set; }
    
    private readonly Lock _sync = new();

    private const int PostGameDurationSeconds = 60;

    private readonly ConcurrentDictionary<Guid, GameLobbySeat> _seats = new()
    {
        [Guid.CreateVersion7()] = new GameLobbySeat(null, true, 1, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 2, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 3, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 4, false, null)
    };
    
    private static readonly LobbySettings DefaultLobbySettings = new(10, GameLanguage.Polish, 100, BoardType.Classic);

    private LobbySettings _lobbySettings;
    private readonly IGameContextService _gameContextService;
    private readonly IGameEngineFactory _gameEngineFactory;
    private readonly IRandomNameService _randomNameService;
    private readonly ILogger<GameLobby> _logger;

    public GameLobby(
        IGameContextService gameContextService, 
        IGameEngineFactory gameEngineFactory, 
        IRandomNameService randomNameService,
        ILogger<GameLobby> logger)
    {
        _gameContextService = gameContextService;
        _gameEngineFactory = gameEngineFactory;
        _randomNameService = randomNameService;
        _logger = logger;
        _lobbySettings = GetDefaultLobbySettings();
    }
    
    #region Lobby State

    private static readonly (int Min, int Max) SettingsTileRange = (50, 200);
    
    private static readonly (int Min, int Max) SettingsTimeBankRange = (3, 60);
    
    public async Task<Result<JoinDetails>> AssignPlayer(Guid playerId, string playerConnectionId, string playerName)
    {
        _logger.LogInformation("Lobby {LobbyId}: Assigning player {PlayerName} ({PlayerId})", LobbyId, playerName, playerId);
        
        if (_players.TryGetValue(playerId, out var existingPlayer))
        {
            existingPlayer.PlayerConnectionId = playerConnectionId;
        }
        else
        {
            var lobbyPlayer = new LobbyPlayer(playerId, playerConnectionId, playerName, false, null);
            _players.TryAdd(playerId, lobbyPlayer);
        }
        
        await UpdateGroupWithLobbyState();
        await _gameContextService.AddToGroup(playerConnectionId, LobbyGroupName);
        
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        var currentState = new LobbyStateDetails(LobbyId, playerDetails, seatDetails, _lobbySettings, State, GameFinishedDetails);

        if (State == GameLobbyState.Game && GameEngine != null)
        {
            GameEngine.SetPlayerOnline(playerId, true);
        }
        
        return new JoinDetails(playerId, currentState);
    }

    public Result<LobbyStateDetails> GetLobbyState()
    {
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        return new LobbyStateDetails(LobbyId, playerDetails, seatDetails, _lobbySettings, State, GameFinishedDetails);
    }

    public async Task<Result> LeaveLobby(Guid playerId)
    {
        await _gameContextService.RemoveFromGroup(_players[playerId].PlayerConnectionId, LobbyGroupName);
        
        if(_players.TryRemove(playerId, out var player))
        {
            await _gameContextService.NotifyGroup(LobbyGroupName, $"{player.PlayerName} has left the lobby.");
        }
        
        var playerSeat = _seats.Select(x => new
        {
            SeatId = x.Key, 
            Seat = x.Value
        }).SingleOrDefault(x => x.Seat.PlayerId == playerId);

        if (playerSeat != null)
        {
            playerSeat.Seat.PlayerId = null;
        }
        
        if (State == GameLobbyState.Game && GameEngine != null)
        {
            GameEngine.SetPlayerOnline(playerId, false);
        }
        
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }

    public async Task<Result> SendMessage(string playerName, string message)
    {
        return await _gameContextService.SendChatMessageToGroup(LobbyGroupName, playerName, message);
    }

    public async Task<Result> JoinSeat(Guid playerId, Guid seatId)
    {
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId != null)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var currentSeat = _seats
            .Select(x => new { SeatId = x.Key, Seat = x.Value })
            .FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (currentSeat != null)
        {
            currentSeat.Seat.PlayerId = null;
        }
        
        seat.PlayerId = playerId;
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }

    public async Task<Result> PlayerDisconnected(string connectionId)
    {
        var playerEntry = _players.SingleOrDefault(x => x.Value.PlayerConnectionId == connectionId);
        if (playerEntry.Value != null)
        {
            var playerId = playerEntry.Key;
            _logger.LogInformation("Lobby {LobbyId}: Player {PlayerId} disconnected (connection lost)", LobbyId, playerId);
            
            if (State == GameLobbyState.Game && GameEngine != null)
            {
                GameEngine.SetPlayerOnline(playerId, false);
                await UpdateGroupWithLobbyState();
            }
            else
            {
                await LeaveLobby(playerId);
            }
        }
        
        return Result.Success();
    }
    
    public async Task<Result> LeaveSeat(Guid playerId)
    {
        var seat = _seats.Select(x => new {SeatId = x.Key, Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (seat == null)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        seat.Seat.PlayerId = null;
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }

    public Result<bool> IsPlayerInLobby(Guid playerId)
    {
        var seat = _seats
            .Select(x => new { Seat = x.Value })
            .FirstOrDefault(x => x.Seat.PlayerId == playerId);
        
        return seat != null || _players.ContainsKey(playerId);
    }

    public async Task<Result> UpdateLobbySettings(Guid playerId, LobbySettingsModel settingsModel)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            return Result.Failure(Error.InvalidState);
        }

        if (State != GameLobbyState.Lobby)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        if (!ValidateLobbySettings(settingsModel))
        {
            return Result.Failure(Error.InvalidArgument);
        }
        
        _lobbySettings = new LobbySettings(settingsModel.TimeBank, settingsModel.Language, settingsModel.TilesCount, settingsModel.BoardType);
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }

    public async Task<Result> AddBotToLobby(Guid addingPlayerId, Guid seatId, BotDifficulty difficulty)
    {
        if (!IsPlayerLobbyAdmin(addingPlayerId))
        {
            return Result.Failure(Error.InvalidState);
        }

        if (State != GameLobbyState.Lobby)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId != null || seat.IsAdmin)
        {
            return Result.Failure(Error.InvalidState);
        }
        var botName = _randomNameService.GetRandomBotName(_lobbySettings.Language);
        var botPlayer = new LobbyPlayer(Guid.CreateVersion7(), string.Empty, $"Bot {botName}", true, difficulty);
        seat.PlayerId = botPlayer.PlayerId;
        seat.IsBot = true;
        seat.BotDifficulty = difficulty;
        
        _players.TryAdd(botPlayer.PlayerId, botPlayer);
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }

    public async Task<Result> RemoveBotFromLobby(Guid removingPlayerId, Guid seatId)
    {
        if (!IsPlayerLobbyAdmin(removingPlayerId))
        {
            return Result.Failure(Error.InvalidState);
        }

        if (State != GameLobbyState.Lobby)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId == null || !seat.IsBot)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var botId = seat.PlayerId.Value;
        
        seat.PlayerId = null;
        seat.IsBot = false;
        seat.BotDifficulty = null;
        
        _players.TryRemove(botId, out _);
        
        await UpdateGroupWithLobbyState();
        return Result.Success();
    }
    

    private bool ValidateLobbySettings(LobbySettingsModel settingsModel)
    {
        if (settingsModel.TimeBank > SettingsTimeBankRange.Max || settingsModel.TimeBank < SettingsTimeBankRange.Min)
        {
            return false;
        }
        
        if(settingsModel.TilesCount > SettingsTileRange.Max || settingsModel.TilesCount < SettingsTileRange.Min)
        {
            return false;
        }

        return true;
    }
    
    #endregion
    
    #region Game State

    public async Task<Result> StartGame(Guid playerId)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            _logger.LogWarning("Lobby {LobbyId}: Player {PlayerId} attempted to start game but is not admin.", LobbyId, playerId);
            return Result.Failure(Error.InvalidState);
        }
        
        if (State != GameLobbyState.Lobby)
        {
            _logger.LogWarning("Lobby {LobbyId}: Attempt to start game in invalid state {State}.", LobbyId, State);
            return Result.Failure(Error.InvalidState);
        }

        if (_seats.Count(x => x.Value.PlayerId != null) < 2)
        {
            _logger.LogWarning("Lobby {LobbyId}: Attempt to start game with insufficient players.", LobbyId);
            return Result.Failure(Error.InvalidState);
        }
        
        State = GameLobbyState.Game;
        _logger.LogInformation("Lobby {LobbyId}: Game started.", LobbyId);
        
        await _gameContextService.NotifyGroup(LobbyGroupName, "Game has started.");
        
        GameEngine = _gameEngineFactory.CreateEngine(
            _lobbySettings, 
            _players.Values.Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty)).ToList(), 
            () => _ = UpdateGroupWithGameState(),
            gameFinishedDetails => _ = FinishGame(gameFinishedDetails));
        
        await UpdateGroupWithLobbyState();
        await UpdateGroupWithGameState();
        
        return Result.Success();
    }

    public Result<MoveResult> HandleMove(Guid playerId, MoveRequestModel moveRequest)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                return new MoveResult(false, MoveErrors.WrongTurn, "Not your turn!");
            }

            return GameEngine.HandleMove(playerId, moveRequest);
        }
    }

    public Result<GameDetails> GetGameDetails(Guid playerId)
    {
        if (State != GameLobbyState.Game || GameEngine == null)
        {
            return Result<GameDetails>.Failure(Error.InvalidState);
        }

        return GameEngine.GetGameDetails(playerId);
    }

    public Result HandleSkipTurn(Guid playerId)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                return Result.Failure(Error.InvalidState);
            }

            GameEngine.HandleSkipTurn(playerId);
        }
        
        return Result.Success();
    }

    public Result HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                return Result.Failure(Error.InvalidState);
            }

            GameEngine.HandleSwapTiles(playerId, tileIdsToSwap);
        }
        
        return Result.Success();
    }

    public Result CheckGameRules()
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                return Result.Failure(Error.InvalidState);
            }

            GameEngine.CheckGameRules();
        }
        
        return Result.Success();
    }
    
    #endregion

    private bool IsPlayerLobbyAdmin(Guid playerId)
    {
        return _seats.Select(x => new {Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId)?.Seat.IsAdmin ?? false;
    }

    private async Task UpdateGroupWithLobbyState()
    {
        var stateResult = GetLobbyState();
        if (stateResult.IsSuccess)
        {
            await _gameContextService.SendToGroup(LobbyGroupName, GameMethods.LobbyUpdated, stateResult.Value);
        }
    }

    private async Task UpdateGroupWithGameState()
    {
        var updateTasks = _players
            .Where(x => !x.Value.IsBot)
            .Select(async x =>
            {
                var detailsResult = GameEngine!.GetGameDetails(x.Key);
                if (detailsResult.IsSuccess)
                {
                    await _gameContextService.SendToPlayer(x.Value.PlayerConnectionId,
                        GameMethods.GameUpdated, detailsResult.Value);
                }
            });
        
        await Task.WhenAll(updateTasks);
    }
    
    private async Task FinishGame(GameFinishedDetails gameFinishedDetails)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game)
            {
                return;
            }
            
            _logger.LogInformation("Lobby {LobbyId}: Game finished.", LobbyId);
            gameFinishedDetails.PostGameDurationSeconds = PostGameDurationSeconds;
            
            GameFinishedDetails = gameFinishedDetails;
            State = GameLobbyState.PostGame;
            GameEngine = null;
        }
        
        await _gameContextService.NotifyGroup(LobbyGroupName, "Game has finished.");
        await UpdateGroupWithLobbyState();
        await WaitPostGame();
    }

    private async Task WaitPostGame()
    {
        await Task.Delay(TimeSpan.FromSeconds(PostGameDurationSeconds));
        await RestartLobby();
    }

    private async Task RestartLobby()
    {
        State = GameLobbyState.Lobby;
        GameEngine = null;
        GameFinishedDetails = null;
        SetDefaultLobbySeats();
        _lobbySettings = GetDefaultLobbySettings();

        var botPlayers = _players.Where(x => x.Value.IsBot).Select(x => x.Value.PlayerId).ToList();

        foreach (var botPlayer in botPlayers)
        {
            _players.TryRemove(botPlayer, out _);
        }
        
        await UpdateGroupWithLobbyState();
    }
    
    private LobbySettings GetDefaultLobbySettings()
    {
        return new LobbySettings(
            DefaultLobbySettings.TimeBank, 
            DefaultLobbySettings.Language, 
            DefaultLobbySettings.TilesCount, 
            DefaultLobbySettings.BoardType);
    }

    private void SetDefaultLobbySeats()
    {
        foreach (var seat in _seats)
        {
            seat.Value.IsBot = false;
            seat.Value.BotDifficulty = null;
            seat.Value.PlayerId = null;
        }
    }
    
    
}