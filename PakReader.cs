using System;
using System.IO;
using System.Text;
using PakViewer.Utility;

namespace PakViewer
{
    class PakReader
    {
        static void Main(string[] args)
        {
            // Register code pages
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length < 1)
            {
                ShowHelp();
                return;
            }

            string command = args[0].ToLower();

            switch (command)
            {
                case "list":
                    if (args.Length < 2) { ShowHelp(); return; }
                    ListFiles(args[1], args.Length > 2 ? args[2] : null);
                    break;

                case "read":
                    if (args.Length < 3) { ShowHelp(); return; }
                    ReadFile(args[1], args[2], args.Length > 3 ? args[3] : null);
                    break;

                case "export":
                    if (args.Length < 4) { ShowHelp(); return; }
                    ExportFile(args[1], args[2], args[3], args.Length > 4 ? args[4] : null);
                    break;

                case "import":
                    if (args.Length < 4) { ShowHelp(); return; }
                    ImportFile(args[1], args[2], args[3], args.Length > 4 ? args[4] : null);
                    break;

                case "info":
                    if (args.Length < 2) { ShowHelp(); return; }
                    ShowInfo(args[1]);
                    break;

                default:
                    ShowHelp();
                    break;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("PakReader - Lineage PAK file utility");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list <idx_file> [filter]");
            Console.WriteLine("    List files in PAK. Optional filter by filename pattern.");
            Console.WriteLine();
            Console.WriteLine("  read <idx_file> <filename> [encoding]");
            Console.WriteLine("    Read and display file content from PAK.");
            Console.WriteLine();
            Console.WriteLine("  export <idx_file> <filename> <output_file> [encoding]");
            Console.WriteLine("    Export file from PAK to disk.");
            Console.WriteLine();
            Console.WriteLine("  import <idx_file> <filename> <input_file> [encoding]");
            Console.WriteLine("    Import file from disk into PAK (must be same size).");
            Console.WriteLine();
            Console.WriteLine("  info <idx_file>");
            Console.WriteLine("    Show PAK file information.");
            Console.WriteLine();
            Console.WriteLine("Encodings: big5, euc-kr, shift_jis, gb2312, utf-8 (default: auto-detect)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  PakReader list Text.idx");
            Console.WriteLine("  PakReader list Text.idx html");
            Console.WriteLine("  PakReader read Text.idx 07bearNPC-c.html");
            Console.WriteLine("  PakReader export Text.idx 07bearNPC-c.html output.html");
            Console.WriteLine("  PakReader import Text.idx 07bearNPC-c.html modified.html");
        }

        static Encoding GetEncoding(string fileName, string encodingName)
        {
            if (!string.IsNullOrEmpty(encodingName))
            {
                return Encoding.GetEncoding(encodingName);
            }

            // Auto-detect by filename
            string fileNameLower = fileName.ToLower();
            if (fileNameLower.IndexOf("-k.") >= 0)
                return Encoding.GetEncoding("euc-kr");
            else if (fileNameLower.IndexOf("-j.") >= 0)
                return Encoding.GetEncoding("shift_jis");
            else if (fileNameLower.IndexOf("-h.") >= 0)
                return Encoding.GetEncoding("gb2312");
            else
                return Encoding.GetEncoding("big5");
        }

        static (L1PakTools.IndexRecord[] records, bool isProtected)? LoadIndex(string idxFile)
        {
            if (!File.Exists(idxFile))
            {
                Console.WriteLine($"Error: IDX file not found: {idxFile}");
                return null;
            }

            byte[] idxData = File.ReadAllBytes(idxFile);

            // Check if protected
            L1PakTools.IndexRecord firstRecord = L1PakTools.Decode_Index_FirstRecord(idxData);
            bool isProtected = firstRecord.Offset != 0;

            // Decode index if protected
            byte[] indexData = isProtected ? L1PakTools.Decode(idxData, 4) : idxData;

            // Parse index records
            int recordSize = 28;
            int recordCount = indexData.Length / recordSize;
            var records = new L1PakTools.IndexRecord[recordCount];

            for (int i = 0; i < recordCount; i++)
            {
                records[i] = new L1PakTools.IndexRecord(indexData, i * recordSize);
            }

            return (records, isProtected);
        }

        static void ShowInfo(string idxFile)
        {
            var result = LoadIndex(idxFile);
            if (result == null) return;

            var (records, isProtected) = result.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            Console.WriteLine($"IDX File: {idxFile}");
            Console.WriteLine($"PAK File: {pakFile}");
            Console.WriteLine($"PAK Exists: {File.Exists(pakFile)}");
            Console.WriteLine($"Protected: {isProtected}");
            Console.WriteLine($"Total Records: {records.Length}");

            if (File.Exists(pakFile))
            {
                var fi = new FileInfo(pakFile);
                Console.WriteLine($"PAK Size: {fi.Length:N0} bytes");
            }
        }

        static void ListFiles(string idxFile, string filter)
        {
            var result = LoadIndex(idxFile);
            if (result == null) return;

            var (records, isProtected) = result.Value;

            Console.WriteLine($"Total: {records.Length} files");
            Console.WriteLine($"Protected: {isProtected}");
            Console.WriteLine();
            Console.WriteLine("No.\tSize\tOffset\t\tFileName");
            Console.WriteLine("---\t----\t------\t\t--------");

            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                var rec = records[i];
                if (string.IsNullOrEmpty(filter) || rec.FileName.ToLower().Contains(filter.ToLower()))
                {
                    Console.WriteLine($"{i + 1}\t{rec.FileSize}\t0x{rec.Offset:X8}\t{rec.FileName}");
                    count++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Shown: {count} files");
        }

        static void ReadFile(string idxFile, string targetFile, string encodingName)
        {
            var result = LoadIndex(idxFile);
            if (result == null) return;

            var (records, isProtected) = result.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            if (!File.Exists(pakFile))
            {
                Console.WriteLine($"Error: PAK file not found: {pakFile}");
                return;
            }

            // Find target file
            L1PakTools.IndexRecord? foundRecord = null;
            int foundIndex = -1;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].FileName.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    foundRecord = records[i];
                    foundIndex = i;
                    break;
                }
            }

            if (foundRecord == null)
            {
                Console.WriteLine($"Error: File not found: {targetFile}");
                return;
            }

            var rec = foundRecord.Value;
            Console.WriteLine($"File: {rec.FileName}");
            Console.WriteLine($"Index: {foundIndex + 1}");
            Console.WriteLine($"Offset: 0x{rec.Offset:X8} ({rec.Offset})");
            Console.WriteLine($"Size: {rec.FileSize} bytes");
            Console.WriteLine($"Protected: {isProtected}");

            // Read PAK data
            byte[] pakData;
            using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read))
            {
                pakData = new byte[rec.FileSize];
                fs.Seek(rec.Offset, SeekOrigin.Begin);
                fs.Read(pakData, 0, rec.FileSize);
            }

            // Decode if protected
            if (isProtected)
            {
                pakData = L1PakTools.Decode(pakData, 0);
            }

            // Show raw bytes
            Console.WriteLine();
            Console.WriteLine($"Raw bytes (first {Math.Min(64, pakData.Length)}):");
            Console.WriteLine(BitConverter.ToString(pakData, 0, Math.Min(64, pakData.Length)));

            // Determine encoding
            Encoding encoding = GetEncoding(targetFile, encodingName);

            Console.WriteLine();
            Console.WriteLine($"Encoding: {encoding.EncodingName}");
            Console.WriteLine();
            Console.WriteLine("=== Content ===");
            string content = encoding.GetString(pakData);
            Console.WriteLine(content.Substring(0, Math.Min(3000, content.Length)));

            if (content.Length > 3000)
            {
                Console.WriteLine();
                Console.WriteLine($"... (truncated, total {content.Length} chars)");
            }
        }

        static void ExportFile(string idxFile, string targetFile, string outputFile, string encodingName)
        {
            var result = LoadIndex(idxFile);
            if (result == null) return;

            var (records, isProtected) = result.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            if (!File.Exists(pakFile))
            {
                Console.WriteLine($"Error: PAK file not found: {pakFile}");
                return;
            }

            // Find target file
            L1PakTools.IndexRecord? foundRecord = null;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].FileName.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    foundRecord = records[i];
                    break;
                }
            }

            if (foundRecord == null)
            {
                Console.WriteLine($"Error: File not found: {targetFile}");
                return;
            }

            var rec = foundRecord.Value;

            // Read PAK data
            byte[] pakData;
            using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read))
            {
                pakData = new byte[rec.FileSize];
                fs.Seek(rec.Offset, SeekOrigin.Begin);
                fs.Read(pakData, 0, rec.FileSize);
            }

            // Decode if protected
            if (isProtected)
            {
                pakData = L1PakTools.Decode(pakData, 0);
            }

            // Write to output file
            File.WriteAllBytes(outputFile, pakData);

            Console.WriteLine($"Exported: {targetFile} -> {outputFile}");
            Console.WriteLine($"Size: {pakData.Length} bytes");
            Console.WriteLine($"Encoding used for decode: {(isProtected ? "Yes (L1 encryption)" : "No")}");
        }

        static void ImportFile(string idxFile, string targetFile, string inputFile, string encodingName)
        {
            var result = LoadIndex(idxFile);
            if (result == null) return;

            var (records, isProtected) = result.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            if (!File.Exists(pakFile))
            {
                Console.WriteLine($"Error: PAK file not found: {pakFile}");
                return;
            }

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file not found: {inputFile}");
                return;
            }

            // Find target file
            L1PakTools.IndexRecord? foundRecord = null;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].FileName.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    foundRecord = records[i];
                    break;
                }
            }

            if (foundRecord == null)
            {
                Console.WriteLine($"Error: File not found in PAK: {targetFile}");
                return;
            }

            var rec = foundRecord.Value;

            // Read input file
            byte[] inputData = File.ReadAllBytes(inputFile);

            Console.WriteLine($"Target: {targetFile}");
            Console.WriteLine($"Input: {inputFile}");
            Console.WriteLine($"Original size: {rec.FileSize} bytes");
            Console.WriteLine($"New size: {inputData.Length} bytes");

            // Encode if protected
            byte[] dataToWrite = inputData;
            if (isProtected)
            {
                dataToWrite = L1PakTools.Encode(inputData, 0);
                Console.WriteLine($"Encoded size: {dataToWrite.Length} bytes");
            }

            // Check size
            if (dataToWrite.Length != rec.FileSize)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: Size mismatch! Expected {rec.FileSize} bytes, got {dataToWrite.Length} bytes.");
                Console.WriteLine("Cannot import file with different size.");
                return;
            }

            // Write to PAK
            using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Write))
            {
                fs.Seek(rec.Offset, SeekOrigin.Begin);
                fs.Write(dataToWrite, 0, dataToWrite.Length);
            }

            Console.WriteLine();
            Console.WriteLine($"Success! Imported {inputFile} -> {targetFile}");
        }
    }
}
