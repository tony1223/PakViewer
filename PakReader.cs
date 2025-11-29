using System;
using System.IO;
using System.Text;
using PakViewer.Utility;

namespace PakViewer
{
    public class PakReader
    {
        public static void Exec(string[] args)
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
            Console.WriteLine("    Import file from disk into PAK. Supports different file sizes.");
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

            // Check if protected by testing first record filename
            L1PakTools.IndexRecord firstRecord = L1PakTools.Decode_Index_FirstRecord(idxData);
            bool isProtected = !System.Text.RegularExpressions.Regex.IsMatch(
                Encoding.Default.GetString(idxData, 8, 20),
                "^([a-zA-Z0-9_\\-\\.']+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (isProtected)
            {
                // Verify it can be decoded
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    firstRecord.FileName,
                    "^([a-zA-Z0-9_\\-\\.']+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    Console.WriteLine("Error: Cannot decode index file");
                    return null;
                }
            }

            // Decode index if protected
            byte[] indexData = isProtected ? L1PakTools.Decode(idxData, 4) : idxData;

            // Parse index records
            // If protected, decoded data starts at 0; otherwise skip first 4 bytes (record count)
            int recordSize = 28;
            int startOffset = isProtected ? 0 : 4;
            int recordCount = (indexData.Length - startOffset) / recordSize;
            var records = new L1PakTools.IndexRecord[recordCount];

            for (int i = 0; i < recordCount; i++)
            {
                records[i] = new L1PakTools.IndexRecord(indexData, startOffset + i * recordSize);
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

            // Decrypt XML files if encrypted (starts with 'X')
            bool isXmlEncrypted = false;
            if (Path.GetExtension(targetFile).ToLower() == ".xml" && XmlCracker.IsEncrypted(pakData))
            {
                isXmlEncrypted = true;
                pakData = XmlCracker.Decrypt(pakData);
                Console.WriteLine($"XML Encrypted: Yes (decrypted)");
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

            // Decrypt XML files if encrypted (starts with 'X')
            bool isXmlEncrypted = false;
            if (Path.GetExtension(targetFile).ToLower() == ".xml" && XmlCracker.IsEncrypted(pakData))
            {
                isXmlEncrypted = true;
                pakData = XmlCracker.Decrypt(pakData);
            }

            // Write to output file
            File.WriteAllBytes(outputFile, pakData);

            Console.WriteLine($"Exported: {targetFile} -> {outputFile}");
            Console.WriteLine($"Size: {pakData.Length} bytes");
            Console.WriteLine($"L1 Protected: {(isProtected ? "Yes" : "No")}");
            Console.WriteLine($"XML Encrypted: {(isXmlEncrypted ? "Yes (exported decrypted)" : "No")}");
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
                Console.WriteLine($"Error: File not found in PAK: {targetFile}");
                return;
            }

            var rec = foundRecord.Value;

            // Check if original file was XML encrypted
            bool isXmlEncrypted = false;
            if (Path.GetExtension(targetFile).ToLower() == ".xml")
            {
                byte[] originalData;
                using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read))
                {
                    originalData = new byte[rec.FileSize];
                    fs.Seek(rec.Offset, SeekOrigin.Begin);
                    fs.Read(originalData, 0, rec.FileSize);
                }
                if (isProtected)
                {
                    originalData = L1PakTools.Decode(originalData, 0);
                }
                isXmlEncrypted = XmlCracker.IsEncrypted(originalData);
            }

            // Read input file
            byte[] inputData = File.ReadAllBytes(inputFile);

            Console.WriteLine($"Target: {targetFile}");
            Console.WriteLine($"Input: {inputFile}");
            Console.WriteLine($"Original size: {rec.FileSize} bytes");
            Console.WriteLine($"New size: {inputData.Length} bytes");
            Console.WriteLine($"XML Encrypted: {(isXmlEncrypted ? "Yes (will re-encrypt)" : "No")}");

            // Re-encrypt XML if needed
            byte[] dataToWrite = inputData;
            if (isXmlEncrypted)
            {
                dataToWrite = XmlCracker.Encrypt(inputData);
                Console.WriteLine($"XML encrypted size: {dataToWrite.Length} bytes");
            }

            // Encode if L1 protected
            if (isProtected)
            {
                dataToWrite = L1PakTools.Encode(dataToWrite, 0);
                Console.WriteLine($"L1 encoded size: {dataToWrite.Length} bytes");
            }

            // Check size
            if (dataToWrite.Length != rec.FileSize)
            {
                Console.WriteLine();
                Console.WriteLine($"Size changed: {rec.FileSize} -> {dataToWrite.Length} bytes (diff: {dataToWrite.Length - rec.FileSize:+#;-#;0})");
                Console.WriteLine("Rebuilding PAK and IDX files...");

                // Rebuild PAK file with new size
                RebuildPakWithNewSize(idxFile, pakFile, records, foundIndex, dataToWrite, isProtected);
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

        static void RebuildPakWithNewSize(string idxFile, string pakFile, L1PakTools.IndexRecord[] records, int targetIndex, byte[] newData, bool isProtected)
        {
            Console.WriteLine($"Size changed: {records[targetIndex].FileSize} -> {newData.Length} bytes (diff: {newData.Length - records[targetIndex].FileSize:+#;-#;0})");
            Console.WriteLine("Rebuilding PAK and IDX files...");

            string result = RebuildPakWithNewSizeCore(idxFile, pakFile, records, targetIndex, newData, isProtected);

            if (result == null)
            {
                Console.WriteLine();
                Console.WriteLine($"Success! Imported with size change.");
            }
            else
            {
                Console.WriteLine($"Error: {result}");
            }
        }

        /// <summary>
        /// Rebuild PAK and IDX files when file size changes.
        /// </summary>
        /// <param name="idxFile">Path to the IDX file</param>
        /// <param name="pakFile">Path to the PAK file</param>
        /// <param name="records">Array of index records (will be modified)</param>
        /// <param name="targetIndex">Index of the file being modified</param>
        /// <param name="newData">New data to write</param>
        /// <param name="isProtected">Whether the PAK is L1 protected</param>
        /// <returns>null on success, error message on failure</returns>
        public static string RebuildPakWithNewSizeCore(string idxFile, string pakFile, L1PakTools.IndexRecord[] records, int targetIndex, byte[] newData, bool isProtected)
        {
            int targetOffset = records[targetIndex].Offset;
            int oldSize = records[targetIndex].FileSize;
            int sizeDiff = newData.Length - oldSize;

            // Count files that have offset > target offset (they need offset update)
            int filesAfterTarget = 0;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Offset > targetOffset)
                    filesAfterTarget++;
            }

            Console.WriteLine($"Target file index: {targetIndex + 1}/{records.Length}");
            Console.WriteLine($"Target offset in PAK: 0x{targetOffset:X8}");
            Console.WriteLine($"Files after target offset that need update: {filesAfterTarget}");

            // Create backup files
            string pakBackup = pakFile + ".bak";
            string idxBackup = idxFile + ".bak";

            if (File.Exists(pakBackup)) File.Delete(pakBackup);
            if (File.Exists(idxBackup)) File.Delete(idxBackup);

            File.Copy(pakFile, pakBackup);
            File.Copy(idxFile, idxBackup);
            Console.WriteLine($"Created backups: {pakBackup}, {idxBackup}");

            try
            {
                // Read entire PAK file
                byte[] pakData = File.ReadAllBytes(pakFile);

                // Create new PAK file
                using (FileStream newPak = File.Create(pakFile))
                {
                    // Write data before target file (unchanged)
                    if (targetOffset > 0)
                    {
                        newPak.Write(pakData, 0, targetOffset);
                    }

                    // Write the new data for target file
                    newPak.Write(newData, 0, newData.Length);

                    // Write data after target file (unchanged content, but shifted position)
                    int afterStart = targetOffset + oldSize;
                    if (afterStart < pakData.Length)
                    {
                        int afterLength = pakData.Length - afterStart;
                        newPak.Write(pakData, afterStart, afterLength);
                    }
                }

                Console.WriteLine($"PAK file rebuilt: {pakFile}");

                // Update target record with new size (offset stays the same)
                records[targetIndex] = new L1PakTools.IndexRecord(
                    records[targetIndex].FileName,
                    newData.Length,
                    targetOffset
                );

                // Update offsets for all files that come AFTER target in PAK (by offset, not by index)
                for (int i = 0; i < records.Length; i++)
                {
                    if (i != targetIndex && records[i].Offset > targetOffset)
                    {
                        records[i] = new L1PakTools.IndexRecord(
                            records[i].FileName,
                            records[i].FileSize,
                            records[i].Offset + sizeDiff
                        );
                    }
                }

                // Rebuild IDX file
                RebuildIndex(idxFile, records, isProtected);
                Console.WriteLine($"IDX file rebuilt: {idxFile}");

                // Delete backups on success
                File.Delete(pakBackup);
                File.Delete(idxBackup);
                Console.WriteLine("Backups removed (success)");

                return null; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during rebuild: {ex.Message}");
                Console.WriteLine("Restoring from backups...");

                // Restore from backups
                if (File.Exists(pakBackup))
                {
                    File.Copy(pakBackup, pakFile, true);
                    File.Delete(pakBackup);
                }
                if (File.Exists(idxBackup))
                {
                    File.Copy(idxBackup, idxFile, true);
                    File.Delete(idxBackup);
                }
                Console.WriteLine("Restored from backups.");

                return ex.Message;
            }
        }

        public static void RebuildIndex(string idxFile, L1PakTools.IndexRecord[] records, bool isProtected)
        {
            // Build raw index data (without 4-byte header)
            int recordSize = 28;
            byte[] indexData = new byte[records.Length * recordSize];

            for (int i = 0; i < records.Length; i++)
            {
                int offset = i * recordSize;

                // Offset (4 bytes)
                byte[] offsetBytes = BitConverter.GetBytes(records[i].Offset);
                Array.Copy(offsetBytes, 0, indexData, offset, 4);

                // FileName (20 bytes)
                byte[] nameBytes = Encoding.Default.GetBytes(records[i].FileName);
                Array.Copy(nameBytes, 0, indexData, offset + 4, Math.Min(nameBytes.Length, 20));

                // FileSize (4 bytes)
                byte[] sizeBytes = BitConverter.GetBytes(records[i].FileSize);
                Array.Copy(sizeBytes, 0, indexData, offset + 24, 4);
            }

            // Encode if protected
            byte[] finalData;
            if (isProtected)
            {
                byte[] encoded = L1PakTools.Encode(indexData, 0);
                // Add 4-byte header (record count)
                finalData = new byte[4 + encoded.Length];
                byte[] countBytes = BitConverter.GetBytes(records.Length);
                Array.Copy(countBytes, 0, finalData, 0, 4);
                Array.Copy(encoded, 0, finalData, 4, encoded.Length);
            }
            else
            {
                // Add 4-byte header (record count)
                finalData = new byte[4 + indexData.Length];
                byte[] countBytes = BitConverter.GetBytes(records.Length);
                Array.Copy(countBytes, 0, finalData, 0, 4);
                Array.Copy(indexData, 0, finalData, 4, indexData.Length);
            }

            File.WriteAllBytes(idxFile, finalData);
        }
    }
}
