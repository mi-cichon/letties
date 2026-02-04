using Microsoft.Extensions.Logging;
using WebGame.Domain.Common;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Games.MoveCalculations;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Application.Games;

public class LetterGameEngine : ILetterGameEngine
{
    private readonly LobbySettings _initialSettings;
    private readonly List<GamePlayer> _gamePlayers;
    
    private readonly ILogger<LetterGameEngine> _logger;

    private readonly IGameLanguageProvider _languageProvider;
    private readonly IMoveValueCalculator _moveValueCalculator;

    private readonly BoardLayoutDetails _boardLayout;
    private readonly IEnumerable<IBotStrategy> _botStrategies;

    private readonly List<PlacedTileDetails> _placedTiles = new();
    private readonly Dictionary<Guid, PlayerHand> _playerHands = new();
    private readonly Dictionary<Guid, int> _playerScores = new();
    private readonly Stack<TileInstanceDetails> _tileBag = new();
    private readonly List<MoveHistoricDetails> _moveHistory = new();

    private readonly IReadOnlyList<LetterTileItem> _tileDefinitions;
    private readonly Dictionary<Guid, LetterTileItem> _tileDefById;
    private readonly Guid? _blankValueId;

    private Guid _currentTurnPlayerId;
    private DateTimeOffset _currentTurnStartedAt = DateTimeOffset.UtcNow;
    
    private readonly DateTimeOffset _gameStartedAt = DateTimeOffset.UtcNow;

    private event Action OnStateChanged;
    private event Action<GameFinishedDetails>  OnGameFinished;

    private const int TimeBelowMinuteBonusSeconds = 15;
    
    private int _consecutiveScorelessTurns = 0;
    private bool _gameFinished = false;
    private bool _botPlaying = false;
    
    private readonly Lock _lock = new();

    public LetterGameEngine(
        IGameLanguageProviderFactory gameLanguageProviderFactory,
        IBoardGenerator boardGenerator,
        IMoveValueCalculator moveValueCalculator,
        LobbySettings initialSettings,
        List<LobbyPlayerDetails> players,
        Action onStateChanged,
        Action<GameFinishedDetails>  onGameFinished,
        IEnumerable<IBotStrategy> botStrategies,
        ILogger<LetterGameEngine> logger)
    {
        _logger = logger;
        _botStrategies = botStrategies;
        _initialSettings = initialSettings;
        _gamePlayers = players
            .Select(x => new GamePlayer(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty))
            .Shuffle()
            .OrderBy(x => x.IsBot)
            .ToList();
        
        _logger.LogInformation("Initializing LetterGameEngine with {PlayerCount} players. Settings: {@Settings}", _gamePlayers.Count, initialSettings);

        _languageProvider = gameLanguageProviderFactory.CreateProvider(initialSettings.Language);
        _boardLayout = boardGenerator.GenerateBoard(_initialSettings.BoardType);
        _moveValueCalculator = moveValueCalculator;

        _tileDefinitions = _languageProvider.GetTileDefinitions();
        _tileDefById = _tileDefinitions.ToDictionary(d => d.ValueId, d => d);

        _blankValueId = _tileDefinitions.FirstOrDefault(d => d.ValueText is "?")?.ValueId;

        OnStateChanged += onStateChanged;
        OnGameFinished += onGameFinished;

        foreach (var gamePlayer in _gamePlayers)
        {
            _playerScores[gamePlayer.PlayerId] = 0;
        }

        InitializeTileBag();

        foreach (var gamePlayer in _gamePlayers)
        {
            var playerTiles = DrawTiles(7);
            _playerHands[gamePlayer.PlayerId] = new PlayerHand(playerTiles, TimeSpan.FromMinutes(initialSettings.TimeBank), true, false);
        }

        _currentTurnPlayerId = _gamePlayers.First().PlayerId;

        NotifyStateChanged();
    }

    public Result<GameDetails> GetGameDetails(Guid requestingPlayerId)
    {
        var myHand = _playerHands
            .GetValueOrDefault(requestingPlayerId);
            
        var myTiles = myHand?.Tiles
            .Select(t => new TileInstanceDetails(t.TileId, t.ValueId, t.SelectedValueId))
            .ToList();

        return new GameDetails(
            Layout: _boardLayout,
            TileDefinitions: _tileDefinitions
                .Select(d => new TileDefinitionDetails(d.ValueId, d.ValueText, d.BasePoints))
                .ToList(),
            BoardContent: new BoardContentDetails(_placedTiles),
            Scores: _gamePlayers.Select(gamePlayer => new PlayerScoreDto(
                PlayerId: gamePlayer.PlayerId,
                TotalPoints: _playerScores[gamePlayer.PlayerId],
                TilesRemainingInHand: _playerHands[gamePlayer.PlayerId].Tiles.Count,
                TimeRemaining: _playerHands[gamePlayer.PlayerId].RemainingTime,
                TimeDepleted: _playerHands[gamePlayer.PlayerId].TimeDepleted,
                PlayerName: gamePlayer.PlayerName
            )).ToList(),
            CurrentTurnPlayerId: _currentTurnPlayerId,
            CurrentTurnStartedAt: _currentTurnStartedAt,
            MyHand: myHand != null ? new PlayerHandDetails(myTiles!) : null,
            TilesRemainingInBag: _tileBag.Count
        );
    }

    public Result SetPlayerOnline(Guid playerId, bool isOnline)
    {
        if (_playerHands.TryGetValue(playerId, out var hand))
        {
            _logger.LogInformation("Setting player {PlayerId} online status to {IsOnline}", playerId, isOnline);
            hand.IsOnline = isOnline;
            return Result.Success();
        }

        _logger.LogWarning("Attempted to set online status for unknown player {PlayerId}", playerId);
        return Result.Failure(Error.InvalidState);
    }

    public Result<MoveResult> HandleMove(Guid playerId, MoveRequestModel request)
    {
        if (playerId != _currentTurnPlayerId)
        {
            _logger.LogWarning("Player {PlayerId} attempted move but it's not their turn. Current turn: {CurrentTurnPlayerId}", playerId, _currentTurnPlayerId);
            return new MoveResult(false, MoveErrors.WrongTurn, "Not your turn!");
        }

        var playerHand = _playerHands[playerId];

        var tilesToPlace = new List<TileInstanceDetails>();
        foreach (var placement in request.Placements)
        {
            var tile = playerHand.Tiles.FirstOrDefault(t => t.TileId == placement.TileId);
            if (tile == null)
            {
                _logger.LogWarning("Player {PlayerId} attempted to place tile {TileId} which is not in their hand.", playerId, placement.TileId);
                return new MoveResult(false, MoveErrors.TileNotInHand, "Tile not in hand!");
            }

            var isBlankTile = _blankValueId != null && tile.ValueId == _blankValueId;

            if (placement.SelectedValueId != null)
            {
                if (!isBlankTile)
                {
                    _logger.LogWarning("Player {PlayerId} provided SelectedValueId for non-blank tile {TileId}.", playerId, tile.TileId);
                    return Result<MoveResult>.Failure(Error.InvalidArgument);
                }

                if (!_tileDefById.ContainsKey(placement.SelectedValueId.Value))
                {
                    _logger.LogWarning("Player {PlayerId} provided invalid SelectedValueId {SelectedValueId}.", playerId, placement.SelectedValueId);
                    return Result<MoveResult>.Failure(Error.InvalidArgument);
                }

                if (_blankValueId != null && placement.SelectedValueId.Value == _blankValueId)
                {
                    _logger.LogWarning("Player {PlayerId} selected blank value for blank tile.", playerId);
                    return Result<MoveResult>.Failure(Error.InvalidArgument);
                }
            }

            if (isBlankTile && placement.SelectedValueId == null)
            {
                _logger.LogWarning("Player {PlayerId} placed blank tile without selecting a value.", playerId);
                return Result<MoveResult>.Failure(Error.InvalidArgument);
            }

            tilesToPlace.Add(tile with { SelectedValueId = placement.SelectedValueId });
        }

        if (request.Placements.Any(p => _placedTiles.Any(pt => pt.CellId == p.CellId)))
        {
            _logger.LogWarning("Player {PlayerId} attempted to place tile on occupied cell.", playerId);
            return new MoveResult(false, MoveErrors.CellOccupied, "Cell is already occupied!");
        }

        return ExecuteMove(playerId, request.Placements, tilesToPlace);
    }

    public Result HandleSkipTurn(Guid playerId)
    {
        if (playerId != _currentTurnPlayerId)
        {
            _logger.LogWarning("Player {PlayerId} attempted to skip turn but it's not their turn.", playerId);
            return Result.Failure(Error.InvalidState);
        }
        
        _logger.LogInformation("Player {PlayerId} skipping turn.", playerId);
        HandleScorelessTurn();
        
        var currentTurnStartedAt = _currentTurnStartedAt;
        RotateTurn();
        SubtractTime(playerId, currentTurnStartedAt, false);
        NotifyStateChanged();
        HandleBotTurns();
        
        return Result.Success();
    }
    

    public Result HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap)
    {
        if (playerId != _currentTurnPlayerId)
        {
            _logger.LogWarning("Player {PlayerId} attempted to swap tiles but it's not their turn.", playerId);
            return Result.Failure(Error.InvalidState);
        }

        if (tileIdsToSwap.Count == 0)
        {
            _logger.LogWarning("Player {PlayerId} attempted to swap 0 tiles.", playerId);
            return Result.Failure(Error.InvalidArgument);
        }

        if (_tileBag.Count < 7)
        {
            _logger.LogWarning("Player {PlayerId} attempted to swap tiles but bag has fewer than 7 tiles ({Count}).", playerId, _tileBag.Count);
            return Result.Failure(Error.InvalidState);
        }

        var playerHand = _playerHands[playerId];
        var tilesToReturn = new List<TileInstanceDetails>();

        foreach (var tileId in tileIdsToSwap)
        {
            var tile = playerHand.Tiles.FirstOrDefault(t => t.TileId == tileId);
            if (tile == null)
            {
                _logger.LogWarning("Player {PlayerId} attempted to swap tile {TileId} which is not in hand.", playerId, tileId);
                return Result.Failure(Error.InvalidArgument);
            }
            tilesToReturn.Add(tile);
        }
        
        _logger.LogInformation("Player {PlayerId} swapping {Count} tiles.", playerId, tilesToReturn.Count);

        foreach (var tile in tilesToReturn)
        {
            playerHand.Tiles.Remove(tile);
        }

        var newTiles = DrawTiles(tilesToReturn.Count);
        playerHand.Tiles.AddRange(newTiles);

        var bagList = _tileBag.ToList();
        bagList.AddRange(tilesToReturn);

        _tileBag.Clear();
        var shuffled = bagList.OrderBy(_ => Guid.CreateVersion7());
        foreach (var t in shuffled) _tileBag.Push(t);
        
        HandleScorelessTurn();

        var currentTurnStartedAt = _currentTurnStartedAt;
        RotateTurn();
        SubtractTime(playerId, currentTurnStartedAt, false);
        NotifyStateChanged();
        HandleBotTurns();
        
        return Result.Success();
    }

    public Result CheckGameRules()
    {
        var currentTurnPlayerHand = _playerHands[_currentTurnPlayerId];
        var timeElapsed = DateTimeOffset.UtcNow - _currentTurnStartedAt;
        
        if (currentTurnPlayerHand.RemainingTime <= timeElapsed)
        {
            _logger.LogInformation("Player {PlayerId} time depleted.", _currentTurnPlayerId);
            currentTurnPlayerHand.RemainingTime = TimeSpan.Zero;
            currentTurnPlayerHand.TimeDepleted = true;
            _consecutiveScorelessTurns = 0;
            RotateTurn();
            NotifyStateChanged();
        }
        
        if (_playerHands.All(h => h.Value.TimeDepleted))
        {
            _logger.LogInformation("All players time depleted. Finishing game.");
            FinishGame();
        }

        if (_playerHands.Where(x => !_gamePlayers.First(p => p.PlayerId == x.Key).IsBot)
            .All(h => !h.Value.IsOnline && h.Value.TimeDepleted))
        {
            _logger.LogInformation("All human players are offline and time depleted. Finishing game.");
            FinishGame();
        }
        
        return Result.Success();
    }

    private MoveResult ExecuteMove(Guid playerId, List<TilePlacementModel> placements, List<TileInstanceDetails> tiles)
    {
        var proposedMoves = placements.Select(p =>
        {
            var cell = _boardLayout.Cells.First(c => c.Id == p.CellId);
            var tile = tiles.First(t => t.TileId == p.TileId);

            var baseDef = _tileDefById[tile.ValueId];

            var displayValueId = tile.SelectedValueId ?? tile.ValueId;
            var displayDef = _tileDefById[displayValueId];

            return new ProposedMove(cell, tile, baseDef, displayDef);
        }).ToList();

        if (!ValidateConnectivity(proposedMoves))
        {
            _logger.LogWarning("Player {PlayerId} move failed: Tiles not connected.", playerId);
            return new MoveResult(false, MoveErrors.TilesNotConnected, "Tiles are not connected!");
        }

        if (!ValidateLine(proposedMoves))
        {
            _logger.LogWarning("Player {PlayerId} move failed: Tiles not inline.", playerId);
            return new MoveResult(false, MoveErrors.TilesNotInline, "Tiles are not in a line!");
        }

        var wordScanResult = _moveValueCalculator.ScanForWords(_boardLayout, _tileDefById, _placedTiles, proposedMoves);

        if (wordScanResult.FormedWords.Count == 0)
        {
            _logger.LogWarning("Player {PlayerId} move failed: No words formed.", playerId);
            return new MoveResult(false, MoveErrors.InvalidWord, "Move must create at least one word!");
        }

        foreach (var word in wordScanResult.FormedWords)
        {
            if (!_languageProvider.IsWordInLanguage(word.Text))
            {
                _logger.LogWarning("Player {PlayerId} move failed: Invalid word '{Word}'.", playerId, word.Text);
                return new MoveResult(false, MoveErrors.InvalidWord, $"Word '{word.Text}' is not valid!");
            }
        }

        foreach (var move in proposedMoves)
        {
            _placedTiles.Add(new PlacedTileDetails(
                move.Cell.Id,
                move.Tile.TileId,
                move.Tile.ValueId,
                playerId,
                move.Tile.SelectedValueId));

            var toRemove = _playerHands[playerId].Tiles.First(t => t.TileId == move.Tile.TileId);
            _playerHands[playerId].Tiles.Remove(toRemove);
        }

        _logger.LogInformation("Player {PlayerId} played valid move. Points: {Points}. Words: {Words}", playerId, wordScanResult.PointsEarned, string.Join(", ", wordScanResult.FormedWords.Select(w => w.Text)));

        _playerScores[playerId] += wordScanResult.PointsEarned;

        var newTiles = DrawTiles(placements.Count);
        _playerHands[playerId].Tiles.AddRange(newTiles);
        
        if (wordScanResult.PointsEarned > 0)
        {
            _consecutiveScorelessTurns = 0;
        }
        else
        {
            HandleScorelessTurn();
        }
        
        var playerName = _gamePlayers.First(p => p.PlayerId == playerId).PlayerName;

        var historicMove = new MoveHistoricDetails(
            playerId, 
            playerName, 
            wordScanResult.PointsEarned,
            _playerScores[playerId], 
            wordScanResult.FormedWords
                .Select(w => w.Text).ToList());
        
        _moveHistory.Add(historicMove);
        
        if (_tileBag.Count == 0 && _playerHands[playerId].Tiles.Count == 0)
        {
            _logger.LogInformation("Game over trigger: Tile bag empty and player {PlayerId} hand empty.", playerId);
            FinishGame();
            return new MoveResult(true, null, null);
        }

        var currentTurnStartedAt = _currentTurnStartedAt;
        RotateTurn();
        SubtractTime(playerId, currentTurnStartedAt, true);

        NotifyStateChanged();
        
        HandleBotTurns();

        return new MoveResult(true, null, null);
    }

    private bool ValidateConnectivity(List<ProposedMove> proposedMoves)
    {
        if (_placedTiles.Count == 0)
        {
            return proposedMoves.Any(m => m.Cell.Type == LetterCellType.Center);
        }

        var placedCoords = _placedTiles
            .Select(pt => _boardLayout.Cells.First(c => c.Id == pt.CellId))
            .Select(c => (c.X, c.Y))
            .ToHashSet();

        foreach (var m in proposedMoves)
        {
            var neighbors = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
            foreach (var (dx, dy) in neighbors)
            {
                if (placedCoords.Contains((m.Cell.X + dx, m.Cell.Y + dy)))
                    return true;
            }
        }

        return false;
    }

    private bool ValidateLine(List<ProposedMove> proposedMoves)
    {
        if (proposedMoves.Count <= 1) return true;

        var isHorizontal = proposedMoves.All(m => m.Cell.Y == proposedMoves[0].Cell.Y);
        var isVertical = proposedMoves.All(m => m.Cell.X == proposedMoves[0].Cell.X);

        if (!isHorizontal && !isVertical) return false;

        var allOccupied = _placedTiles
            .Select(pt => _boardLayout.Cells.First(c => c.Id == pt.CellId))
            .Select(c => (c.X, c.Y))
            .Union(proposedMoves.Select(m => (m.Cell.X, m.Cell.Y)))
            .ToHashSet();

        if (isHorizontal)
        {
            var y = proposedMoves[0].Cell.Y;
            var minX = proposedMoves.Min(m => m.Cell.X);
            var maxX = proposedMoves.Max(m => m.Cell.X);

            for (var x = minX; x <= maxX; x++)
            {
                if (!allOccupied.Contains((x, y))) return false;
            }
        }
        else
        {
            var x = proposedMoves[0].Cell.X;
            var minY = proposedMoves.Min(m => m.Cell.Y);
            var maxY = proposedMoves.Max(m => m.Cell.Y);

            for (var y = minY; y <= maxY; y++)
            {
                if (!allOccupied.Contains((x, y))) return false;
            }
        }

        return true;
    }

    private void RotateTurn()
    {
        var currentPlayer = _gamePlayers.First(p => p.PlayerId == _currentTurnPlayerId);
        var currentIndex = _gamePlayers.IndexOf(currentPlayer);

        for (var step = 1; step <= _gamePlayers.Count; step++)
        {
            var nextIndex = (currentIndex + step) % _gamePlayers.Count;
            var candidatePlayer = _gamePlayers[nextIndex];

            if (_playerHands.TryGetValue(candidatePlayer.PlayerId, out var hand) && !hand.TimeDepleted)
            {
                _currentTurnPlayerId = candidatePlayer.PlayerId;
                _currentTurnStartedAt = DateTimeOffset.UtcNow;
                return;
            }
        }

        FinishGame();
    }

    private void HandleBotTurns()
    {
        lock (_lock)
        {
            if (_botPlaying)
            {
                return;
            }
            
            _botPlaying = true;
            var currentTurnPlayer = _gamePlayers.First(p => p.PlayerId == _currentTurnPlayerId);
            
            if (!currentTurnPlayer.IsBot)
            {
                _botPlaying = false;
                return;
            }
            
            _logger.LogInformation("Starting bot turn for {PlayerName} ({PlayerId})", currentTurnPlayer.PlayerName, currentTurnPlayer.PlayerId);
        
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await HandleBotMoveAsync(currentTurnPlayer.PlayerId);
                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Bot {PlayerName} move successful.", currentTurnPlayer.PlayerName);
                    }
                    else
                    {
                         _logger.LogWarning("Bot {PlayerName} move failed: {Error}", currentTurnPlayer.PlayerName, result.Error);
                        lock (_lock)
                        {
                            if (_currentTurnPlayerId == currentTurnPlayer.PlayerId)
                            {
                                SubtractTime(currentTurnPlayer.PlayerId, _currentTurnStartedAt, false);
                                RotateTurn();
                                NotifyStateChanged();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during bot {PlayerName} turn execution", currentTurnPlayer.PlayerName);
                    lock (_lock)
                    {
                        if (_currentTurnPlayerId == currentTurnPlayer.PlayerId)
                        {
                            SubtractTime(currentTurnPlayer.PlayerId, _currentTurnStartedAt, false);
                            RotateTurn();
                            NotifyStateChanged();
                        }
                    }
                }
                finally
                {
                    lock (_lock)
                    {
                        _botPlaying = false;
                    }
                }
            });
        }
    }
    

    private async Task<Result> HandleBotMoveAsync(Guid playerBotId)
    {
        var botPlayer = _gamePlayers.First(p => p.PlayerId == playerBotId);
        if (!botPlayer.IsBot)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var botHand = _playerHands[playerBotId];
        var botDifficulty = botPlayer.BotDifficulty!;

        var strategy = _botStrategies.FirstOrDefault(s => s.Difficulty == botDifficulty);
        if (strategy == null)
        {
            return Result.Failure(Error.InvalidState);
        }
        
        var botHandDetails = new PlayerHandDetails(
            botHand.Tiles.Select(t => new TileInstanceDetails(t.TileId, t.ValueId, t.SelectedValueId)).ToList()
        );

        var action = await strategy.GetNextMove(_boardLayout, _placedTiles, botHandDetails, _initialSettings.Language);

        lock (_lock)
        {
            if (_currentTurnPlayerId != playerBotId)
            {
                return Result.Failure(Error.InvalidState);
            }

            switch (action)
            {
                case MakeMoveAction moveAction:
                    HandleMove(playerBotId, moveAction.Move);
                    break;
                case SwapTilesAction swapAction:
                    HandleSwapTiles(playerBotId, swapAction.TileIds);
                    break;
                case SkipTurnAction:
                    HandleSkipTurn(playerBotId);
                    break;
            }
        }
        
        return Result.Success();
    }

    private void SubtractTime(Guid playerId, DateTimeOffset lastTurnStartTime, bool playerMoved)
    {
        var timeElapsed = DateTimeOffset.UtcNow - lastTurnStartTime;
        var remainingTime = _playerHands[playerId].RemainingTime - timeElapsed;
        
        if (remainingTime <= TimeSpan.Zero)
        {
            _playerHands[playerId].RemainingTime = TimeSpan.Zero;
            _playerHands[playerId].TimeDepleted = true;
            _consecutiveScorelessTurns = 0;
            return;
        }

        if (playerMoved && remainingTime < TimeSpan.FromMinutes(1))
        {
            remainingTime += TimeSpan.FromSeconds(TimeBelowMinuteBonusSeconds);
        }
        
        _playerHands[playerId].RemainingTime = remainingTime;
    }

    private void FinishGame()
    {
        lock (_lock)
        {
            if (_gameFinished)
            {
                return;
            }
            
            _gameFinished = true;
            var finishedAt = DateTimeOffset.UtcNow;
        
            var gameElapsedTime = finishedAt - _gameStartedAt;
        
            var playersDetails = _gamePlayers.Select(x =>
            {
                var playerScore = _playerScores[x.PlayerId];
            
                return new GameFinishedPlayerDetails(x.PlayerId, x.PlayerName, playerScore);
            }).ToList();
        
            _logger.LogInformation("Game finished. Duration: {Duration}. Scores: {@Scores}", gameElapsedTime, playersDetails);

            var gameFinishedDetails = new GameFinishedDetails(playersDetails, _moveHistory, gameElapsedTime, finishedAt);
            OnGameFinished.Invoke(gameFinishedDetails);
        }
    }

    private void InitializeTileBag()
    {
        var defs = _tileDefinitions;
        var tempTiles = new List<TileInstanceDetails>();

        foreach (var def in defs)
        {
            var count = (int)Math.Round(_initialSettings.TilesCount * (def.Weight / 100.0));

            for (var i = 0; i < count; i++)
            {
                tempTiles.Add(new TileInstanceDetails(Guid.CreateVersion7(), def.ValueId, null));
            }
        }

        var shuffled = tempTiles.OrderBy(_ => Guid.CreateVersion7());
        foreach (var tile in shuffled) _tileBag.Push(tile);
    }

    private List<TileInstanceDetails> DrawTiles(int count)
    {
        var drawn = new List<TileInstanceDetails>();
        for (var i = 0; i < count && _tileBag.Count > 0; i++)
        {
            drawn.Add(_tileBag.Pop());
        }
        return drawn;
    }

    private void NotifyStateChanged()
    {
        OnStateChanged.Invoke();
    }
    
    private void HandleScorelessTurn()
    {
        var startCountingAtOrBelow = _initialSettings.TilesCount / 3;
        if (_tileBag.Count > startCountingAtOrBelow)
        {
            _consecutiveScorelessTurns = 0;
            return;
        }
        
        _consecutiveScorelessTurns++;

        var playersWithTime = _playerHands.Count(x => !x.Value.TimeDepleted);

        var limit = 2 * playersWithTime;
        if (_consecutiveScorelessTurns >= limit)
        {
            FinishGame();
        }
    }

    private record GamePlayer(Guid PlayerId, string PlayerName, bool IsBot, BotDifficulty? BotDifficulty);
}