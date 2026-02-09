using System;
using System.IO;
using System.Text;
using Lin.Helper.Core.Xml;

namespace PakViewer.Cli
{
    internal static class XmlCommands
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
                "decrypt" => Decrypt(subArgs),
                "encrypt" => Encrypt(subArgs),
                "info" => Info(subArgs),
                "--help" or "-h" => PrintUsageOk(),
                _ => Unknown(command)
            };
        }

        static int Decrypt(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli xml decrypt <input-file> [-o <output-file>]"); return 1; }

            var inputPath = args[0];
            string outputPath = null;

            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputPath = args[i + 1];
            }

            var data = File.ReadAllBytes(inputPath);

            if (!XmlCracker.IsEncrypted(data))
            {
                Console.Error.WriteLine("File is not encrypted (or not a Lineage XML file).");
                return 1;
            }

            var decrypted = XmlCracker.Decrypt(data);
            outputPath ??= Path.ChangeExtension(inputPath, ".decrypted.xml");

            File.WriteAllBytes(outputPath, decrypted);

            var encoding = XmlCracker.GetXmlEncoding(decrypted, Path.GetFileName(inputPath));
            Console.WriteLine($"Decrypted: {Path.GetFileName(inputPath)} -> {outputPath}");
            Console.WriteLine($"Encoding: {encoding.WebName}");
            Console.WriteLine($"Size: {decrypted.Length:N0} bytes");
            return 0;
        }

        static int Encrypt(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli xml encrypt <input-file> [-o <output-file>]"); return 1; }

            var inputPath = args[0];
            string outputPath = null;

            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "-o" || args[i] == "--output")
                    outputPath = args[i + 1];
            }

            var data = File.ReadAllBytes(inputPath);

            if (XmlCracker.IsEncrypted(data))
            {
                Console.Error.WriteLine("File is already encrypted.");
                return 1;
            }

            if (!XmlCracker.IsDecryptedXml(data))
            {
                Console.Error.WriteLine("File does not appear to be an XML file (first byte is not '<').");
                return 1;
            }

            var encrypted = XmlCracker.Encrypt(data);
            outputPath ??= Path.ChangeExtension(inputPath, ".encrypted.xml");

            File.WriteAllBytes(outputPath, encrypted);
            Console.WriteLine($"Encrypted: {Path.GetFileName(inputPath)} -> {outputPath}");
            Console.WriteLine($"Size: {encrypted.Length:N0} bytes");
            return 0;
        }

        static int Info(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: pakviewer-cli xml info <file>"); return 1; }

            var inputPath = args[0];
            var data = File.ReadAllBytes(inputPath);

            Console.WriteLine($"File: {Path.GetFileName(inputPath)}");
            Console.WriteLine($"Size: {data.Length:N0} bytes");

            if (XmlCracker.IsEncrypted(data))
            {
                Console.WriteLine("Status: Encrypted");
            }
            else if (XmlCracker.IsDecryptedXml(data))
            {
                Console.WriteLine("Status: Decrypted (plain XML)");
                var encoding = XmlCracker.GetXmlEncoding(data, Path.GetFileName(inputPath));
                Console.WriteLine($"Encoding: {encoding.WebName}");
            }
            else
            {
                Console.WriteLine("Status: Unknown (not a Lineage XML file)");
            }

            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("XML encryption/decryption (Lineage 1)");
            Console.WriteLine();
            Console.WriteLine("Usage: pakviewer-cli xml <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  decrypt <input> [-o <output>]                   Decrypt XML file");
            Console.WriteLine("  encrypt <input> [-o <output>]                   Encrypt XML file");
            Console.WriteLine("  info <file>                                     Check encryption status");
        }

        static int PrintUsageOk() { PrintUsage(); return 0; }
        static int Unknown(string cmd) { Console.Error.WriteLine($"Unknown xml command: {cmd}"); PrintUsage(); return 1; }
    }
}
