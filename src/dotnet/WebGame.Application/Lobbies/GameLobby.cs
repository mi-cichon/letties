using System.Collections.Concurrent;
using WebGame.Application.Constants;
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

    private const int PostGameDurationSeconds = 30;

    private static readonly ConcurrentDictionary<Guid, GameLobbySeat> DefaultLobbySeats = new()
    {
        [Guid.CreateVersion7()] = new GameLobbySeat(null, true, 1, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 2, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 3, false, null),
        [Guid.CreateVersion7()] = new GameLobbySeat(null, false, 4, false, null)
    };

    private ConcurrentDictionary<Guid, GameLobbySeat> _seats;

    #region Lobby State
    
    private static readonly LobbySettings DefaultLobbySettings = new(10, GameLanguage.Polish, 100, BoardType.Classic);

    private LobbySettings _lobbySettings;
    private readonly IGameContextService _gameContextService;
    private readonly IGameEngineFactory _gameEngineFactory;
    private readonly IRandomNameService _randomNameService;

    public GameLobby(IGameContextService gameContextService, IGameEngineFactory gameEngineFactory, IRandomNameService randomNameService)
    {
        _gameContextService = gameContextService;
        _gameEngineFactory = gameEngineFactory;
        _randomNameService = randomNameService;
        _lobbySettings = GetDefaultLobbySettings();
        _seats = GetDefaultLobbySeats();
    }

    private static readonly (int Min, int Max) SettingsTileRange = (50, 200);
    
    private static readonly (int Min, int Max) SettingsTimeBankRange = (3, 60);
    
    public async Task<LobbyStateDetails> AssignPlayer(Guid playerId, string playerConnectionId, string playerName)
    {
        var lobbyPlayer = new LobbyPlayer(playerId, playerConnectionId, playerName, false, null);
        _players.TryAdd(playerId, lobbyPlayer);
        
        await UpdateGroupWithLobbyState();
        await _gameContextService.AddToGroup(playerConnectionId, LobbyGroupName);
        await _gameContextService.NotifyGroup(LobbyGroupName, $"{playerName} has joined the lobby.");
        
        
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
        
        return currentState;
    }

    public async Task LeaveLobby(Guid playerId)
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
    }

    public async Task SendMessage(string playerName, string message)
    {
        await _gameContextService.SendChatMessageToGroup(LobbyGroupName, playerName, message);
    }

    public async Task<bool> JoinSeat(Guid playerId, Guid seatId)
    {
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId != null)
        {
            return false;
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
        return true;
    }

    public async Task PlayerDisconnected(string connectionId)
    {
        var playerId = _players.SingleOrDefault(x => x.Value.PlayerConnectionId == connectionId).Key;
        
        await LeaveLobby(playerId);
    }
    
    public async Task LeaveSeat(Guid playerId)
    {
        var seat = _seats.Select(x => new {SeatId = x.Key, Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId);

        if (seat == null)
        {
            return;
        }
        
        seat.Seat.PlayerId = null;
        await UpdateGroupWithLobbyState();
    }

    public bool IsPlayerInLobby(Guid playerId)
    {
        var seat = _seats
            .Select(x => new { Seat = x.Value })
            .FirstOrDefault(x => x.Seat.PlayerId == playerId);
        
        return seat != null || _players.ContainsKey(playerId);
    }

    public async Task UpdateLobbySettings(Guid playerId, LobbySettingsModel settingsModel)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            throw new InvalidOperationException("Only lobby admins can update lobby settings.");
        }

        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Lobby settings can only be updated while lobby is in lobby state.");
        }
        
        if (!ValidateLobbySettings(settingsModel))
        {
            throw new InvalidOperationException("Invalid lobby settings.");
        }
        
        _lobbySettings = new LobbySettings(settingsModel.TimeBank, settingsModel.Language, settingsModel.TilesCount, settingsModel.BoardType);
        await UpdateGroupWithLobbyState();
    }

    public async Task AddBotToLobby(Guid addingPlayerId, Guid seatId, BotDifficulty difficulty)
    {
        if (!IsPlayerLobbyAdmin(addingPlayerId))
        {
            throw new InvalidOperationException("Only lobby admins can add bots to lobby.");
        }

        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Bots can only be added while lobby is in lobby state.");
        }
        
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId != null || seat.IsAdmin)
        {
            throw new InvalidOperationException("Only non-admin seats can be assigned to bot players.");
        }
        var botName = _randomNameService.GetRandomBotName(_lobbySettings.Language);
        var botPlayer = new LobbyPlayer(Guid.CreateVersion7(), string.Empty, $"Bot {botName}", true, difficulty);
        seat.PlayerId = botPlayer.PlayerId;
        seat.IsBot = true;
        seat.BotDifficulty = difficulty;
        
        _players.TryAdd(botPlayer.PlayerId, botPlayer);
        await UpdateGroupWithLobbyState();
    }

    public async Task RemoveBotFromLobby(Guid removingPlayerId, Guid seatId)
    {
        if (!IsPlayerLobbyAdmin(removingPlayerId))
        {
            throw new InvalidOperationException("Only lobby admins can remove bots from lobby.");
        }

        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Bots can only be removed while lobby is in lobby state.");
        }
        
        var seat = _seats.GetValueOrDefault(seatId);
        if (seat == null || seat.PlayerId == null || !seat.IsBot)
        {
            throw new InvalidOperationException("Only bot seats can be removed.");
        }
        
        var botId = seat.PlayerId.Value;
        
        seat.PlayerId = null;
        seat.IsBot = false;
        seat.BotDifficulty = null;
        
        _players.TryRemove(botId, out _);
        
        await UpdateGroupWithLobbyState();
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

    public async Task StartGame(Guid playerId)
    {
        if (!IsPlayerLobbyAdmin(playerId))
        {
            throw new InvalidOperationException("Only lobby admins can start the game.");
        }
        
        if (State != GameLobbyState.Lobby)
        {
            throw new InvalidOperationException("Game can only be started while lobby is in lobby state.");
        }

        if (_seats.Count(x => x.Value.PlayerId != null) < 2)
        {
            throw new InvalidOperationException("Not enough players to start the game.");
        }
        
        State = GameLobbyState.Game;
        
        await _gameContextService.NotifyGroup(LobbyGroupName, "Game has started.");
        
        GameEngine = _gameEngineFactory.CreateEngine(
            _lobbySettings, 
            _players.Values.Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty)).ToList(), 
            () => _ = UpdateGroupWithGameState(),
            gameFinishedDetails => _ = FinishGame(gameFinishedDetails));
        
        await UpdateGroupWithLobbyState();
        await UpdateGroupWithGameState();
    }

    public MoveResult HandleMove(Guid playerId, MoveRequestModel moveRequest)
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

    public GameDetails GetGameDetails(Guid playerId)
    {
        if (State != GameLobbyState.Game || GameEngine == null)
        {
            throw new InvalidOperationException("Game details can only be retrieved while game is in progress.");
        }

        return GameEngine.GetGameDetails(playerId);
    }

    public void HandleSkipTurn(Guid playerId)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                throw new InvalidOperationException("Game details can only be retrieved while game is in progress.");
            }

            GameEngine.HandleSkipTurn(playerId);
        }
    }

    public void HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap)
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                throw new InvalidOperationException("Game details can only be retrieved while game is in progress.");
            }

            GameEngine.HandleSwapTiles(playerId, tileIdsToSwap);
        }
    }

    public void CheckGameRules()
    {
        lock (_sync)
        {
            if (State != GameLobbyState.Game || GameEngine == null)
            {
                return;
            }

            GameEngine.CheckGameRules();
        }
    }
    
    #endregion

    private bool IsPlayerLobbyAdmin(Guid playerId)
    {
        return _seats.Select(x => new {Seat = x.Value}).FirstOrDefault(x => x.Seat.PlayerId == playerId)?.Seat.IsAdmin ?? false;
    }

    private async Task UpdateGroupWithLobbyState()
    {
        var playerDetails = _players.Values.ToArray()
            .Select(x => new LobbyPlayerDetails(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty))
            .ToArray();
        
        var seatDetails = _seats
            .Select(x => new LobbySeatDetails(x.Key, x.Value.PlayerId, x.Value.IsAdmin, x.Value.Order))
            .ToArray();
        
        var currentState = new LobbyStateDetails(LobbyId, playerDetails, seatDetails, _lobbySettings, State, GameFinishedDetails);
        
        await _gameContextService.SendToGroup(LobbyGroupName, GameMethods.LobbyUpdated, currentState);
    }

    private async Task UpdateGroupWithGameState()
    {
        var updateTasks = _players
            .Where(x => !x.Value.IsBot)
            .Select(x => _gameContextService.SendToPlayer(x.Value.PlayerConnectionId,
                GameMethods.GameUpdated, GameEngine!.GetGameDetails(x.Key)));
        
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
        _seats = GetDefaultLobbySeats();
        _lobbySettings = GetDefaultLobbySettings();
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

    private ConcurrentDictionary<Guid, GameLobbySeat> GetDefaultLobbySeats()
    {
        var seats = new ConcurrentDictionary<Guid, GameLobbySeat>();
        
        foreach (var seat in DefaultLobbySeats)
        {
            seats.TryAdd(seat.Key, new GameLobbySeat(seat.Value.PlayerId, seat.Value.IsAdmin, seat.Value.Order, seat.Value.IsBot, seat.Value.BotDifficulty));
        }
        
        return seats;
    }
    
    
}