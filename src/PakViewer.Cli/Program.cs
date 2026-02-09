using System;
using System.Reflection;
using System.Text;

namespace PakViewer.Cli
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var group = args[0].ToLowerInvariant();
            var subArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

            return group switch
            {
                "pak" => PakCommands.Run(subArgs),
                "spr" => SprCommands.Run(subArgs),
                "dat" => DatCommands.Run(subArgs),
                "xml" => XmlCommands.Run(subArgs),
                "map" => MapCommands.Run(subArgs),
                "til" => TilCommands.Run(subArgs),
                "version" or "--version" or "-v" => PrintVersion(),
                "--help" or "-h" or "help" => PrintUsageOk(),
                _ => UnknownCommand(group)
            };
        }

        static int PrintVersion()
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "unknown";
            Console.WriteLine($"pakviewer-cli {version}");
            return 0;
        }

        static int PrintUsageOk()
        {
            PrintUsage();
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("PakViewer CLI - Lineage 1 file format tools");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli <command-group> <command> [arguments] [options]");
            Console.WriteLine();
            Console.WriteLine("Command Groups:");
            Console.WriteLine("  pak       PAK/IDX archive operations");
            Console.WriteLine("  spr       SPR sprite file operations");
            Console.WriteLine("  dat       DAT file operations (Lineage M)");
            Console.WriteLine("  xml       XML encryption/decryption");
            Console.WriteLine("  map       S32/SEG map file operations");
            Console.WriteLine("  til       TIL tile file operations");
            Console.WriteLine("  version   Show version information");
            Console.WriteLine();
            Console.WriteLine("Use 'pakviewer-cli <command-group> --help' for more information.");
        }

        static int UnknownCommand(string command)
        {
            Console.Error.WriteLine($"Unknown command group: {command}");
            Console.Error.WriteLine("Use 'pakviewer-cli --help' for available commands.");
            return 1;
        }
    }
}
