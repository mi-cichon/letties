using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Details;

namespace WebGame.Application.Games;

public class LetterGameEngine : ILetterGameEngine
{
    private readonly LobbySettings _initialSettings;
    private readonly List<Guid> _playerIds;

    private readonly IGameLanguageProvider _languageProvider;
    
    private readonly BoardLayoutDetails _boardLayout;
    
    private readonly List<PlacedTileDetails> _placedTiles = new();
    private readonly Dictionary<Guid, List<TileInstanceDetails>> _playerHands = new();
    private readonly Dictionary<Guid, int> _playerScores = new();
    private readonly Stack<TileInstanceDetails> _tileBag = new();
    
    private Guid _currentTurnPlayerId;

    private event Action OnStateChanged;

    public LetterGameEngine(
        IGameLanguageProviderFactory gameLanguageProviderFactory,
        IBoardGenerator boardGenerator,
        LobbySettings initialSettings, 
        List<Guid> playerIds,
        Action onStateChanged)
    {
        _initialSettings = initialSettings;
        _playerIds = playerIds;
        _languageProvider = gameLanguageProviderFactory.CreateProvider(initialSettings.Language);
        _boardLayout = boardGenerator.GenerateBoard(_initialSettings.BoardType);
        
        OnStateChanged += onStateChanged;
        
        foreach (var id in _playerIds) _playerScores[id] = 0;
        
        InitializeTileBag();

        foreach (var playerId in _playerIds)
        {
            _playerHands[playerId] = DrawTiles(7);
        }

        _currentTurnPlayerId = playerIds.First();
        
        NotifyStateChanged();
    }
    
    public GameDetails GetGameDetails(Guid requestingPlayerId)
    {
        var myHand = _playerHands
            .GetValueOrDefault(requestingPlayerId)?
            .Select(t => new TileInstanceDetails(t.TileId, t.ValueId))
            .ToList();
        
        return new GameDetails(
            Layout: _boardLayout,
            TileDefinitions: _languageProvider.GetTileDefinitions()
                .Select(d => new TileDefinitionDetails(d.ValueId, d.ValueText, d.BasePoints))
                .ToList(),
            BoardContent: new BoardContentDetails(_placedTiles),
            Scores: _playerIds.Select(pid => new PlayerScoreDto(
                PlayerId: pid,
                TotalPoints: _playerScores[pid],
                TilesRemainingInHand: _playerHands[pid].Count
            )).ToList(),
            CurrentTurnPlayerId: _currentTurnPlayerId,
            MyHand: myHand != null ? new PlayerHandDetails(myHand) : null,
            TilesRemainingInBag: _tileBag.Count
        );
    }
    
    public MoveResult HandleMove(Guid playerId, MoveRequestModel request)
    {
        if (playerId != _currentTurnPlayerId)
        {
            return new MoveResult(false, MoveErrors.WrongTurn, "Not your turn!");
        }

        var playerHand = _playerHands[playerId];
        var tilesToPlace = new List<TileInstanceDetails>();
    
        foreach (var tile in request.Placements.Select(p => playerHand.FirstOrDefault(t => t.TileId == p.TileId)))
        {
            if (tile == null)
            {
                return new MoveResult(false, MoveErrors.TileNotInHand, "Tile not in hand!");
            }
            tilesToPlace.Add(tile);
        }

        if (request.Placements.Any(p => _placedTiles.Any(pt => pt.CellId == p.CellId)))
        {
            return new MoveResult(false, MoveErrors.CellOccupied, "Cell is already occupied!");
        }

        return ExecuteMove(playerId, request.Placements, tilesToPlace);
    }

    private MoveResult ExecuteMove(Guid playerId, List<TilePlacementModel> placements, List<TileInstanceDetails> tiles)
    {
        var proposedMoves = placements.Select(p =>
        {
            var cell = _boardLayout.Cells.First(c => c.Id == p.CellId);
            var tile = tiles.First(t => t.TileId == p.TileId);
            var def = _languageProvider.GetTileDefinitions().First(d => d.ValueId == tile.ValueId);
            return new ProposedMove(cell, tile, def);
        }).ToList();

        if (!ValidateConnectivity(proposedMoves))
        {
            return new MoveResult(false, MoveErrors.TilesNotConnected, "Tiles are not connected!" );
        }

        if (!ValidateLine(proposedMoves))
        {
            return new MoveResult(false, MoveErrors.TilesNotInline, "Tiles are not in a line!");
        }

        var formedWords = ScanForWords(proposedMoves);
        
        if (formedWords.Count == 0)
        {
            return new MoveResult(false, MoveErrors.InvalidWord, "Move must create at least one word!");
        }

        foreach (var word in formedWords)
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
                playerId));

            _playerHands[playerId].Remove(move.Tile);
        }

        var pointsEarned = formedWords.Sum(w => w.Points);
        
        if (placements.Count == 7)
        {
            pointsEarned += 50;
        }
        
        _playerScores[playerId] += pointsEarned;

        var newTiles = DrawTiles(placements.Count);
        _playerHands[playerId].AddRange(newTiles);

        RotateTurn();

        NotifyStateChanged();

        return new MoveResult(true, null, null);
    }
    
    private List<ScannedWord> ScanForWords(List<ProposedMove> proposedMoves)
    {
        var words = new List<ScannedWord>();

        var virtualBoard = _placedTiles.Select(pt => {
            var cell = _boardLayout.Cells.First(c => c.Id == pt.CellId);
            var def = _languageProvider.GetTileDefinitions().First(d => d.ValueId == pt.ValueId);
            return new TempTile(cell.X, cell.Y, def.BasePoints, def.ValueId, cell.Type, false);
        }).Concat(proposedMoves.Select(m => 
            new TempTile(m.Cell.X, m.Cell.Y, m.Def.BasePoints, m.Def.ValueId, m.Cell.Type, true)
        )).ToDictionary(t => (t.X, t.Y));
        
        if (proposedMoves.Count == 1)
        {
            var hWord = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, true, virtualBoard);
            var vWord = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, false, virtualBoard);
        
            if (hWord != null) words.Add(hWord);
            if (vWord != null) words.Add(vWord);
        
            return words;
        }

        var mainIsHorizontal = proposedMoves.Count <= 1 || proposedMoves[0].Cell.Y == proposedMoves[1].Cell.Y;

        var main = ScanLine(proposedMoves[0].Cell.X, proposedMoves[0].Cell.Y, mainIsHorizontal, virtualBoard);
        if (main != null) words.Add(main);

        words.AddRange(proposedMoves
            .Select(move => ScanLine(move.Cell.X, move.Cell.Y, !mainIsHorizontal, virtualBoard))
            .OfType<ScannedWord>());

        return words;
    }
    
    private ScannedWord? ScanLine(int x, int y, bool horizontal, Dictionary<(int, int), TempTile> board)
    {
        var dx = horizontal ? 1 : 0;
        var dy = horizontal ? 0 : 1;

        int startX = x, startY = y;
        while (board.ContainsKey((startX - dx, startY - dy)))
        {
            startX -= dx;
            startY -= dy;
        }

        var text = "";
        var wordScore = 0;
        var wordMultiplier = 1;
        var count = 0;

        int currX = startX, currY = startY;
        while (board.TryGetValue((currX, currY), out var tile))
        {
            text += _languageProvider.GetTileDefinitions().First(d => d.ValueId == tile.ValueId).ValueText;
        
            var tilePoints = tile.Points;

            if (tile.IsNew)
            {
                tilePoints *= GetLetterMultiplier(tile.Type);
                wordMultiplier *= GetWordMultiplier(tile.Type);
            }

            wordScore += tilePoints;
            currX += dx;
            currY += dy;
            count++;
        }

        return count > 1 ? new ScannedWord(text, wordScore * wordMultiplier, horizontal) : null;
    }
    
    private int GetLetterMultiplier(LetterCellType type)
    {
        return type switch
        {
            LetterCellType.DoubleLetter => 2,
            LetterCellType.TripleLetter => 3,
            _ => 1
        };
    }

    private int GetWordMultiplier(LetterCellType type)
    {
        return type switch
        {
            LetterCellType.DoubleWord => 2,
            LetterCellType.TripleWord => 3,
            LetterCellType.Center => 2,
            _ => 1
        };
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
        var currentIndex = _playerIds.IndexOf(_currentTurnPlayerId);
        var nextIndex = (currentIndex + 1) % _playerIds.Count;
        _currentTurnPlayerId = _playerIds[nextIndex];
    }

    private void InitializeTileBag()
    {
        var defs = _languageProvider.GetTileDefinitions();
        var tempTiles = new List<TileInstanceDetails>();

        foreach (var def in defs)
        {
            var count = (int)Math.Round(_initialSettings.TilesCount * (def.Weight / 100.0));
            
            for (var i = 0; i < count; i++)
            {
                tempTiles.Add(new TileInstanceDetails(Guid.CreateVersion7(), def.ValueId));
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
    
    private record ProposedMove(BoardCellDetails Cell, TileInstanceDetails Tile, LetterTileItem Def);
    
    private record ScannedWord(string Text, int Points, bool IsHorizontal);

    private record TempTile(int X, int Y, int Points, Guid ValueId, LetterCellType Type, bool IsNew);
}
