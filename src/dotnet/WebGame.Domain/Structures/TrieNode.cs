namespace WebGame.Domain.Structures;

public class TrieNode
{
    public Dictionary<char, TrieNode> Children = new();
    public bool IsEndOfWord;
}

public class WordTrie
{
    private readonly TrieNode _root = new();

    public TrieNode Root => _root;

    public void Insert(string word)
    {
        var current = _root;
        foreach (var c in word.ToLower())
        {
            if (!current.Children.ContainsKey(c))
                current.Children[c] = new TrieNode();
            current = current.Children[c];
        }
        current.IsEndOfWord = true;
    }

    public bool IsValid(string word)
    {
        var current = _root;
        foreach (var c in word.ToLower())
        {
            if (!current.Children.TryGetValue(c, out var next))
                return false;
            current = next;
        }
        return current.IsEndOfWord;
    }
}