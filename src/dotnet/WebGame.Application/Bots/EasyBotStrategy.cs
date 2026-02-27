using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Models;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;

namespace WebGame.Application.Bots;

public class EasyBotStrategy(
    IGameLanguageProviderFactory languageProviderFactory,
    IMoveSimulator moveSimulator,
    ILogger<EasyBotStrategy> logger
    ) : IBotStrategy
{
    private readonly Random _random = new();

    private const int MaxWordLength = 6;
    private const double ExchangeProbability = 0.10;
    private const double SkipProbability = 0.01;
    private const int MaxLettersToUse = 7;

    private readonly (int From, int To) WaitRangeSeconds = (5, 40);

    public BotDifficulty Difficulty => BotDifficulty.Easy;
    
    public async Task<BotAction> GetNextMove(
        BoardLayoutDetails boardLayout,
        List<PlacedTileDetails> placedTiles,
        PlayerHandDetails botHand,
        int tilesLeftInBag,
        GameLanguage language)
    {
        var waitSeconds = _random.Next(WaitRangeSeconds.From, WaitRangeSeconds.To);
        
        await Task.Delay(waitSeconds * 1000);

        var start = Stopwatch.GetTimestamp();

        if (placedTiles.Count == 0)
        {
            return new SkipTurnAction();
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

        var filteredMoves = validMoves.Where(m => 
        {
            return m.Placements.Count <= MaxWordLength; 
        }).ToList();

        if (filteredMoves.Any())
        {
            var selectedMove = filteredMoves[_random.Next(filteredMoves.Count)];
            
            var moveRequest = new MoveRequestModel(
                selectedMove.Placements.Select(p => new TilePlacementModel(
                    p.TileId, 
                    p.CellId, 
                    p.SelectedValueId
                )).ToList()
            );
            
            var elapsed = Stopwatch.GetElapsedTime(start);
            logger.LogDebug("Easy bot move calculation took {ElapsedMilliseconds} ms.", elapsed.TotalMilliseconds);

            return new MakeMoveAction(moveRequest);
        }

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
}