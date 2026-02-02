using System.IO.Compression;
using WebGame.Domain.Structures;

namespace WebGame.Application.Languages.Assets;

public static class EnglishLanguageAssets
{
    public static readonly WordTrie Dictionary = new();
    private const string FilePath = "Languages/Assets/EnglishDictionary.txt.gz";
    
    static EnglishLanguageAssets()
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
        (new Guid("b946c91f-99b8-45d0-971a-0e044b61014e"), "A", 1, 9.0),
        (new Guid("2f95fd35-ff0b-4582-8892-4ed26bb6b3ec"), "B", 3, 2.0),
        (new Guid("52950b6b-9e0d-4205-aafa-26e999ae74b9"), "C", 3, 2.0),
        (new Guid("fb5b6846-efa0-4d3f-922f-227113a1bfcc"), "D", 2, 4.0),
        (new Guid("a0e47f41-13f3-4da2-bdfc-bca56c1b89c7"), "E", 1, 12.0),
        (new Guid("6dd34fc1-e7f7-44c1-b26d-8ba875a5b43b"), "F", 4, 2.0),
        (new Guid("2ea63761-f8ad-4700-95d2-a79bf955442c"), "G", 2, 3.0),
        (new Guid("eae13059-0cb2-4379-b311-552488c856e0"), "H", 4, 2.0),
        (new Guid("52e1fef3-9852-4d69-93ce-9baa082ac502"), "I", 1, 9.0),
        (new Guid("c40d48a0-8cbd-4385-a795-1d2a03502c4d"), "J", 8, 1.0),
        (new Guid("9204ea54-3db3-460e-818a-6b38832efc9d"), "K", 5, 1.0),
        (new Guid("4f7e2564-fa65-4b9b-a1a7-3747b5fab408"), "L", 1, 4.0),
        (new Guid("ca9947d0-2029-46a5-ad1a-e88d8ba2c1ea"), "M", 3, 2.0),
        (new Guid("10edb30a-47be-4f4a-9223-32650dac1d12"), "N", 1, 6.0),
        (new Guid("c808e80f-c58a-4167-bb08-40f71e04d79c"), "O", 1, 8.0),
        (new Guid("060ba29d-4531-41e1-bdc3-fd4a97522be9"), "P", 3, 2.0),
        (new Guid("25aa3af1-ccb6-4b9f-8112-4d528563bb9e"), "Q", 10, 1.0),
        (new Guid("5954f7ba-05b3-4c6e-83a4-50a0ac014dff"), "R", 1, 6.0),
        (new Guid("6f3ad915-aa1e-441e-af30-bb9a8c123781"), "S", 1, 4.0),
        (new Guid("c853a277-41e3-4569-8232-9feacfcded58"), "T", 1, 6.0),
        (new Guid("52bc5c1a-a12a-46c9-9823-4ab3904dc040"), "U", 1, 4.0),
        (new Guid("79b9eef4-cfc9-40d1-a635-00c6c0c8e716"), "V", 4, 2.0),
        (new Guid("a986ed6e-32b9-4d8b-a7f3-b3ea50148160"), "W", 4, 2.0),
        (new Guid("5b6e0435-5695-4260-9c15-3768a8dda4cf"), "X", 8, 1.0),
        (new Guid("9fe09a85-a1dc-40b7-9183-b347cb88fd7b"), "Y", 4, 2.0),
        (new Guid("d4d73fb5-03cb-4766-bcda-1788540dd42e"), "Z", 10, 1.0),
        (new Guid("4c81bb6b-8d2a-4a12-9e97-78829cc147dc"), "?", 0, 2.0)
    ];
}