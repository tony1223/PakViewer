using System;
using System.IO;
using System.Linq;
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

                case "delete":
                    if (args.Length < 3) { ShowHelp(); return; }
                    // Collect all filenames to delete (args[2] onwards)
                    string[] filesToDelete = new string[args.Length - 2];
                    Array.Copy(args, 2, filesToDelete, 0, args.Length - 2);
                    DeleteFiles(args[1], filesToDelete);
                    break;

                case "add":
                    if (args.Length < 3) { ShowHelp(); return; }
                    // Collect all file paths to add (args[2] onwards)
                    string[] filesToAdd = new string[args.Length - 2];
                    Array.Copy(args, 2, filesToAdd, 0, args.Length - 2);
                    AddFiles(args[1], filesToAdd);
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
            Console.WriteLine("  delete <idx_file> <filename1> [filename2] [filename3] ...");
            Console.WriteLine("    Delete one or more files from PAK. Supports batch deletion.");
            Console.WriteLine();
            Console.WriteLine("  add <idx_file> <file_path1> [file_path2] [file_path3] ...");
            Console.WriteLine("    Add one or more files to PAK. Files are appended to the end.");
            Console.WriteLine("    Checks for duplicate filenames and skips existing files.");
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
            Console.WriteLine("  PakReader delete Text.idx 07bearNPC-c.html");
            Console.WriteLine("  PakReader delete Text.idx file1.html file2.xml file3.txt");
            Console.WriteLine("  PakReader add Text.idx newfile.xml");
            Console.WriteLine("  PakReader add Text.idx file1.html file2.xml file3.txt");
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
            using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

            // Determine encoding - 對 XML 檔案優先從內容解析 encoding 聲明
            Encoding encoding;
            bool isXml = Path.GetExtension(targetFile).ToLower() == ".xml";
            if (isXml)
            {
                encoding = !string.IsNullOrEmpty(encodingName)
                    ? Encoding.GetEncoding(encodingName)
                    : XmlCracker.GetXmlEncoding(pakData, targetFile);
            }
            else
            {
                encoding = GetEncoding(targetFile, encodingName);
            }

            Console.WriteLine();
            Console.WriteLine($"Encoding: {encoding.EncodingName}" + (isXml ? " (from XML declaration)" : ""));
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
            using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

            // Check if original file was XML encrypted and get original encoding
            bool isXmlEncrypted = false;
            Encoding originalXmlEncoding = null;
            bool isXml = Path.GetExtension(targetFile).ToLower() == ".xml";

            if (isXml)
            {
                byte[] originalData;
                using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

                // 取得原始 XML 的 encoding
                byte[] decryptedOriginal = isXmlEncrypted ? XmlCracker.Decrypt((byte[])originalData.Clone()) : originalData;
                originalXmlEncoding = XmlCracker.GetXmlEncoding(decryptedOriginal, targetFile);
            }

            // Read input file
            byte[] inputData = File.ReadAllBytes(inputFile);

            // 如果是 XML，檢查輸入檔案的編碼並轉換
            Encoding inputXmlEncoding = null;
            if (isXml)
            {
                inputXmlEncoding = XmlCracker.GetXmlEncoding(inputData, targetFile);

                // 如果輸入編碼與原始編碼不同，進行轉換
                if (originalXmlEncoding != null && inputXmlEncoding != null &&
                    originalXmlEncoding.CodePage != inputXmlEncoding.CodePage)
                {
                    Console.WriteLine($"Encoding conversion: {inputXmlEncoding.EncodingName} -> {originalXmlEncoding.EncodingName}");
                    string content = inputXmlEncoding.GetString(inputData);
                    inputData = originalXmlEncoding.GetBytes(content);
                }
            }

            Console.WriteLine($"Target: {targetFile}");
            Console.WriteLine($"Input: {inputFile}");
            Console.WriteLine($"Original size: {rec.FileSize} bytes");
            Console.WriteLine($"New size: {inputData.Length} bytes");
            if (isXml)
            {
                Console.WriteLine($"Input XML Encoding: {inputXmlEncoding?.EncodingName ?? "N/A"}");
                Console.WriteLine($"Target XML Encoding: {originalXmlEncoding?.EncodingName ?? "N/A"}");
            }
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
            try
            {
                using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Write))
                {
                    fs.Seek(rec.Offset, SeekOrigin.Begin);
                    fs.Write(dataToWrite, 0, dataToWrite.Length);
                }

                Console.WriteLine();
                Console.WriteLine($"Success! Imported {inputFile} -> {targetFile}");
            }
            catch (IOException ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error: Cannot write to file. The file may be in use by another program.");
                Console.WriteLine("Please close Lineage game or other editors and try again.");
                Console.WriteLine($"Details: {ex.Message}");
            }
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

        static void DeleteFiles(string idxFile, string[] filesToDelete)
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

            Console.WriteLine($"Files to delete: {filesToDelete.Length}");
            Console.WriteLine();

            // Find all files to delete and their indices
            var filesToDeleteInfo = new System.Collections.Generic.List<(int index, L1PakTools.IndexRecord record)>();
            var notFoundFiles = new System.Collections.Generic.List<string>();

            foreach (string targetFile in filesToDelete)
            {
                bool found = false;
                for (int i = 0; i < records.Length; i++)
                {
                    if (records[i].FileName.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                    {
                        filesToDeleteInfo.Add((i, records[i]));
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    notFoundFiles.Add(targetFile);
                }
            }

            // Show not found files
            if (notFoundFiles.Count > 0)
            {
                Console.WriteLine("Files not found:");
                foreach (string file in notFoundFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
                Console.WriteLine();
            }

            // Show files to delete
            if (filesToDeleteInfo.Count == 0)
            {
                Console.WriteLine("No files to delete.");
                return;
            }

            Console.WriteLine($"Found {filesToDeleteInfo.Count} file(s) to delete:");
            foreach (var (index, record) in filesToDeleteInfo)
            {
                Console.WriteLine($"  [{index + 1}] {record.FileName} (Size: {record.FileSize}, Offset: 0x{record.Offset:X8})");
            }
            Console.WriteLine();

            // Sort by offset (descending) for deletion
            filesToDeleteInfo.Sort((a, b) => b.record.Offset.CompareTo(a.record.Offset));

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

                // Create new records list (excluding deleted files)
                var newRecordsList = new System.Collections.Generic.List<L1PakTools.IndexRecord>();
                var deleteIndices = new System.Collections.Generic.HashSet<int>();
                foreach (var (index, _) in filesToDeleteInfo)
                {
                    deleteIndices.Add(index);
                }

                for (int i = 0; i < records.Length; i++)
                {
                    if (!deleteIndices.Contains(i))
                    {
                        newRecordsList.Add(records[i]);
                    }
                }

                // Build new PAK file data by copying segments
                var newPakData = new System.Collections.Generic.List<byte>();
                int currentOffset = 0;

                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];

                    // Check if this is a file to delete
                    bool shouldDelete = deleteIndices.Contains(i);

                    if (!shouldDelete)
                    {
                        // Copy data from PAK
                        byte[] fileData = new byte[rec.FileSize];
                        Array.Copy(pakData, rec.Offset, fileData, 0, rec.FileSize);
                        newPakData.AddRange(fileData);

                        // Update offset for this record
                        int recordIndex = newRecordsList.FindIndex(r =>
                            r.FileName == rec.FileName &&
                            r.Offset == rec.Offset &&
                            r.FileSize == rec.FileSize);

                        if (recordIndex >= 0)
                        {
                            newRecordsList[recordIndex] = new L1PakTools.IndexRecord(
                                rec.FileName,
                                rec.FileSize,
                                currentOffset
                            );
                        }

                        currentOffset += rec.FileSize;
                    }
                }

                // Write new PAK file
                File.WriteAllBytes(pakFile, newPakData.ToArray());
                Console.WriteLine($"PAK file rebuilt: {pakFile}");

                // Rebuild IDX file
                RebuildIndex(idxFile, newRecordsList.ToArray(), isProtected);
                Console.WriteLine($"IDX file rebuilt: {idxFile}");

                // Delete backups on success
                File.Delete(pakBackup);
                File.Delete(idxBackup);
                Console.WriteLine("Backups removed (success)");

                Console.WriteLine();
                Console.WriteLine($"Success! Deleted {filesToDeleteInfo.Count} file(s).");
                Console.WriteLine($"New total: {newRecordsList.Count} files");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deletion: {ex.Message}");
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
            }
        }

        static void AddFiles(string idxFile, string[] filePaths)
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

            Console.WriteLine($"Files to add: {filePaths.Length}");
            Console.WriteLine();

            // Validate files and check for duplicates
            var filesToAddInfo = new System.Collections.Generic.List<(string filePath, string fileName, long fileSize)>();
            var notFoundFiles = new System.Collections.Generic.List<string>();
            var duplicateFiles = new System.Collections.Generic.List<string>();

            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    notFoundFiles.Add(filePath);
                    continue;
                }

                string fileName = Path.GetFileName(filePath);

                // Check filename length (max 20 bytes including null terminator)
                if (Encoding.Default.GetByteCount(fileName) > 19)
                {
                    Console.WriteLine($"Warning: Filename too long (max 19 bytes): {fileName}");
                    continue;
                }

                // Check if filename already exists in PAK
                bool exists = false;
                foreach (var rec in records)
                {
                    if (rec.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        duplicateFiles.Add(fileName);
                        break;
                    }
                }

                if (!exists)
                {
                    var fileInfo = new FileInfo(filePath);
                    filesToAddInfo.Add((filePath, fileName, fileInfo.Length));
                }
            }

            // Show validation results
            if (notFoundFiles.Count > 0)
            {
                Console.WriteLine("Files not found:");
                foreach (string file in notFoundFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
                Console.WriteLine();
            }

            if (duplicateFiles.Count > 0)
            {
                Console.WriteLine("Files already exist in PAK (skipped):");
                foreach (string file in duplicateFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
                Console.WriteLine();
            }

            if (filesToAddInfo.Count == 0)
            {
                Console.WriteLine("No files to add.");
                return;
            }

            Console.WriteLine($"Adding {filesToAddInfo.Count} file(s):");
            foreach (var (filePath, fileName, fileSize) in filesToAddInfo)
            {
                Console.WriteLine($"  - {fileName} ({fileSize} bytes) from {filePath}");
            }
            Console.WriteLine();

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
                // Get current PAK file size (this will be the offset for the first new file)
                var pakFileInfo = new FileInfo(pakFile);
                int currentOffset = (int)pakFileInfo.Length;

                // Create new records list
                var newRecordsList = new System.Collections.Generic.List<L1PakTools.IndexRecord>(records);

                // Process each file and append to PAK
                using (FileStream pakFs = File.Open(pakFile, FileMode.Append, FileAccess.Write))
                {
                    foreach (var (filePath, fileName, fileSize) in filesToAddInfo)
                    {
                        Console.WriteLine($"Processing: {fileName}");

                        // Read file data
                        byte[] fileData = File.ReadAllBytes(filePath);

                        // Check if this is an XML file that should be encrypted
                        bool isXml = Path.GetExtension(fileName).ToLower() == ".xml";
                        bool shouldEncryptXml = false;

                        // For XML files, check if other XML files in the PAK are encrypted
                        if (isXml)
                        {
                            // Sample check: see if any existing XML file is encrypted
                            foreach (var rec in records)
                            {
                                if (Path.GetExtension(rec.FileName).ToLower() == ".xml")
                                {
                                    // Read a sample to check
                                    byte[] sampleData;
                                    using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    {
                                        sampleData = new byte[Math.Min(rec.FileSize, 100)];
                                        fs.Seek(rec.Offset, SeekOrigin.Begin);
                                        fs.Read(sampleData, 0, sampleData.Length);
                                    }
                                    if (isProtected)
                                    {
                                        sampleData = L1PakTools.Decode(sampleData, 0);
                                    }
                                    if (XmlCracker.IsEncrypted(sampleData))
                                    {
                                        shouldEncryptXml = true;
                                        break;
                                    }
                                }
                            }
                        }

                        byte[] dataToWrite = fileData;

                        // 對 XML 檔案顯示 encoding 資訊
                        if (isXml)
                        {
                            var xmlEncoding = XmlCracker.GetXmlEncoding(fileData, fileName);
                            Console.WriteLine($"  XML Encoding: {xmlEncoding.EncodingName}");
                        }

                        // Encrypt XML if needed
                        if (shouldEncryptXml)
                        {
                            dataToWrite = XmlCracker.Encrypt(dataToWrite);
                            Console.WriteLine($"  XML encrypted: {dataToWrite.Length} bytes");
                        }

                        // Encode if L1 protected
                        if (isProtected)
                        {
                            dataToWrite = L1PakTools.Encode(dataToWrite, 0);
                            Console.WriteLine($"  L1 encoded: {dataToWrite.Length} bytes");
                        }

                        // Write to PAK
                        pakFs.Write(dataToWrite, 0, dataToWrite.Length);

                        // Add new record
                        newRecordsList.Add(new L1PakTools.IndexRecord(
                            fileName,
                            dataToWrite.Length,
                            currentOffset
                        ));

                        Console.WriteLine($"  Written at offset: 0x{currentOffset:X8} (size: {dataToWrite.Length})");

                        currentOffset += dataToWrite.Length;
                    }
                }

                Console.WriteLine($"PAK file updated: {pakFile}");

                // Rebuild IDX file
                RebuildIndex(idxFile, newRecordsList.ToArray(), isProtected);
                Console.WriteLine($"IDX file rebuilt: {idxFile}");

                // Delete backups on success
                File.Delete(pakBackup);
                File.Delete(idxBackup);
                Console.WriteLine("Backups removed (success)");

                Console.WriteLine();
                Console.WriteLine($"Success! Added {filesToAddInfo.Count} file(s).");
                Console.WriteLine($"New total: {newRecordsList.Count} files");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during addition: {ex.Message}");
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
            }
        }
    }
}
