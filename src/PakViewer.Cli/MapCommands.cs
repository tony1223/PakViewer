using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Map;

namespace PakViewer.Cli
{
    internal static class MapCommands
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
                "tiles" => Tiles(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int Info(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli map info <s32-or-seg-file>"); return 1; }

            var filePath = args[0];
            var data = File.ReadAllBytes(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            S32Data s32;
            string format;

            if (ext == ".seg")
            {
                s32 = SegReader.Parse(data);
                format = "SEG";
            }
            else
            {
                s32 = S32Reader.Parse(data);
                format = "S32";
            }

            Console.WriteLine($"Map File:     {Path.GetFileName(filePath)}");
            Console.WriteLine($"Format:       {format}");
            Console.WriteLine($"File Size:    {data.Length:N0} bytes");
            Console.WriteLine($"Used Tiles:   {s32.UsedTiles.Count}");
            Console.WriteLine($"Layer 2 Items: {s32.Layer2.Count}");
            Console.WriteLine($"Layer 3 Size: {S32Data.StandardHeight}x{S32Data.StandardWidth}");

            return 0;
        }

        static int Tiles(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli map tiles <s32-or-seg-file>"); return 1; }

            var filePath = args[0];
            var data = File.ReadAllBytes(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            S32Data s32 = ext == ".seg" ? SegReader.Parse(data) : S32Reader.Parse(data);

            Console.WriteLine($"Used tiles in {Path.GetFileName(filePath)}:");
            Console.WriteLine();
            Console.WriteLine($"{"TileId",8} {"IndexId",8} {"UsageCount",12}");
            Console.WriteLine(new string('-', 32));

            foreach (var tile in s32.UsedTiles.Values.OrderBy(t => t.TileId))
            {
                Console.WriteLine($"{tile.TileId,8} {tile.IndexId,8} {tile.UsageCount,12}");
            }

            Console.WriteLine($"\nTotal: {s32.UsedTiles.Count} unique tiles");
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("S32/SEG map file operations");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli map <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  info <s32|seg>                                  Show map metadata");
            Console.WriteLine("  tiles <s32|seg>                                 List used tiles");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown map command: {cmd}"); PrintUsage(); return 1; }
    }
}
