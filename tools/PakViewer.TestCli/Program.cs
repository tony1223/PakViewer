// PakViewer.TestCli - 測試用 scratch pad
using System.Text;
using Lin.Helper.Core.Sprite;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("=== SprListParser — All resources/*.txt Test ===\n");

string resourceDir = @"C:\workspaces\lineage\PakViewer\resources";
var txtFiles = Directory.GetFiles(resourceDir, "*.txt").OrderBy(f => f).ToArray();

Console.WriteLine($"Found {txtFiles.Length} .txt files\n");

foreach (var path in txtFiles)
{
    string name = Path.GetFileName(path);
    try
    {
        var sprList = SprListParser.LoadFromFile(path);
        var status = sprList.Warnings.Count == 0 ? "OK" : $"WARN({sprList.Warnings.Count})";
        Console.WriteLine($"[{status,-8}] {name,-35} entries={sprList.Entries.Count,6}  header={sprList.TotalEntries}");

        foreach (var w in sprList.Warnings)
        {
            Console.WriteLine($"           {w}");
        }

        // 驗證 #0 是否正確
        var e0 = sprList.Entries.FirstOrDefault(e => e.Id == 0);
        if (e0 != null)
        {
            Console.WriteLine($"           #0: ImageCount={e0.ImageCount} LinkedId={e0.LinkedId} Actions={e0.Actions.Count} Attrs={e0.Attributes.Count}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL    ] {name,-35} {ex.GetType().Name}: {ex.Message}");
    }
    Console.WriteLine();
}

Console.WriteLine("=== Done ===");
