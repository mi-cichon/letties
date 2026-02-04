using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Games.MoveCalculations;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Bots;

public class HardBotStrategy(
    IGameLanguageProviderFactory languageProviderFactory,
    IMoveSimulator moveSimulator,
    IMoveValueCalculator moveValueCalculator,
    ILogger<HardBotStrategy> logger
    ) : IBotStrategy
{
    private readonly Random _random = new();

    private const int MaxLettersToUse = 7;
    private readonly (int From, int To) WaitRangeSeconds = (2, 15);

    public BotDifficulty Difficulty => BotDifficulty.Hard;
    
    public async Task<BotAction> GetNextMove(
        BoardLayoutDetails boardLayout,
        List<PlacedTileDetails> placedTiles,
        PlayerHandDetails botHand,
        GameLanguage language)
    {
        var waitSeconds = _random.Next(WaitRangeSeconds.From, WaitRangeSeconds.To);
        await Task.Delay(waitSeconds * 1000);
        
        var start = Stopwatch.GetTimestamp();

        if (placedTiles.Count == 0 && botHand.Tiles.Count == 0)
        {
            return new SkipTurnAction();
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

        var bestMove = (Move: (SimulatedMove?)null, Score: -1);

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
            
            if (result.PointsEarned > bestMove.Score)
            {
                bestMove = (move, result.PointsEarned);
            }
        }

        var selectedMove = bestMove.Move ?? validMoves.First();

        var moveRequest = new MoveRequestModel(
            selectedMove.Placements.Select(p => new TilePlacementModel(
                p.TileId, 
                p.CellId, 
                p.SelectedValueId
            )).ToList()
        );

        var elapsed = Stopwatch.GetElapsedTime(start);
        logger.LogDebug("Hard bot move calculation took {ElapsedMilliseconds} ms.", elapsed.TotalMilliseconds);
        return new MakeMoveAction(moveRequest);
    }
}