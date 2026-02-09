using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp.Formats.Png;

namespace PakViewer.Cli
{
    internal static class SprCommands
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
                "list-parse" => ListParse(subArgs),
                "list-convert" => ListConvert(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int Info(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli spr info <client-folder|idx-file> <spr-name>"); return 1; }

            var source = args[0];
            var sprName = args[1];

            var data = LoadSprData(source, sprName);
            if (data == null) return 1;

            var frames = SprReader.Load(data);
            Console.WriteLine($"SPR: {sprName}");
            Console.WriteLine($"Data Size: {data.Length:N0} bytes");
            Console.WriteLine($"Frames: {frames.Length}");
            Console.WriteLine();

            for (int i = 0; i < frames.Length; i++)
            {
                var f = frames[i];
                string imgInfo = f.Image != null ? $"{f.Width}x{f.Height}" : "no image";
                Console.WriteLine($"  Frame {i}: {imgInfo}, offset=({f.XOffset},{f.YOffset})");
            }

            return 0;
        }

        static int Export(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli spr export <client-folder|idx-file> <spr-name> [-o <output-folder>]"); return 1; }

            var source = args[0];
            var sprName = args[1];
            string outputFolder = "output";

            for (int i = 2; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputFolder = args[i + 1];
            }

            var data = LoadSprData(source, sprName);
            if (data == null) return 1;

            var frames = SprReader.Load(data);
            Console.WriteLine($"Loaded {frames.Length} frames from {sprName}");

            Directory.CreateDirectory(outputFolder);

            int exported = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                if (frame.Image == null)
                {
                    Console.WriteLine($"  Frame {i}: no image data, skipped");
                    continue;
                }

                var outputPath = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(sprName)}_frame{i}.png");
                using (var fs = File.Create(outputPath))
                {
                    frame.Image.Save(fs, new PngEncoder());
                }
                Console.WriteLine($"  Frame {i}: {frame.Width}x{frame.Height} -> {outputPath}");
                exported++;
            }

            Console.WriteLine($"\nExported {exported}/{frames.Length} frames to {outputFolder}");
            return 0;
        }

        static int ListParse(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli spr list-parse <sprlist-file>"); return 1; }

            var filePath = args[0];
            var result = SprListParser.LoadFromFile(filePath);

            Console.WriteLine($"SPR List: {Path.GetFileName(filePath)}");
            Console.WriteLine($"Total Entries: {result.TotalEntries}");
            Console.WriteLine($"Parsed Entries: {result.Entries.Count}");

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine($"\nWarnings ({result.Warnings.Count}):");
                foreach (var w in result.Warnings.Take(10))
                    Console.WriteLine($"  {w}");
                if (result.Warnings.Count > 10)
                    Console.WriteLine($"  ... and {result.Warnings.Count - 10} more");
            }

            Console.WriteLine();
            int showCount = Math.Min(20, result.Entries.Count);
            Console.WriteLine($"First {showCount} entries:");
            for (int i = 0; i < showCount; i++)
            {
                var entry = result.Entries[i];
                Console.WriteLine($"  #{entry.Id}: ImageCount={entry.ImageCount}, Actions={entry.Actions.Count}, Attrs={entry.Attributes.Count}, Name={entry.Name}");
            }
            if (result.Entries.Count > showCount)
                Console.WriteLine($"  ... and {result.Entries.Count - showCount} more");

            return 0;
        }

        static int ListConvert(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli spr list-convert <input-file> <output-file> [--compact]"); return 1; }

            var inputPath = args[0];
            var outputPath = args[1];
            bool compact = args.Any(a => a == "--compact");

            var sprList = SprListParser.LoadFromFile(inputPath);
            string output = compact
                ? SprListWriter.ToCompactFormat(sprList)
                : SprListWriter.ToStandardFormat(sprList);

            File.WriteAllText(outputPath, output);
            Console.WriteLine($"Converted: {Path.GetFileName(inputPath)} -> {Path.GetFileName(outputPath)} ({(compact ? "compact" : "standard")} format)");
            Console.WriteLine($"Entries: {sprList.Entries.Count}");
            return 0;
        }

        /// <summary>
        /// 從 client 資料夾或 IDX 檔案中找到並提取 SPR 資料
        /// </summary>
        static byte[] LoadSprData(string source, string sprName)
        {
            // 如果 source 是目錄，搜尋 sprite*.idx
            if (Directory.Exists(source))
            {
                var idxFiles = Directory.GetFiles(source, "sprite*.idx");
                foreach (var idxFile in idxFiles)
                {
                    using var pak = new PakFile(idxFile);
                    int idx = pak.FindFileIndex(sprName);
                    if (idx >= 0)
                    {
                        Console.WriteLine($"Found {sprName} in {Path.GetFileName(idxFile)}");
                        return pak.Extract(idx);
                    }
                }

                Console.Error.WriteLine($"File '{sprName}' not found in any sprite*.idx under {source}");
                return null;
            }

            // 如果 source 是 IDX 檔案
            if (File.Exists(source))
            {
                using var pak = new PakFile(source);
                return pak.Extract(sprName);
            }

            Console.Error.WriteLine($"Source not found: {source}");
            return null;
        }

        static void PrintUsage()
        {
            Console.WriteLine("SPR sprite file operations");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli spr <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  info <folder|idx> <spr-name>                    Show SPR info");
            Console.WriteLine("  export <folder|idx> <spr-name> [-o <dir>]       Export frames as PNG");
            Console.WriteLine("  list-parse <sprlist-file>                        Parse SPR list file");
            Console.WriteLine("  list-convert <input> <output> [--compact]        Convert SPR list format");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown spr command: {cmd}"); PrintUsage(); return 1; }
    }
}
