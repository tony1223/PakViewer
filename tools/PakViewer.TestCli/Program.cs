// PakViewer.TestCli - 掃描 loose TIL 檔案完整性
using System.Text;
using Lin.Helper.Core.Tile;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

string tileDir = @"C:\workspaces\lineage\v381\client_paktest_m_map\Tile\Tile";
var tilFiles = Directory.GetFiles(tileDir, "*.til");
Console.WriteLine($"Total TIL files: {tilFiles.Length}");

int pass = 0, fail = 0;
var failures = new List<(string file, string reason)>();

foreach (var f in tilFiles)
{
    try
    {
        var data = File.ReadAllBytes(f);
        var tb = L1Til.ParseToTileBlocks(data);
        if (tb == null)
        {
            fail++;
            failures.Add((Path.GetFileName(f), "returned null"));
        }
        else
            pass++;
    }
    catch (InvalidDataException ex)
    {
        fail++;
        failures.Add((Path.GetFileName(f), ex.Message));
    }
    catch (Exception ex)
    {
        fail++;
        failures.Add((Path.GetFileName(f), $"{ex.GetType().Name}: {ex.Message}"));
    }
}

Console.WriteLine($"PASS: {pass}, FAIL: {fail}");
if (failures.Count > 0)
{
    Console.WriteLine("\nFailures:");
    foreach (var (file, reason) in failures)
        Console.WriteLine($"  {file}: {reason}");
}
