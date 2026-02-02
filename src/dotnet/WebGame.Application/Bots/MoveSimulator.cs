using System;
using System.Collections.Generic;
using System.Linq;
using WebGame.Domain.Interfaces.Bots;
using WebGame.Domain.Interfaces.Games.Details;
using WebGame.Domain.Interfaces.Games.Enums;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies.Enums;
using WebGame.Domain.Structures;

namespace WebGame.Application.Bots;

public class MoveSimulator(IGameLanguageProviderFactory languageProviderFactory) : IMoveSimulator
{
    public List<SimulatedMove> SimulateMoves(
        GameLanguage language,
        int maxLetters,
        BoardLayoutDetails boardLayout,
        List<PlacedTileDetails> placedTiles,
        List<TileInstanceDetails> availableTiles)
    {
        var languageProvider = languageProviderFactory.CreateProvider(language);
        var trie = languageProvider.GetWordTrie();
        var tileDefinitions = languageProvider.GetTileDefinitions();
        var tileDefMap = tileDefinitions.ToDictionary(x => x.ValueId, x => x);

        var boardMap = new char[boardLayout.Width, boardLayout.Height];
        var cellMap = new BoardCellDetails[boardLayout.Width, boardLayout.Height];

        foreach (var cell in boardLayout.Cells)
        {
            cellMap[cell.X, cell.Y] = cell;
            boardMap[cell.X, cell.Y] = '\0';
        }

        foreach (var tile in placedTiles)
        {
            var cell = boardLayout.Cells.First(c => c.Id == tile.CellId);
            var defId = tile.SelectedValueId ?? tile.ValueId;
            if (tileDefMap.TryGetValue(defId, out var def))
            {
                boardMap[cell.X, cell.Y] = def.ValueText[0];
            }
        }

        var isFirstTurn = placedTiles.Count == 0;

        var context = new SimulationContext(
            boardLayout,
            cellMap,
            boardMap,
            trie,
            tileDefMap,
            null!, false, maxLetters,
            isFirstTurn
        );

        var moves = new List<SimulatedMove>();

        moves.AddRange(FindMoves(context with { IsTranspose = false }, availableTiles));

        moves.AddRange(FindMoves(context with { IsTranspose = true }, availableTiles));

        return moves.DistinctBy(m => 
            string.Join("|", m.Placements.OrderBy(p => p.CellId).Select(p => $"{p.CellId}-{p.TileId}")))
            .ToList();
    }

    private List<SimulatedMove> FindMoves(SimulationContext context, List<TileInstanceDetails> rack)
    {
        var foundMoves = new List<SimulatedMove>();

        var crossChecks = ComputeCrossChecks(context);
        var passContext = context with { CrossChecks = crossChecks };

        int rows = context.IsTranspose ? context.BoardLayout.Width : context.BoardLayout.Height;
        int cols = context.IsTranspose ? context.BoardLayout.Height : context.BoardLayout.Width;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (c > 0 && GetChar(c - 1, r, passContext) != '\0') continue;

                ExtendRight(
                    partialWord: "", 
                    node: passContext.Trie.Root, 
                    col: c, 
                    row: r, 
                    rack: rack, 
                    placed: new List<SimulatedMoveLetter>(), 
                    context: passContext, 
                    results: foundMoves
                );
            }
        }
        
        return foundMoves;
    }

    private void ExtendRight(
        string partialWord,
        TrieNode node,
        int col,
        int row,
        List<TileInstanceDetails> rack,
        List<SimulatedMoveLetter> placed,
        SimulationContext context,
        List<SimulatedMove> results)
    {
        int cols = context.IsTranspose ? context.BoardLayout.Height : context.BoardLayout.Width;

        if (node.IsEndOfWord && placed.Count > 0)
        {
            if (col >= cols || GetChar(col, row, context) == '\0')
            {
                if (IsConnected(placed, context))
                {
                     results.Add(new SimulatedMove(new List<SimulatedMoveLetter>(placed)));
                }
            }
        }
        
        if (col >= cols) return;

        char existingChar = GetChar(col, row, context);
        if (existingChar != '\0')
        {
            if (node.Children.TryGetValue(char.ToLower(existingChar), out var nextNode))
            {
                ExtendRight(partialWord + existingChar, nextNode, col + 1, row, rack, placed, context, results);
            }
        }
        else
        {
            if (placed.Count >= context.MaxLetters) return;

            foreach (var child in node.Children)
            {
                char letter = child.Key;

                if (!context.CrossChecks[col, row].Contains(letter)) continue;

                var tile = FindInRack(rack, letter, context.TileDefs);
                if (tile != null)
                {
                    var nextRack = new List<TileInstanceDetails>(rack);
                    nextRack.Remove(tile);
                    
                    var cell = GetCell(col, row, context);
                    
                    Guid? selectedVal = null;
                    if (context.TileDefs[tile.ValueId].ValueText == "?")
                    {
                        var letterDef = context.TileDefs.Values.FirstOrDefault(d => d.ValueText.Equals(letter.ToString(), StringComparison.OrdinalIgnoreCase));
                        if(letterDef != null) selectedVal = letterDef.ValueId;
                    }

                    var newPlaced = new List<SimulatedMoveLetter>(placed)
                    {
                        new SimulatedMoveLetter(tile.TileId, cell.Id, selectedVal)
                    };
                    
                    ExtendRight(partialWord + letter, child.Value, col + 1, row, nextRack, newPlaced, context, results);
                }
            }
        }
    }

    private static TileInstanceDetails? FindInRack(List<TileInstanceDetails> rack, char letter, Dictionary<Guid, LetterTileItem> tileDefs)
    {
        var exact = rack.FirstOrDefault(t => 
            tileDefs[t.ValueId].ValueText.Equals(letter.ToString(), StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        
        var blank = rack.FirstOrDefault(t => tileDefs[t.ValueId].ValueText == "?");
        return blank;
    }

    private static HashSet<char>[,] ComputeCrossChecks(SimulationContext context)
    {
        int rows = context.IsTranspose ? context.BoardLayout.Width : context.BoardLayout.Height;
        int cols = context.IsTranspose ? context.BoardLayout.Height : context.BoardLayout.Width;
        
        var checks = new HashSet<char>[cols, rows];
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                checks[c, r] = new HashSet<char>();
                
                if (GetChar(c, r, context) != '\0') 
                {
                    continue; 
                }

                if (!HasVerticalNeighbor(c, r, context))
                {
                    for(char l = 'a'; l <= 'z'; l++) checks[c, r].Add(l);
                    continue;
                }
                
                int top = r - 1;
                while (top >= 0 && GetChar(c, top, context) != '\0') top--;
                top++; 
                
                int bottom = r + 1;
                while (bottom < rows && GetChar(c, bottom, context) != '\0') bottom++;
                bottom--; 

                string prefix = "";
                for (int k = top; k < r; k++) prefix += GetChar(c, k, context);
                
                string suffix = "";
                for (int k = r + 1; k <= bottom; k++) suffix += GetChar(c, k, context);

                for (char l = 'a'; l <= 'z'; l++)
                {
                    string word = prefix + l + suffix;
                    if (context.Trie.IsValid(word))
                    {
                        checks[c, r].Add(l);
                    }
                }
            }
        }
        return checks;
    }

    private static bool HasVerticalNeighbor(int c, int r, SimulationContext context)
    {
        int rows = context.IsTranspose ? context.BoardLayout.Width : context.BoardLayout.Height;
        if (r > 0 && GetChar(c, r - 1, context) != '\0') return true;
        if (r < rows - 1 && GetChar(c, r + 1, context) != '\0') return true;
        return false;
    }
    
    private static char GetChar(int c, int r, SimulationContext context)
    {
        if (context.IsTranspose) return context.BoardMap[r, c]; 
        return context.BoardMap[c, r];
    }
    
    private static BoardCellDetails GetCell(int c, int r, SimulationContext context)
    {
        if (context.IsTranspose) return context.CellMap[r, c];
        return context.CellMap[c, r];
    }

    private static bool IsConnected(List<SimulatedMoveLetter> placed, SimulationContext context)
    {
        if (context.IsFirstTurn)
        {
             foreach (var p in placed)
             {
                 var cell = context.BoardLayout.Cells.First(c => c.Id == p.CellId);
                 if (cell.Type == LetterCellType.Center) return true;
             }
             return false;
        }
        
        foreach (var p in placed)
        {
            var cell = context.BoardLayout.Cells.First(c => c.Id == p.CellId);
            int x = cell.X;
            int y = cell.Y;
            
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (nx >= 0 && nx < context.BoardLayout.Width && ny >= 0 && ny < context.BoardLayout.Height)
                {
                    if (context.BoardMap[nx, ny] != '\0') return true;
                }
            }
        }
        
        return false;
    }

    private record SimulationContext(
        BoardLayoutDetails BoardLayout,
        BoardCellDetails[,] CellMap,
        char[,] BoardMap,
        WordTrie Trie,
        Dictionary<Guid, LetterTileItem> TileDefs,
        HashSet<char>[,] CrossChecks,
        bool IsTranspose,
        int MaxLetters,
        bool IsFirstTurn
    );
}