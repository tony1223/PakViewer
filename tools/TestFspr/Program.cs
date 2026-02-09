using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Sprite;

class TestFspr
{
    static void Main(string[] args)
    {
        string folder = @"C:\workspaces\lineage\PakViewer\resources\sprs";
        var files = Directory.GetFiles(folder, "*.spr");

        long totalOriginal = 0;
        long totalCompressed = 0;
        int count = 0;
        int errors = 0;

        Console.WriteLine($"Testing {files.Length} SPR files...\n");

        foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
        {
            try
            {
                byte[] original = File.ReadAllBytes(file);
                byte[] fspr = FsprWriter.Convert(original);

                totalOriginal += original.Length;
                totalCompressed += fspr.Length;
                count++;

                double ratio = (double)fspr.Length / original.Length * 100;
                string name = Path.GetFileName(file);

                if (count <= 10 || count % 20 == 0 || original.Length > 1000000)
                {
                    Console.WriteLine($"{name,-20} {original.Length,10:N0} -> {fspr.Length,10:N0}  ({ratio:F1}%)");
                }
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"ERROR: {Path.GetFileName(file)} - {ex.Message}");
            }
        }

        Console.WriteLine($"\n{'=',-50}");
        Console.WriteLine($"Total files:      {count}");
        Console.WriteLine($"Errors:           {errors}");
        Console.WriteLine($"Original size:    {totalOriginal:N0} bytes ({totalOriginal / 1024.0 / 1024.0:F2} MB)");
        Console.WriteLine($"Compressed size:  {totalCompressed:N0} bytes ({totalCompressed / 1024.0 / 1024.0:F2} MB)");
        Console.WriteLine($"Compression ratio: {(double)totalCompressed / totalOriginal * 100:F1}%");
        Console.WriteLine($"Space saved:       {(1 - (double)totalCompressed / totalOriginal) * 100:F1}%");
    }
}
