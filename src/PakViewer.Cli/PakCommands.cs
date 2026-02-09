using System;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;

namespace PakViewer.Cli
{
    internal static class PakCommands
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
                "list" => List(subArgs),
                "info" => Info(subArgs),
                "extract" => Extract(subArgs),
                "extract-all" => ExtractAll(subArgs),
                "add" => Add(subArgs),
                "delete" => Delete(subArgs),
                "create" => Create(subArgs),
                "search" => Search(subArgs),
                "verify" => Verify(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int List(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli pak list <idx-file> [--filter <pattern>]"); return 1; }

            var idxPath = args[0];
            string filter = null;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--filter" || args[i] == "-f")
                    filter = args[i + 1];
            }

            using var pak = new PakFile(idxPath);
            var files = pak.Files.AsEnumerable();

            if (!string.IsNullOrEmpty(filter))
                files = files.Where(f => f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"{"#",-6} {"FileName",-24} {"Size",10} {"Offset",12}");
            Console.WriteLine(new string('-', 56));

            int count = 0;
            foreach (var f in files)
            {
                Console.WriteLine($"{count,-6} {f.FileName,-24} {f.FileSize,10:N0} 0x{f.Offset:X8}");
                count++;
            }

            Console.WriteLine($"\nTotal: {count} files");
            return 0;
        }

        static int Info(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli pak info <idx-file>"); return 1; }

            var idxPath = args[0];
            using var pak = new PakFile(idxPath);

            Console.WriteLine($"IDX File:    {Path.GetFileName(pak.IdxPath)}");
            Console.WriteLine($"PAK File:    {Path.GetFileName(pak.PakPath)}");
            Console.WriteLine($"Encryption:  {pak.EncryptionType}");
            Console.WriteLine($"Protected:   {pak.IsProtected}");
            Console.WriteLine($"File Count:  {pak.Count}");

            if (pak.Count > 0)
            {
                long totalSize = pak.Files.Sum(f => (long)f.FileSize);
                var extensions = pak.Files
                    .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                    .Where(e => !string.IsNullOrEmpty(e))
                    .GroupBy(e => e)
                    .OrderByDescending(g => g.Count())
                    .Take(10);

                Console.WriteLine($"Total Size:  {totalSize:N0} bytes ({totalSize / 1024.0 / 1024.0:F2} MB)");
                Console.WriteLine($"Sorted:      {pak.IsSorted()}");
                Console.WriteLine($"\nTop Extensions:");
                foreach (var ext in extensions)
                    Console.WriteLine($"  {ext.Key,-10} {ext.Count(),6} files");
            }

            return 0;
        }

        static int Extract(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli pak extract <idx-file> <filename> [-o <output-path>]"); return 1; }

            var idxPath = args[0];
            var fileName = args[1];
            string outputPath = null;

            for (int i = 2; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputPath = args[i + 1];
            }

            outputPath ??= fileName;

            using var pak = new PakFile(idxPath);
            var data = pak.Extract(fileName);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputPath, data);
            Console.WriteLine($"Extracted: {fileName} ({data.Length:N0} bytes) -> {outputPath}");
            return 0;
        }

        static int ExtractAll(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli pak extract-all <idx-file> <output-folder>"); return 1; }

            var idxPath = args[0];
            var outputFolder = args[1];

            using var pak = new PakFile(idxPath);
            Console.WriteLine($"Extracting {pak.Count} files from {Path.GetFileName(idxPath)}...");

            pak.ExtractAll(outputFolder, (current, total, name) =>
            {
                Console.Write($"\r[{current}/{total}] {name}                    ");
            });

            Console.WriteLine($"\nDone! Extracted to: {outputFolder}");
            return 0;
        }

        static int Add(string[] args)
        {
            if (args.Length < 3) { Console.Error.WriteLine("Usage: pakviewer-cli pak add <idx-file> <name-in-pak> <source-file> [--sorted]"); return 1; }

            var idxPath = args[0];
            var nameInPak = args[1];
            var sourceFile = args[2];
            bool sorted = args.Any(a => a == "--sorted");

            if (!File.Exists(sourceFile))
            {
                Console.Error.WriteLine($"Source file not found: {sourceFile}");
                return 1;
            }

            var data = File.ReadAllBytes(sourceFile);
            using var pak = new PakFile(idxPath);

            pak.Add(nameInPak, data, sorted);
            pak.Save();

            Console.WriteLine($"Added: {nameInPak} ({data.Length:N0} bytes)");
            return 0;
        }

        static int Delete(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli pak delete <idx-file> <filename>"); return 1; }

            var idxPath = args[0];
            var fileName = args[1];

            using var pak = new PakFile(idxPath);
            pak.Delete(fileName);
            pak.Save();

            Console.WriteLine($"Deleted: {fileName}");
            return 0;
        }

        static int Create(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli pak create <idx-file>"); return 1; }

            var idxPath = args[0];
            bool encrypted = !args.Any(a => a == "--no-encrypt");

            using var pak = PakFile.Create(idxPath, encrypted);
            Console.WriteLine($"Created: {Path.GetFileName(idxPath)} (encrypted={encrypted})");
            return 0;
        }

        static int Search(string[] args)
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: pakviewer-cli pak search <idx-file> <pattern>"); return 1; }

            var idxPath = args[0];
            var pattern = args[1];

            using var pak = new PakFile(idxPath);
            var matches = pak.Files
                .Where(f => f.FileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"Search '{pattern}' in {Path.GetFileName(idxPath)}: {matches.Count} matches");
            Console.WriteLine();

            foreach (var f in matches)
                Console.WriteLine($"  {f.FileName,-24} {f.FileSize,10:N0} bytes");

            return 0;
        }

        static int Verify(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli pak verify <idx-file> [--underscore-first]"); return 1; }

            var idxPath = args[0];
            var sortType = args.Any(a => a == "--underscore-first")
                ? PakFile.SortType.UnderscoreFirst
                : PakFile.SortType.Ascii;

            using var pak = new PakFile(idxPath);
            var errors = pak.VerifySortOrder(sortType);

            if (errors.Count == 0)
            {
                Console.WriteLine($"{Path.GetFileName(idxPath)}: Sort order OK ({pak.Count} files, {sortType})");
            }
            else
            {
                Console.WriteLine($"{Path.GetFileName(idxPath)}: {errors.Count} sort errors (type={sortType})");
                foreach (var (index, actual, expected) in errors.Take(20))
                    Console.WriteLine($"  [{index}] {actual} -> expected: {expected}");
                if (errors.Count > 20)
                    Console.WriteLine($"  ... and {errors.Count - 20} more");
            }

            return errors.Count == 0 ? 0 : 2;
        }

        static void PrintUsage()
        {
            Console.WriteLine("PAK/IDX archive operations");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli pak <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list <idx> [--filter <pattern>]                 List files");
            Console.WriteLine("  info <idx>                                      Show PAK metadata");
            Console.WriteLine("  extract <idx> <name> [-o <path>]                Extract single file");
            Console.WriteLine("  extract-all <idx> <output-dir>                  Extract all files");
            Console.WriteLine("  add <idx> <name> <file> [--sorted]              Add file to PAK");
            Console.WriteLine("  delete <idx> <name>                             Delete file");
            Console.WriteLine("  create <idx> [--no-encrypt]                     Create empty PAK");
            Console.WriteLine("  search <idx> <pattern>                          Search filenames");
            Console.WriteLine("  verify <idx> [--underscore-first]               Verify sort order");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown pak command: {cmd}"); PrintUsage(); return 1; }
    }
}
