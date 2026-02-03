using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Games.MoveCalculations;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Bots;

public class MediumBotStrategy(
    IGameLanguageProviderFactory languageProviderFactory,
    IMoveSimulator moveSimulator,
    IMoveValueCalculator moveValueCalculator
    ) : IBotStrategy
{
    private readonly Random _random = new();

    private const int MaxLettersToUse = 7;
    private const double ExchangeProbability = 0.05;
    private readonly (int From, int To) WaitRangeSeconds = (3, 30);

    public BotDifficulty Difficulty => BotDifficulty.Medium;
    
    public async Task<BotAction> GetNextMove(
        BoardLayoutDetails boardLayout,
        List<PlacedTileDetails> placedTiles,
        PlayerHandDetails botHand,
        GameLanguage language)
    {
        var waitSeconds = _random.Next(WaitRangeSeconds.From, WaitRangeSeconds.To);
        await Task.Delay(waitSeconds * 1000);

        if (placedTiles.Count == 0)
        {
            if (botHand.Tiles.Count == 0) return new SkipTurnAction();
        }

        if (botHand.Tiles.Count > 0 && _random.NextDouble() < ExchangeProbability)
        {
            var countToExchange = _random.Next(1, botHand.Tiles.Count + 1);
             var tilesToExchange = botHand.Tiles
                .OrderBy(_ => _random.Next())
                .Take(countToExchange)
                .Select(t => t.TileId)
                .ToList();
            
             return new SwapTilesAction(tilesToExchange);
        }

        var validMoves = moveSimulator.SimulateMoves(
            language, 
            MaxLettersToUse, 
            boardLayout, 
            placedTiles, 
            botHand.Tiles);

        if (!validMoves.Any())
        {
            if (botHand.Tiles.Count > 0)
            {
                 var tilesToExchange = botHand.Tiles
                    .OrderBy(_ => _random.Next())
                    .Take(botHand.Tiles.Count)
                    .Select(t => t.TileId)
                    .ToList();
                 return new SwapTilesAction(tilesToExchange);
            }
            return new SkipTurnAction();
        }

        var languageProvider = languageProviderFactory.CreateProvider(language);
        var tileDefs = languageProvider.GetTileDefinitions().ToDictionary(d => d.ValueId);

        var scoredMoves = new List<(SimulatedMove Move, int Score)>();

        foreach (var move in validMoves)
        {
            var proposedMoves = move.Placements.Select(p =>
            {
                var cell = boardLayout.Cells.First(c => c.Id == p.CellId);
                var tile = botHand.Tiles.First(t => t.TileId == p.TileId);

                var displayValueId = p.SelectedValueId ?? tile.ValueId;
                
                var baseDef = tileDefs[tile.ValueId];
                var displayDef = tileDefs[displayValueId];
                
                var tileInstance = tile with { SelectedValueId = p.SelectedValueId };

                return new ProposedMove(cell, tileInstance, baseDef, displayDef);
            }).ToList();

            var result = moveValueCalculator.ScanForWords(boardLayout, tileDefs, placedTiles, proposedMoves);
            scoredMoves.Add((move, result.PointsEarned));
        }

        var sortedMoves = scoredMoves.OrderByDescending(x => x.Score).ToList();
        
        var topCount = Math.Max(5, (int)(sortedMoves.Count * 0.3));
        topCount = Math.Min(topCount, sortedMoves.Count);

        var candidateMoves = sortedMoves.Take(topCount).ToList();
        var selectedMove = candidateMoves[_random.Next(candidateMoves.Count)];

        var moveRequest = new MoveRequestModel(
            selectedMove.Move.Placements.Select(p => new TilePlacementModel(
                p.TileId, 
                p.CellId, 
                p.SelectedValueId
            )).ToList()
        );

        return new MakeMoveAction(moveRequest);
    }
}