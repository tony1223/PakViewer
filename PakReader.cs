using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PakViewer.Models;
using PakViewer.Utility;
using Lin.Helper.Core.Tile;
using SprListParser = Lin.Helper.Core.Sprite.SprListParser;
using SprListWriter = Lin.Helper.Core.Sprite.SprListWriter;

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

                case "sprlist":
                    if (args.Length < 2) { ShowHelp(); return; }
                    ParseSprList(args[1], args.Length > 2 ? args[2] : null);
                    break;

                case "sprtxt":
                    // 測試 sprtxt save/load: sprtxt <sprlist_file> <entry_id> [output.sprtxt]
                    if (args.Length < 3) { Console.WriteLine("Usage: sprtxt <sprlist_file> <entry_id> [output.sprtxt]"); return; }
                    TestSprtxt(args[1], args[2], args.Length > 3 ? args[3] : null);
                    break;

                case "sprtest":
                    // 測試 sprite 載入: sprtest <client_folder> <sprite_key>
                    // 例如: sprtest "C:\lineage\client" "3225-5"
                    if (args.Length < 3) { Console.WriteLine("Usage: sprtest <client_folder> <sprite_key>"); return; }
                    TestSpriteLoad(args[1], args[2]);
                    break;

                case "testdel":
                    // 測試批次刪除: testdel <client_folder>
                    if (args.Length < 2) { Console.WriteLine("Usage: testdel <client_folder>"); return; }
                    TestBatchDelete(args[1]);
                    break;

                case "cleanup":
                    // 清理未連結的大型 sprite: cleanup <client_folder> <list.spr> [min_size_mb]
                    if (args.Length < 3) { Console.WriteLine("Usage: cleanup <client_folder> <list.spr> [min_size_mb=1]"); return; }
                    int minSizeMB = args.Length > 3 && int.TryParse(args[3], out int mb) ? mb : 1;
                    CleanupUnlinkedSprites(args[1], args[2], minSizeMB);
                    break;

                case "listdes":
                    // 使用 DES 解密列出檔案: listdes <idx_file> [filter]
                    if (args.Length < 2) { ShowHelp(); return; }
                    ListFilesDES(args[1], args.Length > 2 ? args[2] : null);
                    break;

                case "listauto":
                    // 自動偵測加密方式列出檔案: listauto <idx_file> [filter]
                    if (args.Length < 2) { ShowHelp(); return; }
                    ListFilesAuto(args[1], args.Length > 2 ? args[2] : null);
                    break;

                case "batch-export":
                    // 批次匯出 SPR 檔案: batch-export <client_folder> <output_folder> <spr_ids> [parallel=8]
                    // spr_ids 可以是逗號分隔的編號或檔案路徑
                    if (args.Length < 4) { Console.WriteLine("Usage: batch-export <client_folder> <output_folder> <spr_ids_or_file> [parallel=8]"); return; }
                    int parallelCount = args.Length > 4 && int.TryParse(args[4], out int p) ? p : 8;
                    BatchExportSpr(args[1], args[2], args[3], parallelCount);
                    break;

                case "verify-sprite":
                    // 驗證 sprite 分布: verify-sprite <client_folder>
                    if (args.Length < 2) { Console.WriteLine("Usage: verify-sprite <client_folder>"); return; }
                    VerifySpriteDistributionCmd(args[1]);
                    break;

                case "verify-sort":
                    // 驗證 IDX 排序: verify-sort <idx_file> [comparer]
                    // comparer: ascii (預設), underscore (底線排在字母後)
                    if (args.Length < 2) { Console.WriteLine("Usage: verify-sort <idx_file> [ascii|underscore]"); return; }
                    string comparerType = args.Length > 2 ? args[2].ToLower() : "ascii";
                    VerifyIdxSortOrderCmd(args[1], comparerType);
                    break;

                case "sprdiff":
                    // 比對兩個客戶端資料夾的 Sprite 差異
                    // sprdiff <folder1> <folder2> [output_file]
                    if (args.Length < 3) { Console.WriteLine("Usage: sprdiff <folder1> <folder2> [output_file]"); return; }
                    CompareSpriteFiles(args[1], args[2], args.Length > 3 ? args[3] : null);
                    break;

                case "sprdiff-export":
                    // 從 diff 結果檔案批次匯出 SPR
                    // sprdiff-export <diff_file> <export_dir>
                    if (args.Length < 3) { Console.WriteLine("Usage: sprdiff-export <diff_file> <export_dir>"); return; }
                    ExportFromDiff(args[1], args[2]);
                    break;

                case "tildiff":
                    // 比對兩個客戶端資料夾的 Tile 差異
                    // tildiff <folder1> <folder2> [output_file]
                    if (args.Length < 3) { Console.WriteLine("Usage: tildiff <folder1> <folder2> [output_file]"); return; }
                    CompareTileFiles(args[1], args[2], args.Length > 3 ? args[3] : null);
                    break;

                case "tilinfo":
                    // 分析 TIL 檔案的 block type
                    // tilinfo <til_file>
                    if (args.Length < 2) { Console.WriteLine("Usage: tilinfo <til_file>"); return; }
                    AnalyzeTilFile(args[1]);
                    break;

                case "tildiff-export":
                    // 從 diff 結果檔案批次匯出 TIL
                    // tildiff-export <diff_file> <export_dir>
                    if (args.Length < 3) { Console.WriteLine("Usage: tildiff-export <diff_file> <export_dir>"); return; }
                    ExportFromDiff(args[1], args[2]);
                    break;

                case "pakdiff":
                    // 通用 PAK 差異比對
                    // pakdiff <folder1> <folder2> <idx_pattern> <extension> [output_file]
                    // 例如: pakdiff folder1 folder2 "Sprite*.idx" ".img" diff_img.txt
                    if (args.Length < 5) { Console.WriteLine("Usage: pakdiff <folder1> <folder2> <idx_pattern> <extension> [output_file]"); return; }
                    ComparePakFiles(args[1], args[2], args[3], args[4], args.Length > 5 ? args[5] : null);
                    break;

                case "mtil-convert":
                    // 批量轉換 M Tile 到 L1 Til 格式
                    // mtil-convert <input_dir> <output_dir> [pattern] [compression]
                    // pattern: 搜尋模式，預設 *.bin
                    // compression: none, zlib, brotli (預設 none)
                    if (args.Length < 3) { Console.WriteLine("Usage: mtil-convert <input_dir> <output_dir> [pattern] [compression]"); return; }
                    string pattern = args.Length > 3 ? args[3] : "*.bin";
                    string compStr = args.Length > 4 ? args[4].ToLower() : "none";
                    BatchConvertMTil(args[1], args[2], pattern, compStr);
                    break;

                case "mtil-debug":
                    // 診斷 MTil 檔案內容
                    // mtil-debug <mtil_file> [block_index]
                    if (args.Length < 2) { Console.WriteLine("Usage: mtil-debug <mtil_file> [block_index]"); return; }
                    int debugBlockIdx = args.Length > 2 ? int.Parse(args[2]) : -1;
                    DebugMTil(args[1], debugBlockIdx);
                    break;

                case "mtil-render-debug":
                    // 調試 MTil 渲染
                    if (args.Length < 3) { Console.WriteLine("Usage: mtil-render-debug <mtil_file> <block_index>"); return; }
                    DebugRenderBlock(args[1], int.Parse(args[2]));
                    break;

                case "til-md5":
                    // 計算 tile blocks 的 MD5
                    // til-md5 <til_file> [block_index]
                    if (args.Length < 2) { Console.WriteLine("Usage: til-md5 <til_file> [block_index]"); return; }
                    TilMd5(args[1], args.Length > 2 ? int.Parse(args[2]) : -1);
                    break;

                case "til-compare":
                    // 比較兩個 tile 檔案的 blocks
                    // til-compare <til_file1> <til_file2>
                    if (args.Length < 3) { Console.WriteLine("Usage: til-compare <til_file1> <til_file2>"); return; }
                    TilCompare(args[1], args[2]);
                    break;

                case "til-diff":
                    // 批量比較 til1 和 til2 目錄中的顏色差異
                    // til-diff <til1_dir> <til2_dir> [threshold] [max_tile_id]
                    if (args.Length < 3) { Console.WriteLine("Usage: til-diff <til1_dir> <til2_dir> [threshold] [max_tile_id]"); return; }
                    double threshold = args.Length > 3 ? double.Parse(args[3]) : 5.0;
                    int maxTileId = args.Length > 4 ? int.Parse(args[4]) : int.MaxValue;
                    TilColorDiff(args[1], args[2], threshold, maxTileId);
                    break;

                case "til-sheet":
                    // 輸出 tile 對比 sheet 圖
                    // til-sheet <til1_file> <til2_file> <output_png>
                    if (args.Length < 4) { Console.WriteLine("Usage: til-sheet <til1_file> <til2_file> <output_png>"); return; }
                    GenerateTileCompareSheet(args[1], args[2], args[3]);
                    break;

                case "til-sheet-batch":
                    // 批量輸出 tile 對比 sheet 圖 (只處理差異大的)
                    // til-sheet-batch <til1_dir> <til2_dir> <output_dir> [threshold] [max_tile_id]
                    if (args.Length < 4) { Console.WriteLine("Usage: til-sheet-batch <til1_dir> <til2_dir> <output_dir> [threshold] [max_tile_id]"); return; }
                    double sheetThreshold = args.Length > 4 ? double.Parse(args[4]) : 30.0;
                    int sheetMaxTileId = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
                    GenerateTileCompareSheetBatch(args[1], args[2], args[3], sheetThreshold, sheetMaxTileId);
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

        public static (L1PakTools.IndexRecord[] records, bool isProtected)? LoadIndex(string idxFile)
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

        /// <summary>
        /// DES ECB 加密的 idx 解析 (用於特定版本的 Text.idx 等)
        /// 密鑰: ~!@#%^$< (0x7e 0x21 0x40 0x23 0x25 0x5e 0x24 0x3c)
        /// </summary>
        public static (L1PakTools.IndexRecord[] records, bool isProtected)? LoadIndexDES(string idxFile)
        {
            if (!File.Exists(idxFile))
            {
                Console.WriteLine($"Error: IDX file not found: {idxFile}");
                return null;
            }

            byte[] idxData = File.ReadAllBytes(idxFile);

            // 驗證檔案格式: 前 4 bytes 是記錄數量
            if (idxData.Length < 32)
            {
                Console.WriteLine("Error: IDX file too small");
                return null;
            }

            int recordCount = BitConverter.ToInt32(idxData, 0);
            int expectedSize = 4 + recordCount * 28;

            if (idxData.Length != expectedSize)
            {
                Console.WriteLine($"Error: IDX file size mismatch. Expected {expectedSize}, got {idxData.Length}");
                return null;
            }

            // DES ECB 解密
            byte[] key = new byte[] { 0x7e, 0x21, 0x40, 0x23, 0x25, 0x5e, 0x24, 0x3c }; // ~!@#%^$<
            byte[] entriesData = new byte[idxData.Length - 4];
            Array.Copy(idxData, 4, entriesData, 0, entriesData.Length);

            using (var des = DES.Create())
            {
                des.Key = key;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;

                using (var decryptor = des.CreateDecryptor())
                {
                    // 每 8 bytes 解密一次
                    int blockCount = entriesData.Length / 8;
                    for (int i = 0; i < blockCount; i++)
                    {
                        int offset = i * 8;
                        byte[] block = new byte[8];
                        Array.Copy(entriesData, offset, block, 0, 8);
                        byte[] decrypted = decryptor.TransformFinalBlock(block, 0, 8);
                        Array.Copy(decrypted, 0, entriesData, offset, 8);
                    }
                }
            }

            // 解析記錄
            var records = new L1PakTools.IndexRecord[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                int offset = i * 28;
                records[i] = new L1PakTools.IndexRecord(entriesData, offset);
            }

            return (records, true); // DES 加密視為 protected
        }

        /// <summary>
        /// 檢測是否為 _EXTB$ 格式 (Extended Index Block)
        /// 用於 tile.idx 等擴展格式檔案
        /// </summary>
        public static bool IsExtBFormat(byte[] data)
        {
            if (data.Length < 16) return false;
            // Magic: "_EXTB$" (6 bytes)
            return data[0] == '_' && data[1] == 'E' && data[2] == 'X' &&
                   data[3] == 'T' && data[4] == 'B' && data[5] == '$';
        }

        /// <summary>
        /// 載入 _EXTB$ 格式的索引檔案 (Extended Index Block)
        /// 格式：
        /// - Header: 16 bytes (magic "_EXTB$" + metadata)
        /// - Entry: 128 bytes each
        ///   - Offset: 4 bytes (int32) - PAK 檔案內偏移量
        ///   - Compression: 4 bytes (int32) - 0=none, 1=zlib, 2=brotli
        ///   - FileName: 120 bytes (null-terminated string)
        /// </summary>
        public static (L1PakTools.IndexRecord[] records, bool isProtected)? LoadIndexExtB(string idxFile)
        {
            if (!File.Exists(idxFile))
            {
                Console.WriteLine($"Error: IDX file not found: {idxFile}");
                return null;
            }

            byte[] data = File.ReadAllBytes(idxFile);

            if (!IsExtBFormat(data))
            {
                Console.WriteLine("Error: Not an _EXTB$ format file");
                return null;
            }

            const int headerSize = 0x10;  // 16 bytes
            const int entrySize = 0x80;   // 128 bytes

            int entryCount = (data.Length - headerSize) / entrySize;
            var records = new List<L1PakTools.IndexRecord>();

            // Entry 結構 (128 bytes):
            // Offset 0-3:     Unknown (可能是排序用的 key)
            // Offset 4-7:     Compression (0=none, 1=zlib, 2=brotli)
            // Offset 8-119:   Filename (112 bytes, null-padded)
            // Offset 120-123: PAK Offset (真正的檔案位置)
            // Offset 124-127: Uncompressed Size
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = headerSize + i * entrySize;

                int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);  // 真正的 PAK offset
                int compression = BitConverter.ToInt32(data, entryOffset + 4);
                int fileSize = BitConverter.ToInt32(data, entryOffset + 124);   // Uncompressed size

                int nameStart = entryOffset + 8;
                int nameEnd = nameStart;
                while (nameEnd < entryOffset + 120 && data[nameEnd] != 0 &&
                       data[nameEnd] >= 32 && data[nameEnd] <= 126)
                {
                    nameEnd++;
                }

                if (nameEnd > nameStart)
                {
                    string fileName = Encoding.ASCII.GetString(data, nameStart, nameEnd - nameStart);
                    if (!string.IsNullOrEmpty(fileName) && fileName.Contains("."))
                    {
                        records.Add(new L1PakTools.IndexRecord(fileName, fileSize, pakOffset));
                    }
                }
            }

            return (records.ToArray(), false); // _EXTB$ 格式目前無加密
        }

        /// <summary>
        /// 從 PAK header 自動偵測壓縮類型
        /// </summary>
        private static int DetectExtBCompression(byte[] header)
        {
            if (header.Length >= 2)
            {
                // zlib: 78 9C, 78 DA, 78 01, 78 5E
                if (header[0] == 0x78 && (header[1] == 0x9C || header[1] == 0xDA ||
                    header[1] == 0x01 || header[1] == 0x5E))
                    return 1;  // zlib
                // brotli: 通常以 0x5B 或 0x1B 開頭
                if (header[0] == 0x5B || header[0] == 0x1B)
                    return 2;  // brotli
            }
            return 0;  // none/raw
        }

        /// <summary>
        /// 解壓縮 ExtB 格式資料
        /// </summary>
        private static byte[] DecompressExtBData(byte[] compressedData, int compressionType)
        {
            try
            {
                if (compressionType == 1) // zlib
                {
                    using (var ms = new MemoryStream(compressedData))
                    using (var zlib = new ZLibStream(ms, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        zlib.CopyTo(output);
                        return output.ToArray();
                    }
                }
                else if (compressionType == 2) // brotli
                {
                    using (var ms = new MemoryStream(compressedData))
                    using (var brotli = new BrotliStream(ms, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        brotli.CopyTo(output);
                        return output.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decompression failed: {ex.Message}");
            }
            return compressedData; // 解壓失敗則返回原始資料
        }

        /// <summary>
        /// 從 ExtB 格式的 IDX 資料建立排序的 offset 列表
        /// </summary>
        private static List<int> BuildExtBSortedOffsets(byte[] idxData)
        {
            const int headerSize = 0x10;
            const int entrySize = 0x80;
            int entryCount = (idxData.Length - headerSize) / entrySize;

            var offsets = new HashSet<int>();
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = headerSize + i * entrySize;
                int pakOffset = BitConverter.ToInt32(idxData, entryOffset + 120);
                offsets.Add(pakOffset);
            }

            var sorted = offsets.ToList();
            sorted.Sort();
            return sorted;
        }

        /// <summary>
        /// 計算 ExtB 格式中指定 offset 的壓縮大小
        /// </summary>
        private static int GetExtBCompressedSize(List<int> sortedOffsets, int offset, long pakFileSize)
        {
            int idx = sortedOffsets.BinarySearch(offset);
            if (idx < 0) return 0;

            if (idx + 1 < sortedOffsets.Count)
                return sortedOffsets[idx + 1] - offset;
            else
                return (int)(pakFileSize - offset);
        }

        /// <summary>
        /// 讀取 ExtB 格式的 PAK 資料 (自動解壓縮)
        /// </summary>
        public static byte[] ReadExtBPakData(string pakFile, byte[] idxData, L1PakTools.IndexRecord record)
        {
            var sortedOffsets = BuildExtBSortedOffsets(idxData);
            long pakFileSize = new FileInfo(pakFile).Length;
            int compressedSize = GetExtBCompressedSize(sortedOffsets, record.Offset, pakFileSize);

            if (compressedSize <= 0) return null;

            byte[] compressedData = new byte[compressedSize];
            using (var fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(record.Offset, SeekOrigin.Begin);
                fs.Read(compressedData, 0, compressedSize);
            }

            int compressionType = DetectExtBCompression(compressedData);
            if (compressionType > 0)
            {
                return DecompressExtBData(compressedData, compressionType);
            }
            else
            {
                // 無壓縮，截斷到正確大小
                if (compressedData.Length > record.FileSize && record.FileSize > 0)
                {
                    byte[] result = new byte[record.FileSize];
                    Array.Copy(compressedData, result, record.FileSize);
                    return result;
                }
                return compressedData;
            }
        }

        /// <summary>
        /// 檢測 idx 檔案是否使用 DES 加密
        /// 判斷條件: 解析出的 FileSize 為負數時，表示解密方式錯誤
        /// </summary>
        public static bool IsDESEncrypted(byte[] idxData)
        {
            // 檢查是否符合 idx 格式
            if (idxData.Length < 32) return false;

            int recordCount = BitConverter.ToInt32(idxData, 0);
            int expectedSize = 4 + recordCount * 28;

            if (idxData.Length != expectedSize) return false;

            // 嘗試不解密直接讀取，檢查 size 是否為負數
            // 記錄結構: offset(4) + filename(20) + size(4) = 28 bytes
            int firstSize = BitConverter.ToInt32(idxData, 4 + 24); // 第一條記錄的 size
            if (firstSize < 0)
            {
                return true; // 未加密但 size 負數，需要 DES
            }

            // 嘗試 L1PakTools 解碼，檢查 size 是否為負數
            try
            {
                var firstRecord = L1PakTools.Decode_Index_FirstRecord(idxData);
                if (firstRecord.FileSize < 0)
                {
                    return true; // L1 解密後 size 負數，需要 DES
                }
            }
            catch
            {
                return true; // L1PakTools 失敗，嘗試 DES
            }

            return false;
        }

        /// <summary>
        /// 檢查記錄陣列中是否有負數 size
        /// </summary>
        private static bool HasNegativeSize(L1PakTools.IndexRecord[] records)
        {
            foreach (var rec in records)
            {
                if (rec.FileSize < 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 自動檢測加密方式並載入 idx (供 GUI 使用)
        /// 支援格式：_EXTB$ (擴展格式)、L1 (標準加密)、DES (DES加密)、None (無加密)
        /// </summary>
        public static (L1PakTools.IndexRecord[] records, bool isProtected, string encryptionType)? LoadIndexAuto(string idxFile)
        {
            if (!File.Exists(idxFile))
            {
                return null;
            }

            byte[] data = File.ReadAllBytes(idxFile);

            // 優先檢測 _EXTB$ 格式
            if (IsExtBFormat(data))
            {
                var extbResult = LoadIndexExtB(idxFile);
                if (extbResult != null)
                {
                    return (extbResult.Value.records, false, "ExtB");
                }
            }

            // 嘗試原本的方式 (L1 或無加密)
            var result = LoadIndex(idxFile);
            if (result != null)
            {
                // 檢查是否有負數 size
                if (!HasNegativeSize(result.Value.records))
                {
                    return (result.Value.records, result.Value.isProtected, result.Value.isProtected ? "L1" : "None");
                }
                // 有負數 size，嘗試 DES
            }

            // 嘗試 DES 解密
            var desResult = LoadIndexDES(idxFile);
            if (desResult != null && !HasNegativeSize(desResult.Value.records))
            {
                return (desResult.Value.records, true, "DES");
            }

            // 都失敗，返回原本的結果（如果有的話）
            if (result != null)
            {
                return (result.Value.records, result.Value.isProtected, result.Value.isProtected ? "L1" : "None");
            }

            return null;
        }

        static void ShowInfo(string idxFile)
        {
            var autoResult = LoadIndexAuto(idxFile);
            if (autoResult == null)
            {
                Console.WriteLine("Error: Cannot decode index file");
                return;
            }

            var (records, isProtected, encryptionType) = autoResult.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            Console.WriteLine($"IDX File: {idxFile}");
            Console.WriteLine($"PAK File: {pakFile}");
            Console.WriteLine($"PAK Exists: {File.Exists(pakFile)}");
            Console.WriteLine($"Format: {encryptionType}");
            Console.WriteLine($"Total Records: {records.Length}");

            if (File.Exists(pakFile))
            {
                var fi = new FileInfo(pakFile);
                Console.WriteLine($"PAK Size: {fi.Length:N0} bytes");
            }
        }

        static void ListFiles(string idxFile, string filter)
        {
            // 使用自動檢測載入
            var autoResult = LoadIndexAuto(idxFile);
            if (autoResult == null)
            {
                Console.WriteLine("Error: Cannot decode index file");
                return;
            }

            var (records, isProtected, encryptionType) = autoResult.Value;

            Console.WriteLine($"Total: {records.Length} files");
            Console.WriteLine($"Format: {encryptionType}");
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

        static void ListFilesDES(string idxFile, string filter)
        {
            var result = LoadIndexDES(idxFile);
            if (result == null)
            {
                Console.WriteLine("Failed to load with DES decryption. Trying standard method...");
                result = LoadIndex(idxFile);
                if (result == null) return;
            }

            var (records, isProtected) = result.Value;

            Console.WriteLine($"Total: {records.Length} files");
            Console.WriteLine($"Encryption: DES ECB");
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

        static void ListFilesAuto(string idxFile, string filter)
        {
            var result = LoadIndexAuto(idxFile);
            if (result == null)
            {
                Console.WriteLine("Failed to load index file.");
                return;
            }

            var (records, isProtected, encryptionType) = result.Value;

            Console.WriteLine($"Total: {records.Length} files");
            Console.WriteLine($"Encryption: {encryptionType}");
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
            var result = LoadIndexAuto(idxFile);
            if (result == null) return;

            var (records, isProtected, encryptionType) = result.Value;
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
            Console.WriteLine($"Format: {encryptionType}");

            byte[] pakData;

            // ExtB 格式需要解壓縮
            if (encryptionType == "ExtB")
            {
                byte[] idxData = File.ReadAllBytes(idxFile);
                pakData = ReadExtBPakData(pakFile, idxData, rec);
                if (pakData == null)
                {
                    Console.WriteLine($"Error: Failed to read/decompress ExtB data");
                    return;
                }
                Console.WriteLine($"Decompressed Size: {pakData.Length} bytes");
            }
            else
            {
                // Read PAK data
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
            }

            // Decrypt XML files if encrypted (starts with 'X')
            if (Path.GetExtension(targetFile).ToLower() == ".xml" && XmlCracker.IsEncrypted(pakData))
            {
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
            var result = LoadIndexAuto(idxFile);
            if (result == null) return;

            var (records, isProtected, encryptionType) = result.Value;
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
            byte[] pakData;

            // ExtB 格式需要解壓縮
            if (encryptionType == "ExtB")
            {
                byte[] idxData = File.ReadAllBytes(idxFile);
                pakData = ReadExtBPakData(pakFile, idxData, rec);
                if (pakData == null)
                {
                    Console.WriteLine($"Error: Failed to read/decompress ExtB data");
                    return;
                }
            }
            else
            {
                // Read PAK data
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
            Console.WriteLine($"Format: {encryptionType}");
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

        /// <summary>
        /// Delete files from PAK (core function for GUI use)
        /// </summary>
        /// <param name="idxFile">Path to the IDX file</param>
        /// <param name="pakFile">Path to the PAK file</param>
        /// <param name="records">Array of index records</param>
        /// <param name="indicesToDelete">Array of indices to delete</param>
        /// <param name="isProtected">Whether the PAK is L1 protected</param>
        /// <returns>Tuple of (error message or null, new records array or null)</returns>
        public static (string error, L1PakTools.IndexRecord[] newRecords) DeleteFilesCore(
            string idxFile, string pakFile, L1PakTools.IndexRecord[] records, int[] indicesToDelete, bool isProtected)
        {
            // 建立要刪除的索引集合
            var deleteIndices = new System.Collections.Generic.HashSet<int>(indicesToDelete);

            // 建立保留的記錄清單
            var keepRecords = new System.Collections.Generic.List<L1PakTools.IndexRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                if (!deleteIndices.Contains(i))
                {
                    keepRecords.Add(records[i]);
                }
            }

            string tempPakFile = pakFile + ".tmp";
            string tempIdxFile = idxFile + ".tmp";

            try
            {
                // 寫入新 PAK
                var newRecords = new System.Collections.Generic.List<L1PakTools.IndexRecord>();
                int currentOffset = 0;

                using (var srcStream = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dstStream = new FileStream(tempPakFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var rec in keepRecords)
                    {
                        // 從原始 PAK 讀取檔案資料
                        byte[] fileData = new byte[rec.FileSize];
                        srcStream.Seek(rec.Offset, SeekOrigin.Begin);
                        srcStream.Read(fileData, 0, rec.FileSize);

                        // 寫入新 PAK
                        dstStream.Write(fileData, 0, fileData.Length);

                        // 建立新記錄（更新 offset）
                        newRecords.Add(new L1PakTools.IndexRecord(
                            rec.FileName,
                            rec.FileSize,
                            currentOffset
                        ));

                        currentOffset += rec.FileSize;
                    }
                }

                // 寫入新 IDX
                var newRecordsArray = newRecords.ToArray();
                RebuildIndex(tempIdxFile, newRecordsArray, isProtected);

                // 驗證暫存檔存在且大小正確
                if (!File.Exists(tempPakFile))
                {
                    return ($"暫存 PAK 檔案不存在: {tempPakFile}", null);
                }
                if (!File.Exists(tempIdxFile))
                {
                    return ($"暫存 IDX 檔案不存在: {tempIdxFile}", null);
                }

                long tempPakSize = new FileInfo(tempPakFile).Length;

                // 驗證暫存 PAK 大小合理 (應該 > 0 且 <= 原始大小)
                long originalPakSize = new FileInfo(pakFile).Length;
                if (tempPakSize == 0 && keepRecords.Count > 0)
                {
                    return ($"暫存 PAK 檔案大小為 0，但應該保留 {keepRecords.Count} 個檔案", null);
                }
                if (tempPakSize > originalPakSize)
                {
                    return ($"暫存 PAK 檔案大小 ({tempPakSize}) 大於原始檔案 ({originalPakSize})", null);
                }

                // 刪除舊檔，重命名新檔
                File.Delete(pakFile);
                File.Move(tempPakFile, pakFile);
                File.Delete(idxFile);
                File.Move(tempIdxFile, idxFile);

                return (null, newRecordsArray);
            }
            catch (Exception ex)
            {
                // 清理暫存檔
                if (File.Exists(tempPakFile)) File.Delete(tempPakFile);
                if (File.Exists(tempIdxFile)) File.Delete(tempIdxFile);

                return (ex.Message, null);
            }
        }

        /// <summary>
        /// 壓縮 PAK 中所有 PNG 檔案
        /// </summary>
        /// <param name="idxFile">IDX 檔案路徑</param>
        /// <param name="progress">進度回報 (已完成數, 總數, 目前檔名)</param>
        /// <returns>(成功數, 原始大小, 新大小, 錯誤訊息)</returns>
        public static (int successCount, long originalSize, long newSize, string error) OptimizePakPng(
            string idxFile, Action<int, int, string> progress = null)
        {
            string pakFile = idxFile.Replace(".idx", ".pak");

            if (!File.Exists(idxFile) || !File.Exists(pakFile))
                return (0, 0, 0, "找不到 IDX 或 PAK 檔案");

            var result = LoadIndex(idxFile);
            if (result == null)
                return (0, 0, 0, "無法讀取 IDX 檔案");

            var (records, isProtected) = result.Value;

            // 找出所有 PNG 檔案
            var pngIndices = new List<int>();
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    pngIndices.Add(i);
            }

            if (pngIndices.Count == 0)
                return (0, 0, 0, null); // 沒有 PNG，不是錯誤

            long originalPakSize = new FileInfo(pakFile).Length;
            int successCount = 0;
            var newRecords = new List<L1PakTools.IndexRecord>();
            var newDataList = new List<byte[]>();

            // 處理所有記錄
            using (var srcStream = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    byte[] fileData = new byte[rec.FileSize];
                    srcStream.Seek(rec.Offset, SeekOrigin.Begin);
                    srcStream.Read(fileData, 0, rec.FileSize);

                    bool isPng = rec.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

                    if (isPng)
                    {
                        progress?.Invoke(successCount + 1, pngIndices.Count, rec.FileName);

                        // 解碼（如果需要）
                        byte[] pngData = isProtected ? L1PakTools.Decode(fileData, 0) : fileData;

                        // 壓縮 PNG
                        var (optimizedData, savedBytes, error) = Utility.PngOptimizer.OptimizeData(pngData);

                        if (error == null && savedBytes > 0)
                        {
                            // 壓縮成功且有節省空間
                            fileData = isProtected ? L1PakTools.Encode(optimizedData, 0) : optimizedData;
                            successCount++;
                        }
                        else if (error == null)
                        {
                            // 已經是最佳，保持原樣
                            successCount++;
                        }
                        // 如果有錯誤，保留原始資料
                    }

                    newDataList.Add(fileData);
                    newRecords.Add(new L1PakTools.IndexRecord(rec.FileName, fileData.Length, 0));
                }
            }

            // 寫入新 PAK
            string tempPakFile = pakFile + ".tmp";
            string tempIdxFile = idxFile + ".tmp";

            try
            {
                int currentOffset = 0;
                using (var dstStream = new FileStream(tempPakFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    for (int i = 0; i < newDataList.Count; i++)
                    {
                        var data = newDataList[i];
                        dstStream.Write(data, 0, data.Length);

                        newRecords[i] = new L1PakTools.IndexRecord(
                            newRecords[i].FileName,
                            data.Length,
                            currentOffset);

                        currentOffset += data.Length;
                    }
                }

                // 寫入新 IDX
                RebuildIndex(tempIdxFile, newRecords.ToArray(), isProtected);

                // 驗證並替換
                if (!File.Exists(tempPakFile) || !File.Exists(tempIdxFile))
                    return (0, 0, 0, "暫存檔建立失敗");

                long newPakSize = new FileInfo(tempPakFile).Length;

                File.Delete(pakFile);
                File.Move(tempPakFile, pakFile);
                File.Delete(idxFile);
                File.Move(tempIdxFile, idxFile);

                return (successCount, originalPakSize, newPakSize, null);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPakFile)) File.Delete(tempPakFile);
                if (File.Exists(tempIdxFile)) File.Delete(tempIdxFile);
                return (0, 0, 0, ex.Message);
            }
        }

        static void CleanupUnlinkedSprites(string folder, string listSprPath, int minSizeMB)
        {
            Console.WriteLine($"=== 清理未連結的大型 Sprite ===");
            Console.WriteLine($"資料夾: {folder}");
            Console.WriteLine($"List.spr: {listSprPath}");
            Console.WriteLine($"最小 size: {minSizeMB} MB (整個 SpriteId 合計)");

            // 1. 讀取 list.spr，收集所有連結的 SpriteId
            var linkedSpriteIds = new HashSet<int>();
            try
            {
                var listSpr = SprListParser.LoadFromFile(listSprPath);
                foreach (var entry in listSpr.Entries)
                {
                    linkedSpriteIds.Add(entry.SpriteId);
                }
                Console.WriteLine($"List.spr 連結了 {linkedSpriteIds.Count} 個 SpriteId");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"無法讀取 list.spr: {ex.Message}");
                return;
            }

            // 2. 找所有 Sprite*.idx，掃描所有檔案，計算每個 SpriteId 的總 size
            var idxFiles = Directory.GetFiles(folder, "Sprite*.idx");
            Console.WriteLine($"找到 {idxFiles.Length} 個 idx 檔案");

            // SpriteId -> 總 size
            var spriteIdTotalSize = new Dictionary<int, long>();
            // SpriteId -> List of (idxFile, recordIndex, fileSize)
            var spriteIdFiles = new Dictionary<int, List<(string idxFile, int recordIndex, int fileSize)>>();

            foreach (var idxFile in idxFiles)
            {
                string pakFile = idxFile.Replace(".idx", ".pak");
                if (!File.Exists(pakFile)) continue;

                var result = LoadIndex(idxFile);
                if (result == null) continue;

                var (records, isProtected) = result.Value;

                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    if (!rec.FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 取得 SpriteId (檔名格式: "1234-5.spr" -> SpriteId = 1234)
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(rec.FileName);
                    int dashIdx = nameWithoutExt.IndexOf('-');
                    if (dashIdx <= 0) continue;

                    if (!int.TryParse(nameWithoutExt.Substring(0, dashIdx), out int spriteId))
                        continue;

                    // 累計 size
                    if (!spriteIdTotalSize.ContainsKey(spriteId))
                    {
                        spriteIdTotalSize[spriteId] = 0;
                        spriteIdFiles[spriteId] = new List<(string, int, int)>();
                    }
                    spriteIdTotalSize[spriteId] += rec.FileSize;
                    spriteIdFiles[spriteId].Add((idxFile, i, rec.FileSize));
                }
            }

            Console.WriteLine($"掃描到 {spriteIdTotalSize.Count} 個不同的 SpriteId");

            // 3. 找出未連結且總 size > 閾值的 SpriteId
            long minSizeBytes = minSizeMB * 1024L * 1024L;
            var spriteIdsToDelete = new HashSet<int>();

            foreach (var kvp in spriteIdTotalSize)
            {
                int spriteId = kvp.Key;
                long totalSize = kvp.Value;

                if (!linkedSpriteIds.Contains(spriteId) && totalSize >= minSizeBytes)
                {
                    spriteIdsToDelete.Add(spriteId);
                }
            }

            Console.WriteLine($"找到 {spriteIdsToDelete.Count} 個未連結且 >= {minSizeMB}MB 的 SpriteId");

            // 顯示前 10 個要刪除的 SpriteId
            int shown = 0;
            foreach (var spriteId in spriteIdsToDelete.OrderByDescending(id => spriteIdTotalSize[id]))
            {
                if (shown >= 10) { Console.WriteLine("  ..."); break; }
                var files = spriteIdFiles[spriteId];
                Console.WriteLine($"  SpriteId {spriteId}: {files.Count} 個檔案, {spriteIdTotalSize[spriteId] / 1024.0 / 1024.0:F2} MB");
                shown++;
            }

            if (spriteIdsToDelete.Count == 0)
            {
                Console.WriteLine("\n無需刪除任何檔案");
                return;
            }

            // 4. 按 PAK 檔案分組要刪除的索引
            // idxFile -> List of recordIndex
            var deleteByPak = new Dictionary<string, List<int>>();

            foreach (var spriteId in spriteIdsToDelete)
            {
                foreach (var (idxFile, recordIndex, _) in spriteIdFiles[spriteId])
                {
                    if (!deleteByPak.ContainsKey(idxFile))
                        deleteByPak[idxFile] = new List<int>();
                    deleteByPak[idxFile].Add(recordIndex);
                }
            }

            // 5. 執行刪除
            int totalDeleted = 0;
            long totalSizeDeleted = 0;

            foreach (var kvp in deleteByPak)
            {
                string idxFile = kvp.Key;
                var toDelete = kvp.Value;

                string pakFile = idxFile.Replace(".idx", ".pak");
                var result = LoadIndex(idxFile);
                if (result == null) continue;

                var (records, isProtected) = result.Value;

                long sizeToDelete = toDelete.Sum(i => (long)records[i].FileSize);

                Console.WriteLine($"\n{Path.GetFileName(idxFile)}: 刪除 {toDelete.Count} 個檔案 ({sizeToDelete / 1024.0 / 1024.0:F1} MB)");

                var (error, newRecords) = DeleteFilesCore(idxFile, pakFile, records, toDelete.ToArray(), isProtected);

                if (error != null)
                {
                    Console.WriteLine($"  錯誤: {error}");
                }
                else
                {
                    Console.WriteLine($"  成功!");
                    totalDeleted += toDelete.Count;
                    totalSizeDeleted += sizeToDelete;
                }
            }

            Console.WriteLine($"\n=== 清理完成 ===");
            Console.WriteLine($"刪除 {spriteIdsToDelete.Count} 個 SpriteId，共 {totalDeleted} 個檔案，釋放 {totalSizeDeleted / 1024.0 / 1024.0:F1} MB");
        }

        static void TestBatchDelete(string folder)
        {
            Console.WriteLine($"=== 測試批次刪除 ===");
            Console.WriteLine($"資料夾: {folder}");

            // 找所有 Sprite*.idx
            var idxFiles = Directory.GetFiles(folder, "Sprite*.idx");
            Console.WriteLine($"找到 {idxFiles.Length} 個 idx 檔案");

            var random = new Random();

            // 每個 PAK 隨機刪除 3-5 個 .spr 檔案
            foreach (var idxFile in idxFiles.Take(3)) // 只測試前 3 個
            {
                string pakFile = idxFile.Replace(".idx", ".pak");
                if (!File.Exists(pakFile))
                {
                    Console.WriteLine($"跳過: {Path.GetFileName(idxFile)} (pak 不存在)");
                    continue;
                }

                var result = LoadIndex(idxFile);
                if (result == null)
                {
                    Console.WriteLine($"跳過: {Path.GetFileName(idxFile)} (無法載入)");
                    continue;
                }

                var (records, isProtected) = result.Value;
                Console.WriteLine($"\n處理: {Path.GetFileName(idxFile)}");
                Console.WriteLine($"  記錄數: {records.Length}, 加密: {isProtected}");
                Console.WriteLine($"  PAK 大小: {new FileInfo(pakFile).Length}");
                Console.WriteLine($"  IDX 大小: {new FileInfo(idxFile).Length}");

                // 找出所有 .spr 檔案的索引
                var sprIndices = new System.Collections.Generic.List<int>();
                for (int i = 0; i < records.Length; i++)
                {
                    if (records[i].FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                    {
                        sprIndices.Add(i);
                    }
                }

                if (sprIndices.Count < 5)
                {
                    Console.WriteLine($"  跳過: spr 檔案太少 ({sprIndices.Count})");
                    continue;
                }

                // 隨機選 3-5 個刪除
                int deleteCount = random.Next(3, 6);
                var toDelete = sprIndices.OrderBy(x => random.Next()).Take(deleteCount).ToArray();

                Console.WriteLine($"  要刪除 {toDelete.Length} 個檔案:");
                foreach (var idx in toDelete)
                {
                    Console.WriteLine($"    [{idx}] {records[idx].FileName}");
                }

                var (error, newRecords) = DeleteFilesCore(idxFile, pakFile, records, toDelete, isProtected);

                if (error != null)
                {
                    Console.WriteLine($"  錯誤: {error}");
                }
                else
                {
                    Console.WriteLine($"  成功! 新記錄數: {newRecords.Length}");
                    Console.WriteLine($"  新 PAK 大小: {new FileInfo(pakFile).Length}");
                    Console.WriteLine($"  新 IDX 大小: {new FileInfo(idxFile).Length}");
                }
            }

            Console.WriteLine("\n=== 測試完成 ===");
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

        static void ParseSprList(string filePath, string entryIdStr)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found: {filePath}");
                return;
            }

            Console.WriteLine($"Parsing: {filePath}");
            Console.WriteLine();

            try
            {
                var sprList = SprListParser.LoadFromFile(filePath);

                Console.WriteLine($"Header: TotalEntries={sprList.TotalEntries}, Unknown1={sprList.Unknown1}, Unknown2={sprList.Unknown2}");
                Console.WriteLine($"Parsed Entries: {sprList.Entries.Count}");
                Console.WriteLine();

                // 如果指定了 entryId，顯示該條目詳細資訊
                if (!string.IsNullOrEmpty(entryIdStr) && int.TryParse(entryIdStr, out int entryId))
                {
                    var entry = sprList.Entries.FirstOrDefault(e => e.Id == entryId);
                    if (entry == null)
                    {
                        Console.WriteLine($"Entry #{entryId} not found.");
                        return;
                    }

                    Console.WriteLine($"=== Entry #{entry.Id} ===");
                    Console.WriteLine($"Name: {entry.Name}");
                    Console.WriteLine($"ImageCount: {entry.ImageCount}");
                    Console.WriteLine($"LinkedId: {entry.LinkedId}");
                    Console.WriteLine($"TypeId: {entry.TypeId} ({entry.TypeName})");
                    Console.WriteLine($"ShadowId: {entry.ShadowId}");
                    Console.WriteLine($"Actions: {entry.Actions.Count}");
                    Console.WriteLine($"Attributes: {entry.Attributes.Count}");
                    Console.WriteLine();

                    // 顯示動作
                    foreach (var action in entry.Actions)
                    {
                        Console.WriteLine($"--- Action {action.ActionId}.{action.ActionName} ---");
                        Console.WriteLine($"  Directional: {action.Directional} (IsDirectional: {action.IsDirectional})");
                        Console.WriteLine($"  FrameCount: {action.FrameCount}");
                        Console.WriteLine($"  Frames.Count: {action.Frames.Count}");
                        Console.WriteLine($"  RawText: {action.RawText}");

                        // 顯示前幾幀
                        int showCount = Math.Min(action.Frames.Count, 8);
                        for (int i = 0; i < showCount; i++)
                        {
                            var frame = action.Frames[i];
                            Console.WriteLine($"    Frame[{i}]: ImageId={frame.ImageId}, FrameIndex={frame.FrameIndex}, Duration={frame.Duration}, SoundIds=[{string.Join(",", frame.SoundIds)}]");
                        }
                        if (action.Frames.Count > showCount)
                        {
                            Console.WriteLine($"    ... and {action.Frames.Count - showCount} more frames");
                        }
                        Console.WriteLine();
                    }

                    // 顯示屬性
                    Console.WriteLine("--- Attributes ---");
                    foreach (var attr in entry.Attributes)
                    {
                        Console.WriteLine($"  {attr.AttributeId}.{attr.AttributeName}({attr.RawParameters})");
                    }
                }
                else
                {
                    // 列出前 20 個條目
                    Console.WriteLine("First 20 entries:");
                    foreach (var entry in sprList.Entries.Take(20))
                    {
                        Console.WriteLine($"  #{entry.Id} {entry.Name} (ImageCount={entry.ImageCount}, Actions={entry.Actions.Count}, Type={entry.TypeName})");
                    }

                    if (sprList.Entries.Count > 20)
                    {
                        Console.WriteLine($"  ... and {sprList.Entries.Count - 20} more entries");
                    }

                    Console.WriteLine();
                    Console.WriteLine("Usage: sprlist <file> <entry_id> to see details");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void TestSprtxt(string sprListFile, string entryIdStr, string outputPath)
        {
            if (!File.Exists(sprListFile))
            {
                Console.WriteLine($"Error: File not found: {sprListFile}");
                return;
            }

            if (!int.TryParse(entryIdStr, out int entryId))
            {
                Console.WriteLine($"Error: Invalid entry ID: {entryIdStr}");
                return;
            }

            Console.WriteLine($"=== SprListWriter 測試 ===\n");

            try
            {
                // 1. Parse
                Console.WriteLine($"1. 解析 {Path.GetFileName(sprListFile)}...");
                var sprFile = SprListParser.LoadFromFile(sprListFile);
                Console.WriteLine($"   Entries: {sprFile.Entries.Count}");

                // 2. 取得指定 Entry
                var entry = sprFile.Entries.FirstOrDefault(e => e.Id == entryId);
                if (entry == null)
                {
                    Console.WriteLine($"   Error: Entry #{entryId} not found");
                    return;
                }
                Console.WriteLine($"   Found: #{entry.Id} {entry.Name} (Actions={entry.Actions.Count}, Attrs={entry.Attributes.Count})");

                // 3. 輸出 sprtxt
                string sprtxtPath = outputPath ?? $"entry_{entryId}.sprtxt";
                Console.WriteLine($"\n2. SaveEntry -> {sprtxtPath}");
                SprListWriter.SaveEntry(entry, sprtxtPath, compact: false);

                string content = File.ReadAllText(sprtxtPath);
                Console.WriteLine($"   Size: {content.Length} bytes");
                Console.WriteLine($"   Preview:\n---");
                Console.WriteLine(content.Length > 500 ? content.Substring(0, 500) + "..." : content);
                Console.WriteLine("---");

                // 4. LoadEntry 並比較
                Console.WriteLine($"\n3. LoadEntry <- {sprtxtPath}");
                var loaded = SprListWriter.LoadEntry(sprtxtPath);

                Console.WriteLine($"   比較結果:");
                Console.WriteLine($"     Id: {entry.Id} vs {loaded.Id} = {(entry.Id == loaded.Id ? "OK" : "FAIL")}");
                Console.WriteLine($"     Name: \"{entry.Name}\" vs \"{loaded.Name}\" = {(entry.Name == loaded.Name ? "OK" : "FAIL")}");
                Console.WriteLine($"     ImageCount: {entry.ImageCount} vs {loaded.ImageCount} = {(entry.ImageCount == loaded.ImageCount ? "OK" : "FAIL")}");
                Console.WriteLine($"     LinkedId: {entry.LinkedId} vs {loaded.LinkedId} = {(entry.LinkedId == loaded.LinkedId ? "OK" : "FAIL")}");
                Console.WriteLine($"     Actions: {entry.Actions.Count} vs {loaded.Actions.Count} = {(entry.Actions.Count == loaded.Actions.Count ? "OK" : "FAIL")}");
                Console.WriteLine($"     Attributes: {entry.Attributes.Count} vs {loaded.Attributes.Count} = {(entry.Attributes.Count == loaded.Attributes.Count ? "OK" : "FAIL")}");

                // 驗證 Action 細節
                if (entry.Actions.Count > 0 && loaded.Actions.Count > 0)
                {
                    Console.WriteLine($"\n   驗證 Action[0]:");
                    var origA = entry.Actions[0];
                    var loadA = loaded.Actions[0];
                    Console.WriteLine($"     ActionId: {origA.ActionId} vs {loadA.ActionId} = {(origA.ActionId == loadA.ActionId ? "OK" : "FAIL")}");
                    Console.WriteLine($"     ActionName: \"{origA.ActionName}\" vs \"{loadA.ActionName}\" = {(origA.ActionName == loadA.ActionName ? "OK" : "FAIL")}");
                    Console.WriteLine($"     FrameCount: {origA.FrameCount} vs {loadA.FrameCount} = {(origA.FrameCount == loadA.FrameCount ? "OK" : "FAIL")}");
                    Console.WriteLine($"     Frames: {origA.Frames.Count} vs {loadA.Frames.Count} = {(origA.Frames.Count == loadA.Frames.Count ? "OK" : "FAIL")}");

                    if (origA.Frames.Count > 0 && loadA.Frames.Count > 0)
                    {
                        var origF = origA.Frames[0];
                        var loadF = loadA.Frames[0];
                        Console.WriteLine($"     Frame[0]: {origF.ImageId}.{origF.FrameIndex}:{origF.Duration} vs {loadF.ImageId}.{loadF.FrameIndex}:{loadF.Duration} = {(origF.ImageId == loadF.ImageId && origF.FrameIndex == loadF.FrameIndex && origF.Duration == loadF.Duration ? "OK" : "FAIL")}");
                    }
                }

                // 5. Compact 格式測試
                string compactPath = Path.ChangeExtension(sprtxtPath, ".compact.sprtxt");
                Console.WriteLine($"\n4. SaveEntry (compact) -> {compactPath}");
                SprListWriter.SaveEntry(entry, compactPath, compact: true);
                string compactContent = File.ReadAllText(compactPath);
                Console.WriteLine($"   Size: {compactContent.Length} bytes (vs standard {content.Length})");
                Console.WriteLine($"   Preview: {(compactContent.Length > 200 ? compactContent.Substring(0, 200) + "..." : compactContent)}");

                Console.WriteLine($"\n=== 測試完成 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void TestSpriteLoad(string clientFolder, string spriteKey)
        {
            Console.WriteLine($"Testing sprite load:");
            Console.WriteLine($"  Client folder: {clientFolder}");
            Console.WriteLine($"  Sprite key: {spriteKey}");
            Console.WriteLine();

            // 建立字典 (模擬 LoadSpriteIdxForSprList)
            var spriteRecords = new Dictionary<string, (L1PakTools.IndexRecord record, string pakFile, bool isProtected)>();

            // 找到所有 Sprite*.idx 檔案
            string[] spriteFiles = Directory.GetFiles(clientFolder, "Sprite*.idx", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"Found {spriteFiles.Length} Sprite*.idx files:");
            foreach (var f in spriteFiles)
            {
                Console.WriteLine($"  - {Path.GetFileName(f)}");
            }

            if (spriteFiles.Length == 0)
            {
                Console.WriteLine("ERROR: No Sprite*.idx files found!");
                return;
            }

            foreach (string idxFile in spriteFiles)
            {
                string pakFile = idxFile.Replace(".idx", ".pak");
                if (!File.Exists(pakFile))
                {
                    Console.WriteLine($"  WARNING: {pakFile} not found, skipping");
                    continue;
                }

                byte[] indexData = File.ReadAllBytes(idxFile);
                int recordCount = (indexData.Length - 4) / 28;

                if (indexData.Length < 32 || (indexData.Length - 4) % 28 != 0)
                {
                    Console.WriteLine($"  WARNING: {idxFile} invalid format, skipping");
                    continue;
                }

                if ((long)BitConverter.ToUInt32(indexData, 0) != (long)recordCount)
                {
                    Console.WriteLine($"  WARNING: {idxFile} record count mismatch, skipping");
                    continue;
                }

                bool isProtected = false;
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    System.Text.Encoding.Default.GetString(indexData, 8, 20),
                    "^([a-zA-Z0-9_\\-\\.']+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    var firstRecord = L1PakTools.Decode_Index_FirstRecord(indexData);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(
                        firstRecord.FileName,
                        "^([a-zA-Z0-9_\\-\\.']+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        Console.WriteLine($"  WARNING: {idxFile} encrypted but can't decode, skipping");
                        continue;
                    }
                    isProtected = true;
                    indexData = L1PakTools.Decode(indexData, 4);
                }

                // 解析 records
                int count = BitConverter.ToInt32(indexData, 0);
                Console.WriteLine($"  {Path.GetFileName(idxFile)}: {count} records, protected={isProtected}");

                for (int i = 0; i < count; i++)
                {
                    int idx = 4 + i * 28;
                    // 正確順序: Offset(4) + FileName(20) + FileSize(4) = 28 bytes
                    int recordOffset = BitConverter.ToInt32(indexData, idx);
                    string fileName = System.Text.Encoding.Default.GetString(indexData, idx + 4, 20).TrimEnd('\0');
                    int fileSize = BitConverter.ToInt32(indexData, idx + 24);
                    string key = Path.GetFileNameWithoutExtension(fileName);

                    var record = new L1PakTools.IndexRecord(fileName, fileSize, recordOffset);
                    spriteRecords[key] = (record, pakFile, isProtected);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total sprite records: {spriteRecords.Count}");

            // 測試查找
            Console.WriteLine();
            Console.WriteLine($"Looking up key: '{spriteKey}'");

            if (spriteRecords.TryGetValue(spriteKey, out var info))
            {
                Console.WriteLine($"  FOUND!");
                Console.WriteLine($"  FileName: {info.record.FileName}");
                Console.WriteLine($"  FileSize: {info.record.FileSize}");
                Console.WriteLine($"  Offset: 0x{info.record.Offset:X}");
                Console.WriteLine($"  PakFile: {info.pakFile}");
                Console.WriteLine($"  Protected: {info.isProtected}");

                // 嘗試讀取
                Console.WriteLine();
                Console.WriteLine("Attempting to read sprite data...");
                try
                {
                    using (var fs = new FileStream(info.pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] data = new byte[info.record.FileSize];
                        fs.Seek(info.record.Offset, SeekOrigin.Begin);
                        fs.Read(data, 0, info.record.FileSize);

                        if (info.isProtected)
                        {
                            data = L1PakTools.Decode(data, 0);
                        }

                        Console.WriteLine($"  Read {data.Length} bytes");

                        // 嘗試解析 SPR
                        var frames = L1Spr.Load(data);
                        if (frames != null && frames.Length > 0)
                        {
                            Console.WriteLine($"  SUCCESS! Loaded {frames.Length} frames");
                            Console.WriteLine($"  First frame: {frames[0].width}x{frames[0].height}");
                        }
                        else
                        {
                            Console.WriteLine($"  WARNING: L1Spr.Load returned null or empty");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR reading: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"  NOT FOUND!");

                // 顯示一些相似的 key
                Console.WriteLine();
                Console.WriteLine("Similar keys in dictionary:");
                var similar = spriteRecords.Keys
                    .Where(k => k.StartsWith(spriteKey.Split('-')[0]))
                    .Take(10)
                    .ToList();
                foreach (var k in similar)
                {
                    Console.WriteLine($"  - {k}");
                }
            }
        }

        /// <summary>
        /// 批次匯出 SPR 檔案 (平行處理)
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾 (含 Sprite*.idx)</param>
        /// <param name="outputFolder">輸出資料夾</param>
        /// <param name="sprIdsOrFile">SPR 編號清單 (逗號分隔) 或清單檔案路徑</param>
        /// <param name="parallelCount">平行處理數量</param>
        static void BatchExportSpr(string clientFolder, string outputFolder, string sprIdsOrFile, int parallelCount)
        {
            Console.WriteLine("=== 批次匯出 SPR 檔案 ===");
            Console.WriteLine($"來源資料夾: {clientFolder}");
            Console.WriteLine($"輸出資料夾: {outputFolder}");
            Console.WriteLine($"平行數量: {parallelCount}");
            Console.WriteLine();

            // 解析 SPR 編號清單
            HashSet<int> targetSprIds;
            if (File.Exists(sprIdsOrFile))
            {
                // 從檔案讀取
                Console.WriteLine($"從檔案讀取編號清單: {sprIdsOrFile}");
                var lines = File.ReadAllLines(sprIdsOrFile);
                targetSprIds = new HashSet<int>();
                foreach (var line in lines)
                {
                    foreach (var part in line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(part.Trim(), out int id))
                            targetSprIds.Add(id);
                    }
                }
            }
            else
            {
                // 直接解析逗號分隔的編號
                targetSprIds = new HashSet<int>();
                foreach (var part in sprIdsOrFile.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), out int id))
                        targetSprIds.Add(id);
                }
            }

            Console.WriteLine($"目標 SPR 編號數量: {targetSprIds.Count}");

            if (targetSprIds.Count == 0)
            {
                Console.WriteLine("錯誤: 沒有有效的 SPR 編號");
                return;
            }

            // 建立輸出資料夾
            Directory.CreateDirectory(outputFolder);

            // 掃描所有 Sprite*.idx 並收集符合的 spr 檔案資訊
            // 結構: (fileName, idxFile, pakFile, record, isProtected)
            var filesToExport = new ConcurrentBag<(string fileName, string idxFile, string pakFile, L1PakTools.IndexRecord record, bool isProtected)>();

            var idxFiles = Directory.GetFiles(clientFolder, "Sprite*.idx");
            Console.WriteLine($"找到 {idxFiles.Length} 個 Sprite*.idx 檔案");
            Console.WriteLine();

            Console.WriteLine("掃描 idx 檔案...");
            foreach (var idxFile in idxFiles)
            {
                string pakFile = idxFile.Replace(".idx", ".pak");
                if (!File.Exists(pakFile)) continue;

                var result = LoadIndex(idxFile);
                if (result == null) continue;

                var (records, isProtected) = result.Value;

                foreach (var rec in records)
                {
                    if (!rec.FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 取得 SPR 編號
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(rec.FileName);
                    int dashIdx = nameWithoutExt.IndexOf('-');
                    if (dashIdx <= 0) continue;

                    if (!int.TryParse(nameWithoutExt.Substring(0, dashIdx), out int sprId))
                        continue;

                    if (targetSprIds.Contains(sprId))
                    {
                        filesToExport.Add((rec.FileName, idxFile, pakFile, rec, isProtected));
                    }
                }
            }

            Console.WriteLine($"找到 {filesToExport.Count} 個符合條件的 SPR 檔案");
            Console.WriteLine();

            if (filesToExport.Count == 0)
            {
                Console.WriteLine("沒有找到符合條件的 SPR 檔案");
                return;
            }

            // 計算跳過的檔案 (已存在)
            var filesToProcess = filesToExport.ToList();
            var alreadyExist = filesToProcess.Where(f => File.Exists(Path.Combine(outputFolder, f.fileName))).ToList();
            var toExport = filesToProcess.Where(f => !File.Exists(Path.Combine(outputFolder, f.fileName))).ToList();

            Console.WriteLine($"已存在 (跳過): {alreadyExist.Count}");
            Console.WriteLine($"需要匯出: {toExport.Count}");
            Console.WriteLine();

            if (toExport.Count == 0)
            {
                Console.WriteLine("所有檔案都已存在，無需匯出");
                return;
            }

            // 依 pakFile 分組，避免同時讀取同一個 pak
            var groupedByPak = toExport.GroupBy(f => f.pakFile).ToList();

            Console.WriteLine($"開始匯出 (分 {groupedByPak.Count} 個 PAK 處理)...");

            int exported = 0;
            int failed = 0;
            var exportLock = new object();
            var startTime = DateTime.Now;

            // 每個 PAK 內部平行處理
            foreach (var pakGroup in groupedByPak)
            {
                string pakFile = pakGroup.Key;
                var filesInPak = pakGroup.ToList();

                // 讀取整個 PAK 到記憶體 (一次性讀取，避免重複 I/O)
                byte[] pakData;
                try
                {
                    pakData = File.ReadAllBytes(pakFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"讀取 PAK 失敗: {pakFile} - {ex.Message}");
                    Interlocked.Add(ref failed, filesInPak.Count);
                    continue;
                }

                // 平行處理此 PAK 內的檔案
                Parallel.ForEach(filesInPak, new ParallelOptions { MaxDegreeOfParallelism = parallelCount }, fileInfo =>
                {
                    try
                    {
                        string outputPath = Path.Combine(outputFolder, fileInfo.fileName);

                        // 從 pakData 讀取
                        byte[] fileData = new byte[fileInfo.record.FileSize];
                        Array.Copy(pakData, fileInfo.record.Offset, fileData, 0, fileInfo.record.FileSize);

                        // 解碼
                        if (fileInfo.isProtected)
                        {
                            fileData = L1PakTools.Decode(fileData, 0);
                        }

                        // 寫入檔案
                        File.WriteAllBytes(outputPath, fileData);

                        int current = Interlocked.Increment(ref exported);
                        if (current % 1000 == 0)
                        {
                            var elapsed = DateTime.Now - startTime;
                            var rate = current / elapsed.TotalSeconds;
                            Console.WriteLine($"進度: {current}/{toExport.Count} ({rate:F0} 檔/秒)");
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref failed);
                    }
                });
            }

            var totalTime = DateTime.Now - startTime;
            Console.WriteLine();
            Console.WriteLine("=== 匯出完成 ===");
            Console.WriteLine($"成功: {exported}");
            Console.WriteLine($"失敗: {failed}");
            Console.WriteLine($"跳過 (已存在): {alreadyExist.Count}");
            Console.WriteLine($"耗時: {totalTime.TotalSeconds:F1} 秒");
            Console.WriteLine($"平均: {exported / totalTime.TotalSeconds:F0} 檔/秒");
        }

        #region Sprite Distribution Verification Commands

        static void VerifySpriteDistributionCmd(string clientFolder)
        {
            Console.WriteLine($"Verifying sprite distribution in: {clientFolder}");
            Console.WriteLine();

            var result = VerifySpriteDistribution(clientFolder);

            Console.WriteLine("=== Sprite Distribution Verification ===");
            Console.WriteLine($"Total files: {result.TotalFiles}");
            Console.WriteLine($"Correct files: {result.CorrectFiles} ({result.CorrectPercentage:F2}%)");
            Console.WriteLine($"Misplaced files: {result.MisplacedFiles.Count}");
            Console.WriteLine();

            if (result.MissingPaks.Count > 0)
            {
                Console.WriteLine("Missing PAK files:");
                foreach (var pak in result.MissingPaks)
                {
                    Console.WriteLine($"  - {pak}");
                }
                Console.WriteLine();
            }

            if (result.LoadErrors.Count > 0)
            {
                Console.WriteLine("Load errors:");
                foreach (var err in result.LoadErrors)
                {
                    Console.WriteLine($"  - {err}");
                }
                Console.WriteLine();
            }

            if (result.MisplacedFiles.Count > 0)
            {
                Console.WriteLine("Misplaced files (showing first 50):");
                int showCount = Math.Min(result.MisplacedFiles.Count, 50);
                foreach (var file in result.MisplacedFiles.Take(showCount))
                {
                    Console.WriteLine($"  {file.FileName}: in {file.CurrentPakName}, should be in {file.ExpectedPakName} (sum={file.FileNameSum}, sum%16={file.FileNameSum % 16})");
                }
                if (result.MisplacedFiles.Count > showCount)
                {
                    Console.WriteLine($"  ... and {result.MisplacedFiles.Count - showCount} more");
                }
                Console.WriteLine();
            }

            if (result.IsValid)
            {
                Console.WriteLine("✓ All sprite files are correctly distributed!");
            }
            else
            {
                Console.WriteLine("✗ Sprite distribution has issues.");
            }
        }

        static void CompareSpriteFiles(string folder1, string folder2, string outputFile)
        {
            Console.WriteLine($"Comparing Sprite files...");
            Console.WriteLine($"  Folder 1: {folder1}");
            Console.WriteLine($"  Folder 2: {folder2}");
            Console.WriteLine();

            if (!Directory.Exists(folder1))
            {
                Console.WriteLine($"Error: Folder 1 does not exist: {folder1}");
                return;
            }
            if (!Directory.Exists(folder2))
            {
                Console.WriteLine($"Error: Folder 2 does not exist: {folder2}");
                return;
            }

            // 收集兩個資料夾的 .spr 檔案
            var sprFiles1 = CollectSprFiles(folder1);
            var sprFiles2 = CollectSprFiles(folder2);

            Console.WriteLine($"Folder 1: {sprFiles1.Count} .spr files");
            Console.WriteLine($"Folder 2: {sprFiles2.Count} .spr files");
            Console.WriteLine();

            // 找出 folder1 有但 folder2 沒有的
            var onlyInFolder1 = sprFiles1.Keys
                .Where(k => !sprFiles2.ContainsKey(k))
                .OrderBy(k => k)
                .ToList();

            Console.WriteLine($"Files only in Folder 1: {onlyInFolder1.Count}");
            Console.WriteLine();

            // 輸出結果
            if (outputFile != null)
            {
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    writer.WriteLine($"# Sprite Diff Report");
                    writer.WriteLine($"# Folder 1: {folder1}");
                    writer.WriteLine($"# Folder 2: {folder2}");
                    writer.WriteLine($"# Files only in Folder 1: {onlyInFolder1.Count}");
                    writer.WriteLine();
                    foreach (var file in onlyInFolder1)
                    {
                        var info = sprFiles1[file];
                        writer.WriteLine($"{file}\t{info.size}\t{info.pakFile}");
                    }
                }
                Console.WriteLine($"Results written to: {outputFile}");
            }
            else
            {
                // 顯示前 100 筆
                int showCount = Math.Min(onlyInFolder1.Count, 100);
                Console.WriteLine($"Showing first {showCount} files:");
                Console.WriteLine("FileName\tSize\tPakFile");
                Console.WriteLine("--------\t----\t-------");
                foreach (var file in onlyInFolder1.Take(showCount))
                {
                    var info = sprFiles1[file];
                    Console.WriteLine($"{file}\t{info.size}\t{Path.GetFileName(info.pakFile)}");
                }
                if (onlyInFolder1.Count > showCount)
                {
                    Console.WriteLine($"... and {onlyInFolder1.Count - showCount} more files");
                    Console.WriteLine();
                    Console.WriteLine("Use output file parameter to save full list:");
                    Console.WriteLine($"  sprdiff \"{folder1}\" \"{folder2}\" output.txt");
                }
            }
        }

        static Dictionary<string, (int size, string pakFile)> CollectSprFiles(string folder)
        {
            var result = new Dictionary<string, (int size, string pakFile)>(StringComparer.OrdinalIgnoreCase);

            // 找所有 Sprite*.idx 檔案
            var idxFiles = Directory.GetFiles(folder, "Sprite*.idx", SearchOption.TopDirectoryOnly);

            foreach (var idxFile in idxFiles)
            {
                try
                {
                    var loadResult = LoadIndexAuto(idxFile);
                    if (loadResult == null) continue;

                    var (records, _, _) = loadResult.Value;
                    string pakFile = idxFile.Replace(".idx", ".pak");

                    foreach (var rec in records)
                    {
                        // 只收集 .spr 檔案
                        if (rec.FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                        {
                            // 使用檔名作為 key (不含副檔名，便於比對)
                            string key = rec.FileName;
                            if (!result.ContainsKey(key))
                            {
                                result[key] = (rec.FileSize, pakFile);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load {idxFile}: {ex.Message}");
                }
            }

            return result;
        }

        static void AnalyzeTilFile(string tilPath)
        {
            if (!File.Exists(tilPath))
            {
                Console.WriteLine($"Error: File not found: {tilPath}");
                return;
            }

            var data = File.ReadAllBytes(tilPath);

            Console.WriteLine($"File: {tilPath}");
            Console.WriteLine($"File size: {data.Length:N0} bytes");

            // 偵測壓縮類型
            var compression = L1Til.DetectCompression(data);
            Console.WriteLine($"Compression: {compression}");

            // 偵測版本
            var version = L1Til.GetVersion(data);
            Console.WriteLine($"Version: {version}");
            Console.WriteLine($"Tile size: {L1Til.GetTileSize(version)}x{L1Til.GetTileSize(version)}");

            // 解析 blocks
            var blocks = L1Til.Parse(data);
            Console.WriteLine($"Total blocks: {blocks.Count}");

            // 統計 block types
            var typeStats = blocks
                .Where(b => b != null && b.Length > 0)
                .GroupBy(b => b[0])
                .OrderBy(g => g.Key)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("=== Block Type Statistics ===");
            Console.WriteLine($"{"Type",-6} {"Hex",-6} {"Count",-8} {"Format",-18} {"Avg Size",-12} {"Flags"}");
            Console.WriteLine(new string('-', 75));

            foreach (var group in typeStats)
            {
                byte type = group.Key;
                int count = group.Count();
                double avgSize = group.Average(b => b.Length);

                bool isSimple = (type & 0x02) == 0;
                string format = isSimple ? "Simple Diamond" : "Compressed";

                // 額外 flags
                var flags = new List<string>();
                if ((type & 0x01) != 0) flags.Add("Flip");
                if ((type & 0x08) != 0) flags.Add("Shadow");
                if ((type & 0x10) != 0) flags.Add("Trans");

                string flagStr = flags.Count > 0 ? string.Join(", ", flags) : "-";

                Console.WriteLine($"{type,-6} 0x{type:X2}   {count,-8} {format,-18} {avgSize:F1} bytes   {flagStr}");
            }

            // 詳細分析
            var (classic, remaster, hybrid, unknown) = L1Til.AnalyzeTilBlocks(data);
            Console.WriteLine();
            Console.WriteLine("=== Block Format Analysis ===");
            Console.WriteLine($"Classic (24x24): {classic}");
            Console.WriteLine($"Remaster (48x48): {remaster}");
            Console.WriteLine($"Hybrid: {hybrid}");
            Console.WriteLine($"Unknown: {unknown}");

            // 取得 TileBlocks 來統計共用狀況
            var tileBlocks = L1Til.ParseToTileBlocks(data);
            if (tileBlocks != null)
            {
                Console.WriteLine();
                Console.WriteLine("=== Block Sharing ===");
                Console.WriteLine($"Total block references: {tileBlocks.Count}");
                Console.WriteLine($"Unique blocks: {tileBlocks.UniqueCount}");
                Console.WriteLine($"Sharing ratio: {(1.0 - (double)tileBlocks.UniqueCount / tileBlocks.Count) * 100:F1}%");
            }
        }

        static void CompareTileFiles(string folder1, string folder2, string outputFile)
        {
            Console.WriteLine($"Comparing Tile files...");
            Console.WriteLine($"  Folder 1: {folder1}");
            Console.WriteLine($"  Folder 2: {folder2}");
            Console.WriteLine();

            if (!Directory.Exists(folder1))
            {
                Console.WriteLine($"Error: Folder 1 does not exist: {folder1}");
                return;
            }
            if (!Directory.Exists(folder2))
            {
                Console.WriteLine($"Error: Folder 2 does not exist: {folder2}");
                return;
            }

            // 收集兩個資料夾的 .til 檔案
            var tilFiles1 = CollectPakFiles(folder1, "Tile*.idx", ".til");
            var tilFiles2 = CollectPakFiles(folder2, "Tile*.idx", ".til");

            Console.WriteLine($"Folder 1: {tilFiles1.Count} .til files");
            Console.WriteLine($"Folder 2: {tilFiles2.Count} .til files");
            Console.WriteLine();

            // 找出 folder1 有但 folder2 沒有的
            var onlyInFolder1 = tilFiles1.Keys
                .Where(k => !tilFiles2.ContainsKey(k))
                .OrderBy(k => k)
                .ToList();

            Console.WriteLine($"Files only in Folder 1: {onlyInFolder1.Count}");
            Console.WriteLine();

            // 輸出結果
            if (outputFile != null)
            {
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    writer.WriteLine($"# Tile Diff Report");
                    writer.WriteLine($"# Folder 1: {folder1}");
                    writer.WriteLine($"# Folder 2: {folder2}");
                    writer.WriteLine($"# Files only in Folder 1: {onlyInFolder1.Count}");
                    writer.WriteLine();
                    foreach (var file in onlyInFolder1)
                    {
                        var info = tilFiles1[file];
                        writer.WriteLine($"{file}\t{info.size}\t{info.pakFile}");
                    }
                }
                Console.WriteLine($"Results written to: {outputFile}");
            }
            else
            {
                // 顯示前 100 筆
                int showCount = Math.Min(onlyInFolder1.Count, 100);
                Console.WriteLine($"Showing first {showCount} files:");
                Console.WriteLine("FileName\tSize\tPakFile");
                Console.WriteLine("--------\t----\t-------");
                foreach (var file in onlyInFolder1.Take(showCount))
                {
                    var info = tilFiles1[file];
                    Console.WriteLine($"{file}\t{info.size}\t{Path.GetFileName(info.pakFile)}");
                }
                if (onlyInFolder1.Count > showCount)
                {
                    Console.WriteLine($"... and {onlyInFolder1.Count - showCount} more files");
                    Console.WriteLine();
                    Console.WriteLine("Use output file parameter to save full list:");
                    Console.WriteLine($"  tildiff \"{folder1}\" \"{folder2}\" output.txt");
                }
            }
        }

        static void ComparePakFiles(string folder1, string folder2, string idxPattern, string extension, string outputFile)
        {
            Console.WriteLine($"Comparing {extension} files...");
            Console.WriteLine($"  Folder 1: {folder1}");
            Console.WriteLine($"  Folder 2: {folder2}");
            Console.WriteLine($"  IDX Pattern: {idxPattern}");
            Console.WriteLine();

            if (!Directory.Exists(folder1))
            {
                Console.WriteLine($"Error: Folder 1 does not exist: {folder1}");
                return;
            }
            if (!Directory.Exists(folder2))
            {
                Console.WriteLine($"Error: Folder 2 does not exist: {folder2}");
                return;
            }

            var files1 = CollectPakFiles(folder1, idxPattern, extension);
            var files2 = CollectPakFiles(folder2, idxPattern, extension);

            Console.WriteLine($"Folder 1: {files1.Count} {extension} files");
            Console.WriteLine($"Folder 2: {files2.Count} {extension} files");
            Console.WriteLine();

            var onlyInFolder1 = files1.Keys
                .Where(k => !files2.ContainsKey(k))
                .OrderBy(k => k)
                .ToList();

            Console.WriteLine($"Files only in Folder 1: {onlyInFolder1.Count}");
            Console.WriteLine();

            if (outputFile != null)
            {
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    writer.WriteLine($"# {extension} Diff Report");
                    writer.WriteLine($"# Folder 1: {folder1}");
                    writer.WriteLine($"# Folder 2: {folder2}");
                    writer.WriteLine($"# Files only in Folder 1: {onlyInFolder1.Count}");
                    writer.WriteLine();
                    foreach (var file in onlyInFolder1)
                    {
                        var info = files1[file];
                        writer.WriteLine($"{file}\t{info.size}\t{info.pakFile}");
                    }
                }
                Console.WriteLine($"Results written to: {outputFile}");
            }
            else
            {
                int showCount = Math.Min(onlyInFolder1.Count, 100);
                Console.WriteLine($"Showing first {showCount} files:");
                Console.WriteLine("FileName\tSize\tPakFile");
                Console.WriteLine("--------\t----\t-------");
                foreach (var file in onlyInFolder1.Take(showCount))
                {
                    var info = files1[file];
                    Console.WriteLine($"{file}\t{info.size}\t{Path.GetFileName(info.pakFile)}");
                }
                if (onlyInFolder1.Count > showCount)
                {
                    Console.WriteLine($"... and {onlyInFolder1.Count - showCount} more files");
                }
            }
        }

        static Dictionary<string, (int size, string pakFile)> CollectPakFiles(string folder, string idxPattern, string extension)
        {
            var result = new Dictionary<string, (int size, string pakFile)>(StringComparer.OrdinalIgnoreCase);

            // 找所有符合 pattern 的 idx 檔案
            var idxFiles = Directory.GetFiles(folder, idxPattern, SearchOption.TopDirectoryOnly);

            foreach (var idxFile in idxFiles)
            {
                try
                {
                    var loadResult = LoadIndexAuto(idxFile);
                    if (loadResult == null) continue;

                    var (records, _, _) = loadResult.Value;
                    string pakFile = idxFile.Replace(".idx", ".pak");

                    foreach (var rec in records)
                    {
                        // 只收集指定副檔名的檔案
                        if (rec.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        {
                            string key = rec.FileName;
                            if (!result.ContainsKey(key))
                            {
                                result[key] = (rec.FileSize, pakFile);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load {idxFile}: {ex.Message}");
                }
            }

            return result;
        }

        static void ExportFromDiff(string diffFile, string exportDir)
        {
            if (!File.Exists(diffFile))
            {
                Console.WriteLine($"Error: Diff file not found: {diffFile}");
                return;
            }

            // 建立輸出目錄
            Directory.CreateDirectory(exportDir);

            // 讀取 diff 檔案，收集需要匯出的檔案
            var filesToExport = new List<(string fileName, string pakFile)>();
            foreach (var line in File.ReadLines(diffFile, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    string fileName = parts[0];
                    string pakFile = parts[2];
                    filesToExport.Add((fileName, pakFile));
                }
            }

            Console.WriteLine($"Found {filesToExport.Count} files to export");
            Console.WriteLine($"Export directory: {exportDir}");
            Console.WriteLine();

            // 按 PAK 檔分組，減少重複載入
            var groupedByPak = filesToExport.GroupBy(f => f.pakFile);
            int exported = 0;
            int failed = 0;

            foreach (var pakGroup in groupedByPak)
            {
                string pakFile = pakGroup.Key;
                string idxFile = pakFile.Replace(".pak", ".idx");

                if (!File.Exists(pakFile))
                {
                    Console.WriteLine($"Warning: PAK file not found: {pakFile}");
                    failed += pakGroup.Count();
                    continue;
                }

                // 載入索引
                var loadResult = LoadIndexAuto(idxFile);
                if (loadResult == null)
                {
                    Console.WriteLine($"Warning: Failed to load index: {idxFile}");
                    failed += pakGroup.Count();
                    continue;
                }

                var (records, isProtected, _) = loadResult.Value;
                // 使用 GroupBy 處理重複檔名，取第一個
                var recordDict = records
                    .GroupBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                using (var pakStream = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    foreach (var (fileName, _) in pakGroup)
                    {
                        try
                        {
                            if (!recordDict.TryGetValue(fileName, out var record))
                            {
                                Console.WriteLine($"Warning: File not found in index: {fileName}");
                                failed++;
                                continue;
                            }

                            // 讀取檔案資料
                            pakStream.Seek(record.Offset, SeekOrigin.Begin);
                            byte[] data = new byte[record.FileSize];
                            pakStream.Read(data, 0, record.FileSize);

                            // 解密 (如果需要)
                            if (isProtected)
                            {
                                data = L1PakTools.Decode(data, 0);
                            }

                            // 寫入檔案
                            string outputPath = Path.Combine(exportDir, fileName);
                            File.WriteAllBytes(outputPath, data);
                            exported++;

                            if (exported % 100 == 0)
                            {
                                Console.Write($"\rExported: {exported} / {filesToExport.Count}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error exporting {fileName}: {ex.Message}");
                            failed++;
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Export completed!");
            Console.WriteLine($"  Exported: {exported}");
            Console.WriteLine($"  Failed: {failed}");
            Console.WriteLine($"  Output: {exportDir}");
        }

        static void VerifyIdxSortOrderCmd(string idxFile, string comparerType = "ascii")
        {
            Console.WriteLine($"Verifying sort order: {idxFile}");
            Console.WriteLine($"Comparer: {comparerType}");
            Console.WriteLine();

            var result = VerifyIdxSortOrder(idxFile, comparerType);

            if (result.Count == 0)
            {
                Console.WriteLine($"✓ IDX file is correctly sorted ({comparerType})!");
            }
            else
            {
                Console.WriteLine($"✗ Found {result.Count} incorrectly sorted entries (showing first 20):");
                int showCount = Math.Min(result.Count, 20);
                foreach (var (index, fileName, expectedFileName) in result.Take(showCount))
                {
                    Console.WriteLine($"  [{index}] {fileName} (expected: {expectedFileName})");
                }
                if (result.Count > showCount)
                {
                    Console.WriteLine($"  ... and {result.Count - showCount} more");
                }
            }
        }

        #endregion

        #region Sprite/Pak File Addition Utilities

        /// <summary>
        /// 計算檔名應該放到哪個 sprite*.pak (0-15)
        /// 規則: 檔名的原始 bytes 加總 % 16
        /// 0 = sprite00.pak, 1-15 = sprite01.pak ~ sprite15.pak
        /// </summary>
        /// <param name="fileName">檔案名稱 (不含路徑)</param>
        /// <returns>0-15 的索引值</returns>
        public static int GetSpritePakIndex(string fileName)
        {
            // 使用 Default encoding 取得 bytes (僅用於新檔案)
            byte[] bytes = Encoding.Default.GetBytes(fileName);
            return GetSpritePakIndex(bytes);
        }

        /// <summary>
        /// 計算檔名應該放到哪個 sprite*.pak (0-15)
        /// 規則: 檔名的原始 bytes 加總 % 16
        /// </summary>
        /// <param name="fileNameBytes">檔名原始 bytes</param>
        /// <returns>0-15 的索引值</returns>
        public static int GetSpritePakIndex(byte[] fileNameBytes)
        {
            int sum = 0;
            foreach (byte b in fileNameBytes)
            {
                sum += b;
            }
            return sum % 16;
        }

        /// <summary>
        /// 取得 sprite pak 檔案路徑
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾路徑</param>
        /// <param name="index">0-15 的索引值</param>
        /// <returns>sprite*.pak 的完整路徑</returns>
        public static string GetSpritePakPath(string clientFolder, int index)
        {
            return Path.Combine(clientFolder, $"sprite{index:D2}.pak");  // sprite00.pak ~ sprite15.pak
        }

        /// <summary>
        /// 取得 sprite idx 檔案路徑
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾路徑</param>
        /// <param name="index">0-15 的索引值</param>
        /// <returns>sprite*.idx 的完整路徑</returns>
        public static string GetSpriteIdxPath(string clientFolder, int index)
        {
            return Path.Combine(clientFolder, $"sprite{index:D2}.idx");  // sprite00.idx ~ sprite15.idx
        }

        /// <summary>
        /// 使用二分搜尋找到插入位置
        /// </summary>
        private static int FindInsertIndex(List<L1PakTools.IndexRecord> records, string fileName, IComparer<string> comparer)
        {
            int left = 0;
            int right = records.Count;

            while (left < right)
            {
                int mid = (left + right) / 2;
                if (comparer.Compare(records[mid].FileName, fileName) < 0)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid;
                }
            }

            return left;
        }

        /// <summary>
        /// 不區分大小寫的 ASCII 排序比較器 (用於檔名排序)
        /// 按照忽略大小寫的 ASCII 字元順序排序
        /// </summary>
        public class AsciiStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                // 使用 OrdinalIgnoreCase 比較 (不區分大小寫)
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 特殊排序比較器 - 底線 (_) 排在字母之前 (數字 → 底線 → 字母)
        /// </summary>
        public class UnderscoreFirstComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                int minLen = Math.Min(x.Length, y.Length);
                for (int i = 0; i < minLen; i++)
                {
                    char cx = char.ToLowerInvariant(x[i]);
                    char cy = char.ToLowerInvariant(y[i]);

                    if (cx == cy) continue;

                    // 底線排在字母之前
                    int ox = GetOrder(cx);
                    int oy = GetOrder(cy);

                    if (ox != oy) return ox.CompareTo(oy);
                    return cx.CompareTo(cy);
                }
                return x.Length.CompareTo(y.Length);
            }

            private int GetOrder(char c)
            {
                // 其他符號 → -1
                // 數字 0-9 → 0
                // 底線 _ → 1  (在字母之前)
                // 字母 a-z → 2
                if (c >= '0' && c <= '9') return 0;
                if (c == '_') return 1;
                if (c >= 'a' && c <= 'z') return 2;
                return c < '0' ? -1 : 3;
            }
        }

        /// <summary>
        /// 新增 SPR 檔案到正確的 sprite*.pak
        /// 根據檔名 ANSI 加總 % 16 決定目標 pak
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾路徑</param>
        /// <param name="sprFilePath">要新增的 SPR 檔案路徑</param>
        /// <returns>(success, error message)</returns>
        public static (bool success, string message) AddSpriteFile(string clientFolder, string sprFilePath)
        {
            if (!File.Exists(sprFilePath))
            {
                return (false, $"File not found: {sprFilePath}");
            }

            string fileName = Path.GetFileName(sprFilePath);
            int pakIndex = GetSpritePakIndex(fileName);
            string idxPath = GetSpriteIdxPath(clientFolder, pakIndex);
            string pakPath = GetSpritePakPath(clientFolder, pakIndex);

            Console.WriteLine($"Adding {fileName} to {Path.GetFileName(pakPath)} (index={pakIndex}, sum={Encoding.Default.GetBytes(fileName).Sum(b => b)})");

            if (!File.Exists(idxPath) || !File.Exists(pakPath))
            {
                return (false, $"Target pak not found: {pakPath}");
            }

            // 使用 AddPakFile 加入並排序
            return AddPakFile(idxPath, sprFilePath, sortAfterAdd: true);
        }

        /// <summary>
        /// 批次新增多個 SPR 檔案
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾路徑</param>
        /// <param name="sprFilePaths">要新增的 SPR 檔案路徑清單</param>
        /// <returns>成功和失敗的數量</returns>
        public static (int success, int failed, List<string> errors) AddSpriteFiles(string clientFolder, string[] sprFilePaths)
        {
            int success = 0;
            int failed = 0;
            var errors = new List<string>();

            // 按目標 pak 分組，減少重複 IO
            var groupedByPak = sprFilePaths
                .Where(f => File.Exists(f))
                .GroupBy(f => GetSpritePakIndex(Path.GetFileName(f)));

            foreach (var group in groupedByPak)
            {
                int pakIndex = group.Key;
                string idxPath = GetSpriteIdxPath(clientFolder, pakIndex);
                var files = group.ToArray();

                Console.WriteLine($"Adding {files.Length} files to {Path.GetFileName(idxPath)}...");

                var result = AddPakFiles(idxPath, files, sortAfterAdd: true);
                success += result.success;
                failed += result.failed;
                errors.AddRange(result.errors);
            }

            return (success, failed, errors);
        }

        /// <summary>
        /// 新增單一檔案到 PAK 並可選擇按 Windows 排序方式排序 IDX
        /// </summary>
        /// <param name="idxFile">IDX 檔案路徑</param>
        /// <param name="filePath">要新增的檔案路徑</param>
        /// <param name="sortAfterAdd">新增後是否按 Windows 檔名排序 IDX</param>
        /// <returns>(success, error message)</returns>
        public static (bool success, string message) AddPakFile(string idxFile, string filePath, bool sortAfterAdd = true)
        {
            var result = AddPakFiles(idxFile, new[] { filePath }, sortAfterAdd);
            if (result.failed > 0 && result.errors.Count > 0)
            {
                return (false, result.errors[0]);
            }
            return (result.success > 0, result.success > 0 ? "Success" : "Unknown error");
        }

        /// <summary>
        /// 新增多個檔案到 PAK 並可選擇按 Windows 排序方式排序 IDX
        /// </summary>
        /// <param name="idxFile">IDX 檔案路徑</param>
        /// <param name="filePaths">要新增的檔案路徑清單</param>
        /// <param name="sortAfterAdd">新增後是否按 Windows 檔名排序 IDX</param>
        /// <returns>成功和失敗的數量</returns>
        public static (int success, int failed, List<string> errors) AddPakFiles(string idxFile, string[] filePaths, bool sortAfterAdd = true)
        {
            int successCount = 0;
            int failedCount = 0;
            var errors = new List<string>();

            var indexResult = LoadIndex(idxFile);
            if (indexResult == null)
            {
                errors.Add($"Cannot load index: {idxFile}");
                return (0, filePaths.Length, errors);
            }

            var (records, isProtected) = indexResult.Value;
            string pakFile = idxFile.Replace(".idx", ".pak");

            if (!File.Exists(pakFile))
            {
                errors.Add($"PAK file not found: {pakFile}");
                return (0, filePaths.Length, errors);
            }

            // 驗證檔案並過濾重複
            var filesToAdd = new List<(string filePath, string fileName, long fileSize)>();
            var existingNames = new HashSet<string>(records.Select(r => r.FileName.ToLowerInvariant()));

            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    errors.Add($"File not found: {filePath}");
                    failedCount++;
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                if (Encoding.Default.GetByteCount(fileName) > 19)
                {
                    errors.Add($"Filename too long (max 19 bytes): {fileName}");
                    failedCount++;
                    continue;
                }

                if (existingNames.Contains(fileName.ToLowerInvariant()))
                {
                    errors.Add($"File already exists: {fileName}");
                    failedCount++;
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                filesToAdd.Add((filePath, fileName, fileInfo.Length));
                existingNames.Add(fileName.ToLowerInvariant());
            }

            if (filesToAdd.Count == 0)
            {
                return (successCount, failedCount, errors);
            }

            // 建立備份
            string pakBackup = pakFile + ".bak";
            string idxBackup = idxFile + ".bak";

            if (File.Exists(pakBackup)) File.Delete(pakBackup);
            if (File.Exists(idxBackup)) File.Delete(idxBackup);

            File.Copy(pakFile, pakBackup);
            File.Copy(idxFile, idxBackup);

            try
            {
                var pakFileInfo = new FileInfo(pakFile);
                int currentOffset = (int)pakFileInfo.Length;
                var newRecordsList = new List<L1PakTools.IndexRecord>(records);

                using (FileStream pakFs = File.Open(pakFile, FileMode.Append, FileAccess.Write))
                {
                    foreach (var (filePath, fileName, fileSize) in filesToAdd)
                    {
                        byte[] fileData = File.ReadAllBytes(filePath);
                        byte[] dataToWrite = fileData;

                        // Encode if L1 protected
                        if (isProtected)
                        {
                            dataToWrite = L1PakTools.Encode(dataToWrite, 0);
                        }

                        pakFs.Write(dataToWrite, 0, dataToWrite.Length);

                        newRecordsList.Add(new L1PakTools.IndexRecord(
                            fileName,
                            dataToWrite.Length,
                            currentOffset
                        ));

                        currentOffset += dataToWrite.Length;
                        successCount++;
                    }
                }

                // 將新增的檔案插入到正確位置 (不重排，只插入)
                L1PakTools.IndexRecord[] finalRecords;
                if (sortAfterAdd)
                {
                    // 分離原有記錄和新增記錄
                    var existingRecords = newRecordsList.Take(records.Length).ToList();
                    var addedRecords = newRecordsList.Skip(records.Length).ToList();

                    // 將新增的記錄逐一插入到正確位置
                    var comparer = new UnderscoreFirstComparer();
                    foreach (var newRec in addedRecords)
                    {
                        int insertIndex = FindInsertIndex(existingRecords, newRec.FileName, comparer);
                        existingRecords.Insert(insertIndex, newRec);
                    }
                    finalRecords = existingRecords.ToArray();
                }
                else
                {
                    finalRecords = newRecordsList.ToArray();
                }

                RebuildIndex(idxFile, finalRecords, isProtected);

                // 刪除備份
                File.Delete(pakBackup);
                File.Delete(idxBackup);
            }
            catch (Exception ex)
            {
                errors.Add($"Error during addition: {ex.Message}");

                // 從備份還原
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

                return (0, filePaths.Length, errors);
            }

            return (successCount, failedCount, errors);
        }

        /// <summary>
        /// 驗證客戶端的 sprite 檔案分布是否符合規則
        /// </summary>
        /// <param name="clientFolder">客戶端資料夾路徑</param>
        /// <returns>驗證結果</returns>
        public static SpriteDistributionResult VerifySpriteDistribution(string clientFolder)
        {
            var result = new SpriteDistributionResult();

            for (int i = 0; i <= 15; i++)  // 0-15 共 16 個 pak
            {
                string idxPath = GetSpriteIdxPath(clientFolder, i);
                string pakPath = GetSpritePakPath(clientFolder, i);

                if (!File.Exists(idxPath))
                {
                    result.MissingPaks.Add(Path.GetFileName(idxPath));
                    continue;
                }

                var indexResult = LoadIndex(idxPath);
                if (indexResult == null)
                {
                    result.LoadErrors.Add($"Cannot load: {idxPath}");
                    continue;
                }

                var (records, _) = indexResult.Value;
                result.TotalFiles += records.Length;

                foreach (var record in records)
                {
                    // 使用原始 bytes 計算，避免編碼轉換問題
                    int fileNameSum = record.GetFileNameBytesSum();
                    int expectedIndex = fileNameSum % 16;
                    if (expectedIndex != i)
                    {
                        result.MisplacedFiles.Add(new MisplacedFile
                        {
                            FileName = record.FileName,
                            CurrentPakIndex = i,
                            ExpectedPakIndex = expectedIndex,
                            FileNameSum = fileNameSum
                        });
                    }
                    else
                    {
                        result.CorrectFiles++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 驗證 IDX 檔案中的排序是否符合指定的排序規則
        /// </summary>
        /// <param name="idxFile">IDX 檔案路徑</param>
        /// <param name="comparerType">比較器類型: ascii, underscore</param>
        /// <returns>不正確排序的項目清單</returns>
        public static List<(int index, string fileName, string expectedFileName)> VerifyIdxSortOrder(string idxFile, string comparerType = "ascii")
        {
            var result = new List<(int index, string fileName, string expectedFileName)>();

            var indexResult = LoadIndex(idxFile);
            if (indexResult == null)
            {
                return result;
            }

            var (records, _) = indexResult.Value;

            // 根據類型選擇比較器
            IComparer<string> comparer = comparerType switch
            {
                "underscore" => new UnderscoreFirstComparer(),  // 底線排在字母之前
                _ => new AsciiStringComparer()  // 不區分大小寫 ASCII
            };

            var sortedNames = records.Select(r => r.FileName).OrderBy(n => n, comparer).ToList();

            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].FileName != sortedNames[i])
                {
                    result.Add((i, records[i].FileName, sortedNames[i]));
                }
            }

            return result;
        }

        #endregion

        #region Sprite Distribution Result Classes

        public class SpriteDistributionResult
        {
            public int TotalFiles { get; set; }
            public int CorrectFiles { get; set; }
            public List<MisplacedFile> MisplacedFiles { get; set; } = new List<MisplacedFile>();
            public List<string> MissingPaks { get; set; } = new List<string>();
            public List<string> LoadErrors { get; set; } = new List<string>();

            public bool IsValid => MisplacedFiles.Count == 0 && LoadErrors.Count == 0;
            public double CorrectPercentage => TotalFiles > 0 ? (double)CorrectFiles / TotalFiles * 100 : 0;
        }

        public class MisplacedFile
        {
            public string FileName { get; set; }
            public int CurrentPakIndex { get; set; }
            public int ExpectedPakIndex { get; set; }
            public int FileNameSum { get; set; }

            public string CurrentPakName => $"sprite{CurrentPakIndex:D2}.pak";
            public string ExpectedPakName => $"sprite{ExpectedPakIndex:D2}.pak";
        }

        #endregion

        #region M Tile Conversion

        static void BatchConvertMTil(string inputDir, string outputDir, string pattern, string compressionStr)
        {
            Console.WriteLine("M Tile to L1 Til Batch Converter");
            Console.WriteLine($"  Input:  {inputDir}");
            Console.WriteLine($"  Output: {outputDir}");
            Console.WriteLine($"  Pattern: {pattern}");
            Console.WriteLine($"  Compression: {compressionStr}");
            Console.WriteLine();

            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine($"Error: Input directory not found: {inputDir}");
                return;
            }

            // 建立輸出目錄
            Directory.CreateDirectory(outputDir);

            // 解析壓縮類型
            L1Til.CompressionType compression;
            switch (compressionStr)
            {
                case "zlib":
                    compression = L1Til.CompressionType.Zlib;
                    break;
                case "brotli":
                    compression = L1Til.CompressionType.Brotli;
                    break;
                default:
                    compression = L1Til.CompressionType.None;
                    break;
            }

            // 找出所有符合的檔案
            var files = Directory.GetFiles(inputDir, pattern);
            Console.WriteLine($"Found {files.Length} files to convert");
            Console.WriteLine();

            int success = 0;
            int failed = 0;
            var errors = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileNameWithoutExtension(file);
                var outputPath = Path.Combine(outputDir, fileName + ".til");

                try
                {
                    MTil.SaveToL1Til(file, outputPath, compression);
                    success++;

                    // 進度顯示
                    if ((i + 1) % 100 == 0 || i == files.Length - 1)
                    {
                        Console.Write($"\rProgress: {i + 1}/{files.Length} ({success} success, {failed} failed)");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{fileName}: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("=== Conversion Complete ===");
            Console.WriteLine($"  Success: {success}");
            Console.WriteLine($"  Failed:  {failed}");
            Console.WriteLine($"  Output:  {outputDir}");

            if (errors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Errors:");
                int showCount = Math.Min(errors.Count, 20);
                foreach (var error in errors.Take(showCount))
                {
                    Console.WriteLine($"  {error}");
                }
                if (errors.Count > showCount)
                {
                    Console.WriteLine($"  ... and {errors.Count - showCount} more errors");
                }
            }
        }

        static void TilMd5(string filePath, int blockIndex)
        {
            Console.WriteLine($"=== Tile MD5: {Path.GetFileName(filePath)} ===");
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found: {filePath}");
                return;
            }

            try
            {
                var blocks = L1Til.Parse(File.ReadAllBytes(filePath));
                Console.WriteLine($"Total blocks: {blocks.Count}");
                Console.WriteLine();

                if (blockIndex >= 0)
                {
                    // 顯示單一 block
                    if (blockIndex < blocks.Count)
                    {
                        string md5 = L1Til.GetBlockMd5(blocks[blockIndex]);
                        Console.WriteLine($"Block {blockIndex}: {md5} ({blocks[blockIndex].Length} bytes)");
                    }
                    else
                    {
                        Console.WriteLine($"Error: Block index {blockIndex} out of range");
                    }
                }
                else
                {
                    // 顯示所有 blocks
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        string md5 = L1Til.GetBlockMd5(blocks[i]);
                        Console.WriteLine($"[{i,3}] {md5} ({blocks[i].Length,4} bytes)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void TilCompare(string file1, string file2)
        {
            Console.WriteLine($"=== Tile Compare ===");
            Console.WriteLine($"  File1: {Path.GetFileName(file1)}");
            Console.WriteLine($"  File2: {Path.GetFileName(file2)}");
            Console.WriteLine();

            if (!File.Exists(file1))
            {
                Console.WriteLine($"Error: File not found: {file1}");
                return;
            }
            if (!File.Exists(file2))
            {
                Console.WriteLine($"Error: File not found: {file2}");
                return;
            }

            try
            {
                var (matched, different, onlyIn1, onlyIn2, diffIndices) = L1Til.CompareBlocks(file1, file2);

                Console.WriteLine($"Results:");
                Console.WriteLine($"  Matched:       {matched}");
                Console.WriteLine($"  Different:     {different}");
                Console.WriteLine($"  Only in file1: {onlyIn1}");
                Console.WriteLine($"  Only in file2: {onlyIn2}");
                Console.WriteLine();

                if (diffIndices.Count > 0 && diffIndices.Count <= 50)
                {
                    Console.WriteLine($"Different block indices: {string.Join(", ", diffIndices)}");
                }
                else if (diffIndices.Count > 50)
                {
                    Console.WriteLine($"Different block indices (first 50): {string.Join(", ", diffIndices.Take(50))}...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void TilColorDiff(string til1Dir, string til2Dir, double threshold, int maxTileId = int.MaxValue)
        {
            Console.WriteLine($"=== Tile Color Difference Analysis ===");
            Console.WriteLine($"  til1 dir:  {til1Dir}");
            Console.WriteLine($"  til2 dir:  {til2Dir}");
            Console.WriteLine($"  Threshold: {threshold}");
            Console.WriteLine($"  Max Tile:  {(maxTileId == int.MaxValue ? "all" : maxTileId.ToString())}");
            Console.WriteLine();

            if (!Directory.Exists(til1Dir))
            {
                Console.WriteLine($"Error: Directory not found: {til1Dir}");
                return;
            }
            if (!Directory.Exists(til2Dir))
            {
                Console.WriteLine($"Error: Directory not found: {til2Dir}");
                return;
            }

            var til1Files = Directory.GetFiles(til1Dir, "*.til")
                .Select(f => Path.GetFileName(f))
                .ToHashSet();

            var results = new List<(int tileId, int blockIdx, double avgDiff, string detail)>();
            int processedTiles = 0;
            int skippedTiles = 0;

            foreach (var til1Name in til1Files.OrderBy(f => {
                var match = System.Text.RegularExpressions.Regex.Match(f, @"(\d+)\.til");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }))
            {
                string til1Path = Path.Combine(til1Dir, til1Name);
                string til2Path = Path.Combine(til2Dir, til1Name);

                if (!File.Exists(til2Path))
                {
                    skippedTiles++;
                    continue;
                }

                var match = System.Text.RegularExpressions.Regex.Match(til1Name, @"(\d+)\.til");
                int tileId = match.Success ? int.Parse(match.Groups[1].Value) : 0;

                if (tileId >= maxTileId)
                {
                    skippedTiles++;
                    continue;
                }

                try
                {
                    var blocks1 = L1Til.Parse(File.ReadAllBytes(til1Path));
                    var blocks2 = L1Til.Parse(File.ReadAllBytes(til2Path));

                    int minBlocks = Math.Min(blocks1.Count, blocks2.Count);

                    for (int i = 0; i < minBlocks; i++)
                    {
                        var colors1 = ExtractBlockColors(blocks1[i]);
                        var colors2 = ExtractBlockColors(blocks2[i]);

                        if (colors1.Count == 0 && colors2.Count == 0)
                            continue;

                        // Calculate average color difference
                        double totalDiff = 0;
                        int compared = 0;

                        int minColors = Math.Min(colors1.Count, colors2.Count);
                        for (int c = 0; c < minColors; c++)
                        {
                            var (r1, g1, b1) = Rgb555ToComponents(colors1[c]);
                            var (r2, g2, b2) = Rgb555ToComponents(colors2[c]);
                            totalDiff += Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2);
                            compared++;
                        }

                        double avgDiff = compared > 0 ? totalDiff / compared : 0;

                        if (avgDiff >= threshold)
                        {
                            string detail = "";
                            if (colors1.Count > 0 && colors2.Count > 0)
                            {
                                var (r1, g1, b1) = Rgb555ToComponents(colors1[0]);
                                var (r2, g2, b2) = Rgb555ToComponents(colors2[0]);
                                detail = $"til1:({r1},{g1},{b1}) til2:({r2},{g2},{b2})";
                            }
                            results.Add((tileId, i, avgDiff, detail));
                        }
                    }

                    processedTiles++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing tile {tileId}: {ex.Message}");
                }
            }

            Console.WriteLine($"Processed: {processedTiles} tiles, Skipped: {skippedTiles} (no match in til2)");
            Console.WriteLine();

            // Sort by difference descending
            var sortedResults = results.OrderByDescending(r => r.avgDiff).Take(100).ToList();

            Console.WriteLine($"=== Blocks with avg diff >= {threshold} (top 100) ===");
            Console.WriteLine($"{"Tile",-8} {"Block",-8} {"AvgDiff",-10} {"First Color Sample"}");
            Console.WriteLine(new string('-', 70));

            foreach (var (tileId, blockIdx, avgDiff, detail) in sortedResults)
            {
                Console.WriteLine($"{tileId,-8} {blockIdx,-8} {avgDiff,-10:F2} {detail}");
            }

            Console.WriteLine();
            Console.WriteLine($"Total blocks with diff >= {threshold}: {results.Count}");
        }

        static List<ushort> ExtractBlockColors(byte[] blockData)
        {
            var colors = new List<ushort>();
            if (blockData == null || blockData.Length < 2)
                return colors;

            int blockType = blockData[0];
            bool isSimpleDiamond = blockType == 0 || blockType == 1 || blockType == 8 || blockType == 9 ||
                                   blockType == 16 || blockType == 17;

            if (isSimpleDiamond)
            {
                // Simple diamond: [type] [pixel data...] [terminator]
                for (int i = 1; i < blockData.Length - 1; i += 2)
                {
                    if (i + 1 < blockData.Length)
                    {
                        ushort color = (ushort)(blockData[i] | (blockData[i + 1] << 8));
                        if (color != 0)
                            colors.Add(color);
                    }
                }
            }
            else if (blockData.Length >= 5)
            {
                // Compressed format: extract colors from segments
                int yLen = blockData[4];
                int idx = 5;

                for (int row = 0; row < yLen && idx < blockData.Length; row++)
                {
                    if (idx >= blockData.Length) break;
                    int segCount = blockData[idx++];

                    for (int s = 0; s < segCount && idx < blockData.Length; s++)
                    {
                        if (idx + 1 >= blockData.Length) break;
                        int skip = blockData[idx++];
                        int count = blockData[idx++];

                        for (int p = 0; p < count && idx + 1 < blockData.Length; p++)
                        {
                            ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                            if (color != 0)
                                colors.Add(color);
                            idx += 2;
                        }
                    }
                }
            }

            return colors;
        }

        static (int r, int g, int b) Rgb555ToComponents(ushort rgb555)
        {
            int b = rgb555 & 0x1F;
            int g = (rgb555 >> 5) & 0x1F;
            int r = (rgb555 >> 10) & 0x1F;
            return (r, g, b);
        }

        static void DebugMTil(string filePath, int blockIndex)
        {
            Console.WriteLine($"=== MTil Debug: {Path.GetFileName(filePath)} ===");
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found: {filePath}");
                return;
            }

            try
            {
                var parsed = MTil.ParseFile(filePath);

                Console.WriteLine($"Block Count: {parsed.BlockCount}");
                Console.WriteLine($"Has Global Palette: {parsed.HasGlobalPalette}");
                Console.WriteLine($"Palette Size: {parsed.GlobalPalette.Length}");
                Console.WriteLine();

                // 輸出前 20 個 palette 顏色
                Console.WriteLine("Global Palette (first 20):");
                for (int i = 0; i < Math.Min(20, parsed.GlobalPalette.Length); i++)
                {
                    ushort c = parsed.GlobalPalette[i];
                    int r = (c >> 10) & 0x1F;
                    int g = (c >> 5) & 0x1F;
                    int b = c & 0x1F;
                    Console.WriteLine($"  [{i,3}] 0x{c:X4} -> R={r,2}, G={g,2}, B={b,2}");
                }
                Console.WriteLine();

                // 輸出指定 block 或所有 block 的摘要
                if (blockIndex >= 0 && blockIndex < parsed.Blocks.Count)
                {
                    var block = parsed.Blocks[blockIndex];
                    PrintBlockDebug(block, parsed.GlobalPalette);
                }
                else
                {
                    // 輸出所有 block 的 flags 摘要
                    Console.WriteLine("Block Flags Summary:");
                    var flagGroups = parsed.Blocks.GroupBy(b => b.Flags).OrderBy(g => g.Key);
                    foreach (var group in flagGroups)
                    {
                        byte flags = group.Key;
                        bool isDefault = (flags & 0x40) != 0;
                        bool useTableB = (flags & 0x01) != 0;
                        Console.WriteLine($"  Flags 0x{flags:X2}: {group.Count()} blocks (IsDefault={isDefault}, UseTableB={useTableB})");
                    }
                    Console.WriteLine();

                    // 輸出前 5 個 block 的詳細信息
                    Console.WriteLine("First 5 blocks:");
                    for (int i = 0; i < Math.Min(5, parsed.Blocks.Count); i++)
                    {
                        var block = parsed.Blocks[i];
                        Console.WriteLine($"  Block {i}: Flags=0x{block.Flags:X2}, IsDefault={block.IsDefault}, UseTableB={block.UseTableB}, Pixels={block.Pixels.Length}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void DebugRenderBlock(string filePath, int blockIndex)
        {
            var parsed = MTil.ParseFile(filePath);
            if (blockIndex < 0 || blockIndex >= parsed.Blocks.Count)
            {
                Console.WriteLine($"Block {blockIndex} out of range");
                return;
            }

            var block = parsed.Blocks[blockIndex];
            Console.WriteLine($"Block {blockIndex}:");
            Console.WriteLine($"  Flags: 0x{block.Flags:X2}, IsDefault: {block.IsDefault}");
            Console.WriteLine($"  RleData.Count: {block.RleData.Count}");
            Console.WriteLine($"  Pixels.Length: {block.Pixels.Length}");

            // 模擬渲染
            var canvas = new ushort[24 * 24];
            var pixelColors = new List<ushort>();
            foreach (byte idx in block.Pixels)
            {
                if (idx < parsed.GlobalPalette.Length)
                    pixelColors.Add(MTil.Rgb565ToRgb555(parsed.GlobalPalette[idx]));
                else
                    pixelColors.Add(0);
            }

            Console.WriteLine($"  PixelColors.Count: {pixelColors.Count}");
            Console.WriteLine($"  First 5 colors: {string.Join(", ", pixelColors.Take(5).Select(c => $"0x{c:X4}"))}");

            // RLE 渲染
            var rleEntries = new List<(int skip, int draw, int rowFlag)>();
            if (block.RleData.Count > 0)
            {
                foreach (ushort rleValue in block.RleData)
                {
                    int skip = rleValue & 0x1F;
                    int draw = (rleValue >> 5) & 0x1F;
                    int rowFlag = (rleValue >> 10) & 0x1F;
                    rleEntries.Add((skip, draw, rowFlag));
                }
            }

            Console.WriteLine($"  RLE entries: {rleEntries.Count}");
            Console.WriteLine($"  First 3 RLE: {string.Join(", ", rleEntries.Take(3).Select(e => $"({e.skip},{e.draw},{e.rowFlag})"))}");

            int pixelIdx = 0, row = 0, xBase = 0, placed = 0;
            foreach (var (skip, draw, rowFlag) in rleEntries)
            {
                int x = xBase + skip;
                for (int j = 0; j < draw && pixelIdx < pixelColors.Count; j++)
                {
                    int px = x + j;
                    if (px >= 0 && px < 24 && row >= 0 && row < 24)
                    {
                        canvas[row * 24 + px] = pixelColors[pixelIdx];
                        placed++;
                    }
                    pixelIdx++;
                }
                if (rowFlag > 0) { row += rowFlag; xBase = 0; }
                else { xBase = x + draw; }
            }

            Console.WriteLine($"  Pixels placed: {placed}");
            int nonZero = canvas.Count(c => c != 0);
            Console.WriteLine($"  Non-zero in canvas: {nonZero}");
        }

        static void PrintBlockDebug(MTil.MBlock block, ushort[] globalPalette)
        {
            Console.WriteLine($"Block {block.Index}:");
            Console.WriteLine($"  Flags: 0x{block.Flags:X2}");
            Console.WriteLine($"  IsDefault: {block.IsDefault}");
            Console.WriteLine($"  UseTableB: {block.UseTableB}");
            Console.WriteLine($"  Width: {block.Width}");
            Console.WriteLine($"  Height: {block.Height}");
            Console.WriteLine($"  ColorCount: {block.ColorCount}");
            Console.WriteLine($"  DataSize: {block.DataSize}");
            Console.WriteLine($"  Pixels Length: {block.Pixels.Length}");
            Console.WriteLine($"  RleData Count: {block.RleData.Count}");
            Console.WriteLine();

            // 輸出前 10 個 pixel indices 和對應顏色
            Console.WriteLine("  First 10 pixel indices and colors:");
            for (int i = 0; i < Math.Min(10, block.Pixels.Length); i++)
            {
                byte idx = block.Pixels[i];
                if (idx < globalPalette.Length)
                {
                    ushort c = globalPalette[idx];
                    int r = (c >> 10) & 0x1F;
                    int g = (c >> 5) & 0x1F;
                    int b = c & 0x1F;
                    Console.WriteLine($"    [{i}] idx={idx,3} -> 0x{c:X4} (R={r,2}, G={g,2}, B={b,2})");
                }
                else
                {
                    Console.WriteLine($"    [{i}] idx={idx,3} -> OUT OF RANGE");
                }
            }
        }

        #endregion

        #region Tile Sheet Generation

        static void GenerateTileCompareSheet(string til1Path, string til2Path, string outputPath)
        {
            Console.WriteLine($"=== Generate Tile Compare Sheet ===");
            Console.WriteLine($"  til1: {til1Path}");
            Console.WriteLine($"  til2: {til2Path}");
            Console.WriteLine($"  output: {outputPath}");

            if (!File.Exists(til1Path))
            {
                Console.WriteLine($"Error: File not found: {til1Path}");
                return;
            }
            if (!File.Exists(til2Path))
            {
                Console.WriteLine($"Error: File not found: {til2Path}");
                return;
            }

            try
            {
                var blocks1 = L1Til.Parse(File.ReadAllBytes(til1Path));
                var blocks2 = L1Til.Parse(File.ReadAllBytes(til2Path));

                // 16x16 grid = 256 blocks, each block 24x24
                // Sheet: til1 on left, til2 on right
                // Each tile sheet: 16 blocks wide * 24 pixels = 384 pixels
                // 16 blocks tall * 24 pixels = 384 pixels
                // Total width: 384 * 2 + 10 (gap) = 778 pixels
                int blockSize = 24;
                int gridSize = 16;
                int tileSheetSize = gridSize * blockSize; // 384
                int gap = 10;
                int totalWidth = tileSheetSize * 2 + gap;
                int totalHeight = tileSheetSize + 40; // Extra space for labels

                using var bmp = new Bitmap(totalWidth, totalHeight);
                using var g = Graphics.FromImage(bmp);

                // Background
                g.Clear(Color.FromArgb(40, 40, 40));

                // Draw labels
                using var font = new Font("Arial", 10);
                using var brush = new SolidBrush(Color.White);
                g.DrawString($"til1: {Path.GetFileName(til1Path)}", font, brush, 5, 5);
                g.DrawString($"til2: {Path.GetFileName(til2Path)}", font, brush, tileSheetSize + gap + 5, 5);

                int yOffset = 25;

                // Render til1 blocks
                for (int i = 0; i < Math.Min(256, blocks1.Count); i++)
                {
                    int x = (i % gridSize) * blockSize;
                    int y = (i / gridSize) * blockSize + yOffset;
                    RenderBlockToBitmap(bmp, blocks1[i], x, y);
                }

                // Render til2 blocks
                for (int i = 0; i < Math.Min(256, blocks2.Count); i++)
                {
                    int x = tileSheetSize + gap + (i % gridSize) * blockSize;
                    int y = (i / gridSize) * blockSize + yOffset;
                    RenderBlockToBitmap(bmp, blocks2[i], x, y);
                }

                // Save
                bmp.Save(outputPath, ImageFormat.Png);
                Console.WriteLine($"Saved: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void GenerateTileCompareSheetBatch(string til1Dir, string til2Dir, string outputDir, double threshold, int maxTileId)
        {
            Console.WriteLine($"=== Batch Generate Tile Compare Sheets ===");
            Console.WriteLine($"  til1 dir:  {til1Dir}");
            Console.WriteLine($"  til2 dir:  {til2Dir}");
            Console.WriteLine($"  output:    {outputDir}");
            Console.WriteLine($"  threshold: {threshold}");
            Console.WriteLine($"  max tile:  {(maxTileId == int.MaxValue ? "all" : maxTileId.ToString())}");
            Console.WriteLine();

            if (!Directory.Exists(til1Dir))
            {
                Console.WriteLine($"Error: Directory not found: {til1Dir}");
                return;
            }
            if (!Directory.Exists(til2Dir))
            {
                Console.WriteLine($"Error: Directory not found: {til2Dir}");
                return;
            }

            Directory.CreateDirectory(outputDir);

            var til1Files = Directory.GetFiles(til1Dir, "*.til")
                .Select(f => Path.GetFileName(f))
                .ToList();

            int generated = 0;
            int skipped = 0;

            foreach (var til1Name in til1Files.OrderBy(f => {
                var match = System.Text.RegularExpressions.Regex.Match(f, @"(\d+)\.til");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }))
            {
                var match = System.Text.RegularExpressions.Regex.Match(til1Name, @"(\d+)\.til");
                int tileId = match.Success ? int.Parse(match.Groups[1].Value) : 0;

                if (tileId >= maxTileId)
                {
                    skipped++;
                    continue;
                }

                string til1Path = Path.Combine(til1Dir, til1Name);
                string til2Path = Path.Combine(til2Dir, til1Name);

                if (!File.Exists(til2Path))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    // Check if this tile has significant differences
                    var blocks1 = L1Til.Parse(File.ReadAllBytes(til1Path));
                    var blocks2 = L1Til.Parse(File.ReadAllBytes(til2Path));

                    double maxDiff = 0;
                    int minBlocks = Math.Min(blocks1.Count, blocks2.Count);

                    for (int i = 0; i < minBlocks; i++)
                    {
                        var colors1 = ExtractBlockColors(blocks1[i]);
                        var colors2 = ExtractBlockColors(blocks2[i]);

                        if (colors1.Count == 0 || colors2.Count == 0)
                            continue;

                        double totalDiff = 0;
                        int compared = 0;
                        int minColors = Math.Min(colors1.Count, colors2.Count);

                        for (int c = 0; c < minColors; c++)
                        {
                            var (r1, g1, b1) = Rgb555ToComponents(colors1[c]);
                            var (r2, g2, b2) = Rgb555ToComponents(colors2[c]);
                            totalDiff += Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2);
                            compared++;
                        }

                        double avgDiff = compared > 0 ? totalDiff / compared : 0;
                        maxDiff = Math.Max(maxDiff, avgDiff);
                    }

                    if (maxDiff >= threshold)
                    {
                        string outputPath = Path.Combine(outputDir, $"{tileId}_compare.png");
                        GenerateTileCompareSheetInternal(blocks1, blocks2, til1Name, til1Name, outputPath);
                        generated++;
                        Console.WriteLine($"Generated: {tileId}_compare.png (maxDiff={maxDiff:F1})");
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing tile {tileId}: {ex.Message}");
                    skipped++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Generated: {generated} sheets");
            Console.WriteLine($"Skipped: {skipped} tiles");
        }

        static void GenerateTileCompareSheetInternal(List<byte[]> blocks1, List<byte[]> blocks2, string label1, string label2, string outputPath)
        {
            int blockSize = 24;
            int gridSize = 16;
            int tileSheetSize = gridSize * blockSize;
            int gap = 10;
            int totalWidth = tileSheetSize * 2 + gap;
            int totalHeight = tileSheetSize + 40;

            using var bmp = new Bitmap(totalWidth, totalHeight);
            using var g = Graphics.FromImage(bmp);

            g.Clear(Color.FromArgb(40, 40, 40));

            using var font = new Font("Arial", 10);
            using var brush = new SolidBrush(Color.White);
            g.DrawString($"til1: {label1}", font, brush, 5, 5);
            g.DrawString($"til2: {label2}", font, brush, tileSheetSize + gap + 5, 5);

            int yOffset = 25;

            for (int i = 0; i < Math.Min(256, blocks1.Count); i++)
            {
                int x = (i % gridSize) * blockSize;
                int y = (i / gridSize) * blockSize + yOffset;
                RenderBlockToBitmap(bmp, blocks1[i], x, y);
            }

            for (int i = 0; i < Math.Min(256, blocks2.Count); i++)
            {
                int x = tileSheetSize + gap + (i % gridSize) * blockSize;
                int y = (i / gridSize) * blockSize + yOffset;
                RenderBlockToBitmap(bmp, blocks2[i], x, y);
            }

            bmp.Save(outputPath, ImageFormat.Png);
        }

        static void RenderBlockToBitmap(Bitmap bmp, byte[] blockData, int destX, int destY)
        {
            if (blockData == null || blockData.Length < 2)
                return;

            int blockType = blockData[0];
            bool isSimpleDiamond = blockType == 0 || blockType == 1 || blockType == 8 || blockType == 9 ||
                                   blockType == 16 || blockType == 17;

            // Decode to 24x24 canvas
            var canvas = new ushort[24 * 24];

            if (isSimpleDiamond)
            {
                // Diamond pixel layout
                int[] rowWidths = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 22, 20, 18, 16, 14, 12, 10, 8, 6, 4, 2 };
                int pixelIdx = 0;
                int dataIdx = 1;

                for (int row = 0; row < 23 && dataIdx < blockData.Length - 1; row++)
                {
                    int width = rowWidths[row];
                    int startX = (blockType & 1) == 0 ? (24 - width) / 2 : 0; // type 0=centered, type 1=left

                    for (int col = 0; col < width && dataIdx + 1 < blockData.Length; col++)
                    {
                        ushort color = (ushort)(blockData[dataIdx] | (blockData[dataIdx + 1] << 8));
                        dataIdx += 2;

                        int x = startX + col;
                        int y = row;
                        if (x >= 0 && x < 24 && y >= 0 && y < 24)
                            canvas[y * 24 + x] = color;
                    }
                }
            }
            else if (blockData.Length >= 5)
            {
                // Compressed format
                int xOffset = blockData[1];
                int yOffsetBlock = blockData[2];
                int xxLen = blockData[3];
                int yLen = blockData[4];
                int idx = 5;

                for (int row = 0; row < yLen && idx < blockData.Length; row++)
                {
                    if (idx >= blockData.Length) break;
                    int segCount = blockData[idx++];

                    int currentX = xOffset;
                    for (int s = 0; s < segCount && idx < blockData.Length; s++)
                    {
                        if (idx + 1 >= blockData.Length) break;
                        int skip = blockData[idx++];
                        int count = blockData[idx++];

                        currentX += skip / 2; // skip is in bytes

                        for (int p = 0; p < count && idx + 1 < blockData.Length; p++)
                        {
                            ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                            idx += 2;

                            int x = currentX + p;
                            int y = yOffsetBlock + row;
                            if (x >= 0 && x < 24 && y >= 0 && y < 24)
                                canvas[y * 24 + x] = color;
                        }
                        currentX += count;
                    }
                }
            }

            // Copy canvas to bitmap
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    ushort rgb555 = canvas[y * 24 + x];
                    Color color;

                    if (rgb555 == 0)
                    {
                        color = Color.FromArgb(30, 30, 30); // Dark background for transparent
                    }
                    else
                    {
                        int b5 = rgb555 & 0x1F;
                        int g5 = (rgb555 >> 5) & 0x1F;
                        int r5 = (rgb555 >> 10) & 0x1F;
                        int r8 = (r5 << 3) | (r5 >> 2);
                        int g8 = (g5 << 3) | (g5 >> 2);
                        int b8 = (b5 << 3) | (b5 >> 2);
                        color = Color.FromArgb(r8, g8, b8);
                    }

                    int px = destX + x;
                    int py = destY + y;
                    if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                        bmp.SetPixel(px, py, color);
                }
            }
        }

        #endregion
    }
}
