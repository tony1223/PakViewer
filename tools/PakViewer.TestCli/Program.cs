// PakViewer.TestCli - 測試用 scratch pad
// 用法: dotnet run --project tools/PakViewer.TestCli
//
// 這個專案不在 Solution 中，不會被 CI/CD 編譯
// 可以隨時修改 Program.cs 來測試 Lin.Helper.Core 的功能

using System.Text;
using Lin.Helper.Core.Lcx;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("=== PakViewer TestCli — LCX Read Test ===\n");

// LCX keys
byte[][] keys = new byte[][]
{
    Convert.FromHexString("9f39503300b36971b3575c1f02a3461e2f0eb1a90b66163c6f71b4f222dbc9e0"), // dynamic
    Convert.FromHexString("cde6d20c931f4fd52d7dfefcf32d4c62607ee70c68efc6744cede25a450d7c2f"), // static
};

string packDir = @"C:\workspaces\lineage\logins\classic\Lineage Classic\pack";
var lcxFiles = Directory.GetFiles(packDir, "*.lcx").OrderBy(f => f).ToArray();
Console.WriteLine($"Found {lcxFiles.Length} LCX files in {packDir}\n");

int totalEntries = 0;
int successCount = 0;

foreach (var lcxPath in lcxFiles)
{
    string name = Path.GetFileName(lcxPath);
    try
    {
        using var lcx = new LcxFile(lcxPath, keys);
        Console.WriteLine($"[OK] {name,-15} entries={lcx.Count,6}");
        totalEntries += lcx.Count;
        successCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name,-15} {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine($"\n--- Summary: {successCount}/{lcxFiles.Length} LCX files loaded, {totalEntries} total entries ---\n");

// 深度測試: data_u.lcx (最小的，250 entries)
string testLcx = Path.Combine(packDir, "data_u.lcx");
if (File.Exists(testLcx))
{
    Console.WriteLine("=== data_u.lcx deep test ===\n");
    using var lcx = new LcxFile(testLcx, keys);

    // 印出前 10 個 entries
    int showCount = Math.Min(10, lcx.Count);
    for (int i = 0; i < showCount; i++)
    {
        var entry = lcx.Entries[i];
        Console.WriteLine($"  [{i}] {entry.FileName} (zip={entry.CompressedSize} bytes)");
    }
    if (lcx.Count > showCount)
        Console.WriteLine($"  ... ({lcx.Count - showCount} more)");

    // 嘗試提取幾個檔案
    Console.WriteLine();
    int extractCount = Math.Min(5, lcx.Count);
    for (int i = 0; i < extractCount; i++)
    {
        var entry = lcx.Entries[i];
        try
        {
            byte[] data = lcx.Extract(i);
            string ext = Path.GetExtension(entry.FileName).ToLowerInvariant();
            string info = (ext == ".txt" || ext == ".xml" || ext == ".json" || ext == ".html")
                ? Encoding.UTF8.GetString(data, 0, Math.Min(60, data.Length)).Replace("\r", "").Replace("\n", "\\n")
                : $"[binary {data.Length} bytes, header: {BitConverter.ToString(data, 0, Math.Min(8, data.Length))}]";
            Console.WriteLine($"  Extract [{i}] {entry.FileName}: {data.Length} bytes");
            Console.WriteLine($"    {info}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Extract [{i}] {entry.FileName}: FAILED — {ex.Message}");
        }
    }
}

Console.WriteLine("\n=== Done ===");
