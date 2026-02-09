using System;
using Lin.Helper.Core.Pak;

class TestLoad
{
    static void Main()
    {
        string[] files = {
            @"C:\workspaces\lineage\v381\client_815_1705042503\Sprite10.idx",
            @"C:\workspaces\lineage\v381\client_815_1705042503\Sprite11.idx",
            @"C:\workspaces\lineage\v381\client_815_1705042503\Sprite12.idx"
        };
        
        foreach (var file in files)
        {
            Console.WriteLine($"\n=== {System.IO.Path.GetFileName(file)} ===");
            try
            {
                using var pak = new PakFile(file);
                Console.WriteLine($"格式: {pak.EncryptionType}");
                Console.WriteLine($"檔案數: {pak.Count}");
                if (pak.Count > 0)
                {
                    Console.WriteLine($"第一筆: {pak.Files[0].FileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }
}
