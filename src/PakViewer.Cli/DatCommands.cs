using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Dat;

namespace PakViewer.Cli
{
    internal static class DatCommands
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
                "list" => List(subArgs),
                "extract" => Extract(subArgs),
                "extract-all" => ExtractAll(subArgs),
                "export-zip" => ExportZip(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int Info(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli dat info <dat-file>"); return 1; }

            var datPath = args[0];
            var dat = new DatFile(datPath);
            var footer = dat.ReadFooter();

            Console.WriteLine($"DAT File:     {dat.FileName}");
            Console.WriteLine($"File Size:    {dat.FileSize:N0} bytes ({dat.FileSize / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"Encrypted:    {footer.IsEncrypted}");
            Console.WriteLine($"Index Offset: 0x{footer.IndexOffset:X8}");
            Console.WriteLine($"Index Size:   {footer.IndexSize:N0} bytes");

            dat.ParseEntries();
            Console.WriteLine($"Entries:      {dat.Entries.Count}");

            if (dat.Entries.Count > 0)
            {
                long totalSize = dat.Entries.Sum(e => (long)e.Size);
                Console.WriteLine($"Total Data:   {totalSize:N0} bytes ({totalSize / 1024.0 / 1024.0:F2} MB)");
            }

            return 0;
        }

        static int List(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli dat list <dat-file>"); return 1; }

            var datPath = args[0];
            var dat = new DatFile(datPath);
            dat.ParseEntries();

            Console.WriteLine($"{"#",-6} {"Path",-50} {"Size",10} {"Type",6}");
            Console.WriteLine(new string('-', 76));

            for (int i = 0; i < dat.Entries.Count; i++)
            {
                var entry = dat.Entries[i];
                Console.WriteLine($"{i,-6} {entry.Path,-50} {entry.Size,10:N0} {entry.Type,6}");
            }

            Console.WriteLine($"\nTotal: {dat.Entries.Count} entries");
            return 0;
        }

        static int Extract(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli dat extract <dat-file> <entry-path> [-o <output-path>]"); return 1; }

            var datPath = args[0];
            var entryPath = args[1];
            string outputPath = null;

            for (int i = 2; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputPath = args[i + 1];
            }

            var dat = new DatFile(datPath);
            dat.ParseEntries();

            var entry = dat.Entries.FirstOrDefault(e =>
                e.Path.Equals(entryPath, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                Console.Error.WriteLine($"Entry not found: {entryPath}");
                return 1;
            }

            outputPath ??= Path.GetFileName(entryPath);

            var data = dat.ExtractFile(entry);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputPath, data);
            Console.WriteLine($"Extracted: {entryPath} ({data.Length:N0} bytes) -> {outputPath}");
            return 0;
        }

        static int ExtractAll(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli dat extract-all <dat-file> <output-dir>"); return 1; }

            var datPath = args[0];
            var outputDir = args[1];

            var dat = new DatFile(datPath);
            dat.ParseEntries();

            Console.WriteLine($"Extracting {dat.Entries.Count} entries from {dat.FileName}...");

            var (extracted, errors) = dat.ExtractAll(outputDir, (current, total, path) =>
            {
                Console.Write($"\r[{current}/{total}] {path}                    ");
            });

            Console.WriteLine($"\nDone! Extracted: {extracted}, Errors: {errors}");
            return 0;
        }

        static int ExportZip(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli dat export-zip <dat-file> <output.zip>"); return 1; }

            var datPath = args[0];
            var zipPath = args[1];

            var dat = new DatFile(datPath);
            dat.ParseEntries();

            Console.WriteLine($"Exporting {dat.Entries.Count} entries to ZIP...");

            dat.ExportToZip(zipPath, (current, total, path) =>
            {
                Console.Write($"\r[{current}/{total}] {path}                    ");
            });

            Console.WriteLine($"\nDone! -> {zipPath}");
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("DAT file operations (Lineage M)");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli dat <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  info <dat-file>                                 Show DAT metadata");
            Console.WriteLine("  list <dat-file>                                 List entries");
            Console.WriteLine("  extract <dat> <entry-path> [-o <path>]          Extract single entry");
            Console.WriteLine("  extract-all <dat> <output-dir>                  Extract all entries");
            Console.WriteLine("  export-zip <dat> <output.zip>                   Export as ZIP");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown dat command: {cmd}"); PrintUsage(); return 1; }
    }
}
