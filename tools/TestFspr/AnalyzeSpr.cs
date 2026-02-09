using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

class AnalyzeSpr
{
    static void Main(string[] args)
    {
        string folder = @"C:\workspaces\lineage\PakViewer\resources\sprs";
        var files = Directory.GetFiles(folder, "*.spr");

        long totalOriginal = 0;
        long totalLz4 = 0;
        long totalBrotli = 0;
        long totalZlib = 0;
        long totalWebp = 0;
        int count = 0;
        int errors = 0;

        Console.WriteLine($"分析 {files.Length} 個 SPR 檔案 (平行處理)...\n");

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
        {
            try
            {
                byte[] sprData = File.ReadAllBytes(file);

                // LZ4
                byte[] lz4Data = FsprWriter.Convert(sprData);

                // Brotli
                long brotliSize;
                using (var ms = new MemoryStream())
                {
                    using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        brotli.Write(sprData, 0, sprData.Length);
                    }
                    brotliSize = ms.Length;
                }

                // Zlib (Deflate)
                long zlibSize;
                using (var ms = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        deflate.Write(sprData, 0, sprData.Length);
                    }
                    zlibSize = ms.Length;
                }

                // WebP lossless
                var frames = SprReader.Load(sprData);
                long webpSize = 0;
                foreach (var f in frames)
                {
                    if (f.Image == null) continue;
                    using var wms = new MemoryStream();
                    f.Image.SaveAsWebp(wms, new WebpEncoder { Quality = 100, FileFormat = WebpFileFormatType.Lossless });
                    webpSize += wms.Length;
                }
                foreach (var f in frames) f.Image?.Dispose();

                Interlocked.Add(ref totalOriginal, sprData.Length);
                Interlocked.Add(ref totalLz4, lz4Data.Length);
                Interlocked.Add(ref totalBrotli, brotliSize);
                Interlocked.Add(ref totalZlib, zlibSize);
                Interlocked.Add(ref totalWebp, webpSize);
                int c = Interlocked.Increment(ref count);

                if (c % 20 == 0)
                {
                    Console.WriteLine($"已處理 {c} 檔案...");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                Console.WriteLine($"ERROR: {Path.GetFileName(file)} - {ex.Message}");
            }
        });

        Console.WriteLine($"\n=== 全部 {count} 個 SPR 檔案壓縮比較 ===");
        if (errors > 0) Console.WriteLine($"(有 {errors} 個檔案處理失敗)");
        Console.WriteLine($"{"方案",-25} {"大小 (MB)",15} {"壓縮率",10}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"原始 SPR",-25} {totalOriginal / 1024.0 / 1024.0,15:F2} {"100%",10}");
        Console.WriteLine($"{"LZ4 (FSPR)",-25} {totalLz4 / 1024.0 / 1024.0,15:F2} {(double)totalLz4 / totalOriginal * 100,9:F1}%");
        Console.WriteLine($"{"Brotli",-25} {totalBrotli / 1024.0 / 1024.0,15:F2} {(double)totalBrotli / totalOriginal * 100,9:F1}%");
        Console.WriteLine($"{"Zlib/Deflate",-25} {totalZlib / 1024.0 / 1024.0,15:F2} {(double)totalZlib / totalOriginal * 100,9:F1}%");
        Console.WriteLine($"{"WebP lossless",-25} {totalWebp / 1024.0 / 1024.0,15:F2} {(double)totalWebp / totalOriginal * 100,9:F1}%");

        Console.WriteLine($"\n節省空間:");
        Console.WriteLine($"  LZ4:     {(totalOriginal - totalLz4) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  Brotli:  {(totalOriginal - totalBrotli) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  Zlib:    {(totalOriginal - totalZlib) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  WebP:    {(totalOriginal - totalWebp) / 1024.0 / 1024.0:F2} MB");
    }
}
