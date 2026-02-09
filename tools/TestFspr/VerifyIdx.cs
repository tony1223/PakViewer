using System;
using System.IO;
using Lin.Helper.Core.Pak;

class VerifyIdx
{
    static void Main()
    {
        string idxPath = @"C:\workspaces\lineage\v381\client_815_1705042503\_compressed\Sprite.idx";
        
        Console.WriteLine($"載入: {idxPath}");
        
        try
        {
            using var pak = new PakFile(idxPath);
            Console.WriteLine($"格式: {pak.EncryptionType}");
            Console.WriteLine($"檔案數: {pak.Count}");
            
            // 嘗試解壓縮前幾個檔案
            Console.WriteLine("\n測試解壓縮:");
            for (int i = 0; i < Math.Min(5, pak.Count); i++)
            {
                var rec = pak.Files[i];
                try
                {
                    byte[] data = pak.Extract(i);
                    Console.WriteLine($"  [{i}] {rec.FileName}: {rec.FileSize} bytes (compressed: {rec.CompressedSize}) -> extracted: {data.Length} bytes OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [{i}] {rec.FileName}: FAILED - {ex.Message}");
                }
            }
            
            // 測試一個 SPR 檔案
            int sprIndex = -1;
            for (int i = 0; i < pak.Count; i++)
            {
                if (pak.Files[i].FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                {
                    sprIndex = i;
                    break;
                }
            }
            
            if (sprIndex >= 0)
            {
                var rec = pak.Files[sprIndex];
                Console.WriteLine($"\n測試 SPR 檔案: {rec.FileName}");
                byte[] sprData = pak.Extract(sprIndex);
                Console.WriteLine($"  解壓後大小: {sprData.Length} bytes");
                Console.WriteLine($"  前 16 bytes: {BitConverter.ToString(sprData, 0, Math.Min(16, sprData.Length))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
