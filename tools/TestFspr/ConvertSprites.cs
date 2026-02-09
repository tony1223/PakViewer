using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;

class ConvertSprites
{
    static void Main(string[] args)
    {
        string inputFolder = @"C:\workspaces\lineage\v381\client_815_1705042503";
        string outputFolder = Path.Combine(inputFolder, "_compressed");

        // 找出所有 sprite*.idx 檔案
        var spriteFiles = Directory.GetFiles(inputFolder, "sprite*.idx")
            .OrderBy(f => f)
            .ToList();

        Console.WriteLine($"找到 {spriteFiles.Count} 個 sprite IDX 檔案");
        Console.WriteLine($"輸出資料夾: {outputFolder}\n");

        if (spriteFiles.Count == 0)
        {
            Console.WriteLine("沒有找到任何 sprite*.idx 檔案");
            return;
        }

        // 列出檔案
        foreach (var f in spriteFiles)
        {
            var info = new FileInfo(f);
            var pakInfo = new FileInfo(Path.ChangeExtension(f, ".pak"));
            long totalSize = info.Length + (pakInfo.Exists ? pakInfo.Length : 0);
            Console.WriteLine($"  {Path.GetFileName(f)}: {totalSize / 1024.0 / 1024.0:F2} MB");
        }

        Console.WriteLine($"\n開始轉換...\n");

        var (totalOrig, totalComp, success, failed) = PakFile.ConvertToIdxFormatBatch(
            spriteFiles,
            outputFolder,
            maxParallelism: 0, // 使用所有 CPU 核心
            progress: (done, total, file, orig, comp) =>
            {
                double ratio = orig > 0 ? (double)comp / orig * 100 : 0;
                Console.WriteLine($"[{done}/{total}] {file}: {orig / 1024.0 / 1024.0:F2} MB -> {comp / 1024.0 / 1024.0:F2} MB ({ratio:F1}%)");
            }
        );

        Console.WriteLine($"\n=== 轉換完成 ===");
        Console.WriteLine($"成功: {success}, 失敗: {failed}");
        Console.WriteLine($"原始大小: {totalOrig / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"壓縮後: {totalComp / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"壓縮率: {(double)totalComp / totalOrig * 100:F1}%");
        Console.WriteLine($"節省空間: {(totalOrig - totalComp) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"\n輸出位置: {outputFolder}");
    }
}
