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

    private readonly IGameLanguageProvider _languageProvider;
    private readonly IMoveValueCalculator _moveValueCalculator;

    private readonly BoardLayoutDetails _boardLayout;
    private readonly IEnumerable<IBotStrategy> _botStrategies;

    private readonly List<PlacedTileDetails> _placedTiles = new();
    private readonly Dictionary<Guid, PlayerHand> _playerHands = new();
    private readonly Dictionary<Guid, int> _playerScores = new();
    private readonly Stack<TileInstanceDetails> _tileBag = new();

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
        IEnumerable<IBotStrategy> botStrategies)
    {
        _botStrategies = botStrategies;
        _initialSettings = initialSettings;
        _gamePlayers = players
            .Select(x => new GamePlayer(x.PlayerId, x.PlayerName, x.IsBot, x.BotDifficulty))
            .Shuffle()
            .OrderBy(x => x.IsBot)
            .ToList();
        
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

    public GameDetails GetGameDetails(Guid requestingPlayerId)
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

    public void SetPlayerOnline(Guid playerId, bool isOnline)
    {
        if (_playerHands.TryGetValue(playerId, out var hand))
        {
            hand.IsOnline = isOnline;
        }
    }

    public MoveResult HandleMove(Guid playerId, MoveRequestModel request)
    {
        if (playerId != _currentTurnPlayerId)
        {
            return new MoveResult(false, MoveErrors.WrongTurn, "Not your turn!");
        }

        var playerHand = _playerHands[playerId];

        var tilesToPlace = new List<TileInstanceDetails>();
        foreach (var placement in request.Placements)
        {
            var tile = playerHand.Tiles.FirstOrDefault(t => t.TileId == placement.TileId);
            if (tile == null)
            {
                return new MoveResult(false, MoveErrors.TileNotInHand, "Tile not in hand!");
            }

            var isBlankTile = _blankValueId != null && tile.ValueId == _blankValueId;

            if (placement.SelectedValueId != null)
            {
                if (!isBlankTile)
                {
                    throw new InvalidOperationException("Blank tile cannot select a value.");
                }

                if (!_tileDefById.ContainsKey(placement.SelectedValueId.Value))
                {
                    throw new InvalidOperationException("SelectedValueId is not a valid tile definition");
                }

                if (_blankValueId != null && placement.SelectedValueId.Value == _blankValueId)
                {
                    throw new InvalidOperationException("Blank tile cannot select blank as its value.");
                }
            }

            if (isBlankTile && placement.SelectedValueId == null)
            {
                throw new InvalidOperationException("Blank tile must have SelectedValueId.");
            }

            tilesToPlace.Add(tile with { SelectedValueId = placement.SelectedValueId });
        }

        if (request.Placements.Any(p => _placedTiles.Any(pt => pt.CellId == p.CellId)))
        {
            return new MoveResult(false, MoveErrors.CellOccupied, "Cell is already occupied!");
        }

        return ExecuteMove(playerId, request.Placements, tilesToPlace);
    }

    public void HandleSkipTurn(Guid playerId)
    {
        if (playerId != _currentTurnPlayerId)
        {
            throw new InvalidOperationException("Not your turn!");
        }
        
        HandleScorelessTurn();
        
        var currentTurnStartedAt = _currentTurnStartedAt;
        RotateTurn();
        SubtractTime(playerId, currentTurnStartedAt, false);
        NotifyStateChanged();
        HandleBotTurns();
    }
    

    public void HandleSwapTiles(Guid playerId, List<Guid> tileIdsToSwap)
    {
        if (playerId != _currentTurnPlayerId)
        {
            throw new InvalidOperationException("Not your turn!");
        }

        if (tileIdsToSwap == null || !tileIdsToSwap.Any())
        {
            throw new InvalidOperationException("No tiles to swap.");
        }

        if (_tileBag.Count < 7)
        {
            throw new InvalidOperationException("Not enough tiles in bag to swap.");
        }

        var playerHand = _playerHands[playerId];
        var tilesToReturn = new List<TileInstanceDetails>();

        foreach (var tileId in tileIdsToSwap)
        {
            var tile = playerHand.Tiles.FirstOrDefault(t => t.TileId == tileId);
            if (tile == null)
            {
                throw new InvalidOperationException($"Tile with id {tileId} not found in player's hand.");
            }
            tilesToReturn.Add(tile);
        }

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
    }

    public void CheckGameRules()
    {
        var currentTurnPlayerHand = _playerHands[_currentTurnPlayerId];
        var timeElapsed = DateTimeOffset.UtcNow - _currentTurnStartedAt;
        
        if (currentTurnPlayerHand.RemainingTime <= timeElapsed)
        {
            currentTurnPlayerHand.RemainingTime = TimeSpan.Zero;
            currentTurnPlayerHand.TimeDepleted = true;
            RotateTurn();
            NotifyStateChanged();
        }
        
        if (_playerHands.All(h => h.Value.TimeDepleted))
        {
            FinishGame();
        }

        if (_playerHands.Where(x => !_gamePlayers.First(p => p.PlayerId == x.Key).IsBot)
            .All(h => !h.Value.IsOnline))
        {
            FinishGame();
        }
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
            return new MoveResult(false, MoveErrors.TilesNotConnected, "Tiles are not connected!");
        }

        if (!ValidateLine(proposedMoves))
        {
            return new MoveResult(false, MoveErrors.TilesNotInline, "Tiles are not in a line!");
        }

        var wordScanResult = _moveValueCalculator.ScanForWords(_boardLayout, _tileDefById, _placedTiles, proposedMoves);

        if (wordScanResult.FormedWords.Count == 0)
        {
            return new MoveResult(false, MoveErrors.InvalidWord, "Move must create at least one word!");
        }

        foreach (var word in wordScanResult.FormedWords)
        {
            if (!_languageProvider.IsWordInLanguage(word.Text))
            {
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

        if (_tileBag.Count == 0 && _playerHands[playerId].Tiles.Count == 0)
        {
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
        
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleBotMoveAsync(currentTurnPlayer.PlayerId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
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

    private async Task HandleBotMoveAsync(Guid playerBotId)
    {
        var botPlayer = _gamePlayers.First(p => p.PlayerId == playerBotId);
        if (!botPlayer.IsBot)
        {
            throw new InvalidOperationException("Player is not a bot.");
        }
        
        var botHand = _playerHands[playerBotId];
        var botDifficulty = botPlayer.BotDifficulty!;

        var strategy = _botStrategies.FirstOrDefault(s => s.Difficulty == botDifficulty);
        if (strategy == null)
        {
            throw new InvalidOperationException($"No strategy found for difficulty {botDifficulty}");
        }
        
        var botHandDetails = new PlayerHandDetails(
            botHand.Tiles.Select(t => new TileInstanceDetails(t.TileId, t.ValueId, t.SelectedValueId)).ToList()
        );

        var action = await strategy.GetNextMove(_boardLayout, _placedTiles, botHandDetails, _initialSettings.Language);

        lock (_lock)
        {
            if (_currentTurnPlayerId != playerBotId) return;

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
    }

    private void SubtractTime(Guid playerId, DateTimeOffset lastTurnStartTime, bool playerMoved)
    {
        var timeElapsed = DateTimeOffset.UtcNow - lastTurnStartTime;
        var remainingTime = _playerHands[playerId].RemainingTime - timeElapsed;
        
        if (remainingTime <= TimeSpan.Zero)
        {
            _playerHands[playerId].RemainingTime = TimeSpan.Zero;
            _playerHands[playerId].TimeDepleted = true;
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
        
            var gameFinishedDetails = new GameFinishedDetails(playersDetails, gameElapsedTime, finishedAt);
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

        var limit = 2 * _gamePlayers.Count;
        if (_consecutiveScorelessTurns >= limit)
        {
            FinishGame();
        }
    }

    private record GamePlayer(Guid PlayerId, string PlayerName, bool IsBot, BotDifficulty? BotDifficulty);
}