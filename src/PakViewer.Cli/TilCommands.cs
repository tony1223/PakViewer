using System;
using System.IO;
using Lin.Helper.Core.Tile;
using Lin.Helper.Core.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace PakViewer.Cli
{
    internal static class TilCommands
    {
        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var command = args[0].ToLowerInvariant();
            var subArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

            return command switch
            {
                "info" => Info(subArgs),
                "export" => Export(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int Info(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli til info <til-file>"); return 1; }

            var filePath = args[0];
            var data = File.ReadAllBytes(filePath);

            var version = L1Til.GetVersion(data);
            var compression = L1Til.DetectCompression(data);

            byte[] decompressed = compression != L1Til.CompressionType.None
                ? L1Til.Decompress(data, compression)
                : data;

            var tileBlocks = L1Til.ParseToTileBlocks(decompressed, false);
            int tileSize = L1Til.GetTileSize(decompressed);

            Console.WriteLine($"TIL File:     {Path.GetFileName(filePath)}");
            Console.WriteLine($"File Size:    {data.Length:N0} bytes");
            Console.WriteLine($"Version:      {version}");
            Console.WriteLine($"Compression:  {compression}");
            Console.WriteLine($"Tile Pixels:  {tileSize}x{tileSize}");
            Console.WriteLine($"Block Count:  {tileBlocks.Count}");
            Console.WriteLine($"Unique:       {tileBlocks.UniqueCount}");

            if (compression != L1Til.CompressionType.None)
                Console.WriteLine($"Decompressed: {decompressed.Length:N0} bytes");

            return 0;
        }

        static int Export(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli til export <til-file> <tile-index> [-o <output.png>]"); return 1; }

            var filePath = args[0];
            if (!int.TryParse(args[1], out int tileIndex))
            {
                Console.Error.WriteLine($"Invalid tile index: {args[1]}");
                return 1;
            }

            string outputPath = null;
            for (int i = 2; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputPath = args[i + 1];
            }

            var data = File.ReadAllBytes(filePath);
            var compression = L1Til.DetectCompression(data);
            byte[] decompressed = compression != L1Til.CompressionType.None
                ? L1Til.Decompress(data, compression)
                : data;

            var tileBlocks = L1Til.ParseToTileBlocks(decompressed, false);

            if (tileIndex < 0 || tileIndex >= tileBlocks.Count)
            {
                Console.Error.WriteLine($"Tile index {tileIndex} out of range (0-{tileBlocks.Count - 1})");
                return 1;
            }

            outputPath ??= $"{Path.GetFileNameWithoutExtension(filePath)}_tile{tileIndex}.png";

            var blockData = tileBlocks.Get(tileIndex);
            int dim = L1Til.GetTileSize(decompressed);

            // Render to BGRA
            byte[] bgra = new byte[dim * dim * 4];
            L1Til.RenderBlockToBgra(blockData, 0, 0, bgra, dim, dim, 0, 0, 0);

            // Convert BGRA to Image<Rgba32>
            using var image = new Image<Rgba32>(dim, dim);
            for (int y = 0; y < dim; y++)
            {
                for (int x = 0; x < dim; x++)
                {
                    int idx = (y * dim + x) * 4;
                    image[x, y] = new Rgba32(bgra[idx + 2], bgra[idx + 1], bgra[idx], bgra[idx + 3]);
                }
            }

            using (var fs = File.Create(outputPath))
            {
                image.Save(fs, new PngEncoder());
            }

            Console.WriteLine($"Exported tile {tileIndex} ({dim}x{dim}) -> {outputPath}");
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("TIL tile file operations");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli til <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  info <til-file>                                 Show TIL metadata");
            Console.WriteLine("  export <til-file> <tile-index> [-o <output>]    Export tile as PNG");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown til command: {cmd}"); PrintUsage(); return 1; }
    }
}
