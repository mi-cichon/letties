using System.IO.Compression;
using WebGame.Domain.Structures;

namespace WebGame.Application.Languages.Assets;

public static class PolishLanguageAssets
{
    public static readonly WordTrie Dictionary = new();
    private const string FilePath = "Languages/Assets/PolishDictionary.txt.gz";
    
    static PolishLanguageAssets()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePath);
        using var fileStream = File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        while (reader.ReadLine() is { } word)
        {
            Dictionary.Insert(word);
        }
    }
    
    public static readonly (Guid Id, string Text, int Points, double Weight)[] TileDefinitions =
    [
        (new Guid("99e98f32-ab86-4627-a089-caf0b879bbdf"), "A", 1, 9.0),
        (new Guid("b4196598-5308-4438-8d2a-8b9f40d79bee"), "Ą", 5, 1.0),
        (new Guid("90c09072-9729-48e8-9cb0-9080f69793be"), "B", 3, 2.0),
        (new Guid("9c90eaf2-02ad-4748-b05b-e0de17303023"), "C", 2, 3.0),
        (new Guid("e8bad0e0-8cf3-4d0d-a553-55c115df6c75"), "Ć", 6, 1.0),
        (new Guid("76fef8f8-7833-4b76-beea-086edc3010fd"), "D", 2, 3.0),
        (new Guid("9026b17f-1ee3-4cf3-8830-77dcbe3e0d24"), "E", 1, 7.0),
        (new Guid("d9df0cdb-199a-4a98-9c89-bde59cb8ee74"), "Ę", 5, 1.0),
        (new Guid("aac3d826-46d8-496e-846a-5e881928b2ef"), "F", 5, 1.0),
        (new Guid("30d4127b-f3d1-4560-a315-39fb8debf371"), "G", 3, 2.0),
        (new Guid("230f1213-4eb1-4b0e-9f38-fa6af6ef5d22"), "H", 3, 2.0),
        (new Guid("f471e3e1-accc-4fad-be9c-9fe54cd94213"), "I", 1, 8.0),
        (new Guid("5bad2e0e-79e3-4058-8bad-8e68498ddbe3"), "J", 3, 2.0),
        (new Guid("307952a8-67f9-4863-8107-98d9d2387d4b"), "K", 2, 3.0),
        (new Guid("e87bc517-c36e-46c4-908f-c4e0e22627e0"), "L", 2, 3.0),
        (new Guid("bceffebb-be75-4aff-9d18-2652136270cb"), "Ł", 3, 2.0),
        (new Guid("00d1ce06-e9ab-41bc-b0c3-87dc6c344fa8"), "M", 2, 3.0),
        (new Guid("770a22fa-549c-487a-98b4-89337547d168"), "N", 1, 5.0),
        (new Guid("3bc75f88-dbf1-4303-910c-4d1eaa515fad"), "Ń", 7, 1.0),
        (new Guid("055dee27-7418-4b83-856f-a6e80e99c714"), "O", 1, 6.0),
        (new Guid("bf781173-746e-42a6-bd24-e288a7d5b065"), "Ó", 5, 1.0),
        (new Guid("d2213dd8-38f1-4bed-bbf9-fc07fee9cff2"), "P", 2, 3.0),
        (new Guid("a39b463e-1d6e-4b59-bf79-430d3a98f54c"), "R", 1, 4.0),
        (new Guid("5f30bed6-5c85-4a1b-b657-7df338067d64"), "S", 1, 4.0),
        (new Guid("2fd2650c-8458-4da7-ae1c-29d42010d237"), "Ś", 5, 1.0),
        (new Guid("9e6b669b-c297-4840-b875-92ccfa87df5e"), "T", 2, 3.0),
        (new Guid("66ae5a75-6ebf-4293-8c70-bbea07b61c0b"), "U", 3, 2.0),
        (new Guid("b4401644-ab09-422b-838a-18b16bbcbef1"), "W", 1, 4.0),
        (new Guid("64819ea8-3de7-4651-8286-5692b632db93"), "Y", 2, 4.0),
        (new Guid("b47204fd-b573-4c37-9d85-3921c4bb052f"), "Z", 1, 5.0),
        (new Guid("3ce8ffc3-84a3-424c-a81a-720df5ed4c2b"), "Ź", 9, 1.0),
        (new Guid("4a69da3d-e0e5-4a68-bf48-d4ecbf00844b"), "Ż", 5, 1.0),
        (new Guid("1ee3e0c0-bd65-48e4-b6a7-86f1a2358d99"), "?", 0, 2.0)
    ];
}