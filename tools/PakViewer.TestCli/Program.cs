// PakViewer.TestCli - 測試用 scratch pad
// 用法: dotnet run --project tools/PakViewer.TestCli
//
// 這個專案不在 Solution 中，不會被 CI/CD 編譯
// 可以隨時修改 Program.cs 來測試 Lin.Helper.Core 的功能

using System.Text;
using Lin.Helper.Core.Pak;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("=== PakViewer TestCli — V880 IDX Handler Test ===\n");

string clientDir = @"C:\workspaces\lineage\v381\client_880_1901142505";

// 收集所有 .idx 檔案
var idxFiles = Directory.GetFiles(clientDir, "*.idx")
    .Where(f => !f.EndsWith(".decrypted", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToArray();

Console.WriteLine($"Found {idxFiles.Length} IDX files\n");

int totalFiles = 0;
int successCount = 0;

foreach (var idxPath in idxFiles)
{
    string name = Path.GetFileName(idxPath);
    try
    {
        using var pak = new PakFile(idxPath);
        Console.WriteLine($"[OK] {name,-20} format={pak.EncryptionType,-10} entries={pak.Count,6}  protected={pak.IsProtected}");
        totalFiles += pak.Count;
        successCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name,-20} {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine($"\n--- Summary: {successCount}/{idxFiles.Length} IDX files loaded, {totalFiles} total entries ---\n");

// 深度測試: Text.idx (DES + Brotli)
string textIdx = Path.Combine(clientDir, "Text.idx");
if (File.Exists(textIdx))
{
    Console.WriteLine("=== Text.idx deep test (Ext+DES, Brotli) ===\n");
    using var textPak = new PakFile(textIdx);

    // 印出前 10 個 entries
    int showCount = Math.Min(10, textPak.Count);
    for (int i = 0; i < showCount; i++)
    {
        var rec = textPak.Files[i];
        Console.WriteLine($"  [{i}] {rec}");
    }
    if (textPak.Count > showCount)
        Console.WriteLine($"  ... ({textPak.Count - showCount} more)");

    // 嘗試解壓幾個檔案
    Console.WriteLine();
    int extractCount = Math.Min(3, textPak.Count);
    for (int i = 0; i < extractCount; i++)
    {
        var rec = textPak.Files[i];
        try
        {
            byte[] data = textPak.Extract(i);
            string preview = Encoding.UTF8.GetString(data, 0, Math.Min(80, data.Length))
                .Replace("\r", "").Replace("\n", "\\n");
            Console.WriteLine($"  Extract [{i}] {rec.FileName}: {data.Length} bytes");
            Console.WriteLine($"    preview: {preview}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Extract [{i}] {rec.FileName}: FAILED — {ex.Message}");
        }
    }
}

// 深度測試: Tile.idx (Ext, 非加密)
string tileIdx = Path.Combine(clientDir, "Tile.idx");
if (File.Exists(tileIdx))
{
    Console.WriteLine($"\n=== Tile.idx deep test (Ext, no encryption) ===\n");
    using var tilePak = new PakFile(tileIdx);

    int showCount = Math.Min(5, tilePak.Count);
    for (int i = 0; i < showCount; i++)
    {
        var rec = tilePak.Files[i];
        Console.WriteLine($"  [{i}] {rec}");
    }

    // 嘗試解壓一個檔案
    if (tilePak.Count > 0)
    {
        var rec = tilePak.Files[0];
        try
        {
            byte[] data = tilePak.Extract(0);
            Console.WriteLine($"\n  Extract [0] {rec.FileName}: {data.Length} bytes (expected {rec.FileSize})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n  Extract [0] {rec.FileName}: FAILED — {ex.Message}");
        }
    }
}

Console.WriteLine("\n=== Done ===");
