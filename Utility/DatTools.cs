using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PakViewer.Utility
{
    /// <summary>
    /// Lineage M DAT 檔案解密工具
    /// </summary>
    public class DatTools
    {
        private static readonly byte[] KEY_SOURCE_DATA = new byte[]
        {
            0xd9, 0xcf, 0x86, 0xdf, 0xd0, 0xf8, 0xfd, 0xd3,
            0xcd, 0xf1, 0x80, 0x97, 0xc6, 0xf6, 0xc6, 0x95
        };

        // AES-128 Key (XOR 0xAA)
        private static readonly byte[] AES_KEY;

        // 常見副檔名
        private static readonly string[] EXTENSIONS = new string[]
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
            ".mp3", ".ogg", ".wav", ".json", ".xml", ".txt",
            ".dat", ".bin", ".atlas", ".skel", ".fnt", ".rar"
        };

        static DatTools()
        {
            AES_KEY = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                AES_KEY[i] = (byte)(KEY_SOURCE_DATA[i] ^ 0xAA);
            }
        }

        /// <summary>
        /// 從 DAT 文件名生成 IV
        /// 方法: 文件名循環擴展到 16 bytes
        /// 例如: "Image39.dat" (11 bytes) -> "Image39.datImage" (16 bytes)
        /// </summary>
        public static byte[] GetIVFromFilename(string filename)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(filename);
            byte[] iv = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                iv[i] = nameBytes[i % nameBytes.Length];
            }

            return iv;
        }

        /// <summary>
        /// AES-128-CBC 解密
        /// </summary>
        public static byte[] DecryptAesCbc(byte[] data, byte[] key, byte[] iv)
        {
            // Pad to 16-byte boundary
            int paddedLength = data.Length;
            if (paddedLength % 16 != 0)
            {
                paddedLength += 16 - (paddedLength % 16);
            }

            byte[] paddedData = new byte[paddedLength];
            Array.Copy(data, paddedData, data.Length);

            using (var aes = System.Security.Cryptography.Aes.Create("AES"))
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(paddedData, 0, paddedData.Length);
                }
            }
        }

        /// <summary>
        /// DAT 檔案 Footer 結構 (最後 9 bytes)
        /// </summary>
        public class DatFooter
        {
            public const int SIZE = 9;

            public int IndexOffset { get; private set; }
            public int IndexSize { get; private set; }
            public bool IsEncrypted { get; private set; }

            public DatFooter(byte[] data)
            {
                if (data == null || data.Length < SIZE)
                    throw new ArgumentException($"Footer data too short: {data?.Length ?? 0} < {SIZE}");

                IndexOffset = BitConverter.ToInt32(data, 0);
                IndexSize = BitConverter.ToInt32(data, 4);
                IsEncrypted = data[8] != 0;
            }

            public override string ToString()
            {
                string encStr = IsEncrypted ? "encrypted" : "plain";
                return $"Footer(offset=0x{IndexOffset:X8}, size={IndexSize}, {encStr})";
            }
        }

        /// <summary>
        /// DAT 檔案內的索引條目
        /// </summary>
        public class DatIndexEntry
        {
            public string Path { get; set; }
            public int Offset { get; set; }
            public int Size { get; set; }
            public byte Type { get; set; }

            /// <summary>
            /// 來源 DAT 檔案完整路徑
            /// </summary>
            public string SourceDatFile { get; set; }

            /// <summary>
            /// 來源 DAT 檔案名稱
            /// </summary>
            public string SourceDatName { get; set; }

            public DatIndexEntry(string path, int offset, int size, string sourceDatFile = null)
            {
                Path = path;
                Offset = offset;
                Size = size;
                SourceDatFile = sourceDatFile;
                SourceDatName = string.IsNullOrEmpty(sourceDatFile) ? null : System.IO.Path.GetFileName(sourceDatFile);
            }

            public override string ToString()
            {
                return $"Entry('{Path}', offset=0x{Offset:X}, size={Size})";
            }
        }

        /// <summary>
        /// DAT 檔案處理器
        /// </summary>
        public class DatFile
        {
            public string FilePath { get; private set; }
            public string FileName { get; private set; }
            public long FileSize { get; private set; }
            public byte[] IV { get; private set; }
            public DatFooter Footer { get; private set; }
            public byte[] IndexData { get; private set; }
            public List<DatIndexEntry> Entries { get; private set; }

            public DatFile(string filepath)
            {
                FilePath = filepath;
                FileName = Path.GetFileName(filepath);
                FileSize = new FileInfo(filepath).Length;
                IV = GetIVFromFilename(FileName);
                Entries = new List<DatIndexEntry>();
            }

            /// <summary>
            /// 讀取 Footer (最後 9 bytes)
            /// </summary>
            public DatFooter ReadFooter()
            {
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(-DatFooter.SIZE, SeekOrigin.End);
                    byte[] footerData = new byte[DatFooter.SIZE];
                    fs.Read(footerData, 0, DatFooter.SIZE);
                    Footer = new DatFooter(footerData);
                }
                return Footer;
            }

            /// <summary>
            /// 解密索引區
            /// </summary>
            public byte[] DecryptIndex()
            {
                if (Footer == null)
                    ReadFooter();

                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(Footer.IndexOffset, SeekOrigin.Begin);
                    byte[] encryptedIndex = new byte[Footer.IndexSize];
                    fs.Read(encryptedIndex, 0, Footer.IndexSize);

                    if (Footer.IsEncrypted)
                    {
                        IndexData = DecryptAesCbc(encryptedIndex, AES_KEY, IV);
                    }
                    else
                    {
                        IndexData = encryptedIndex;
                    }
                }

                return IndexData;
            }

            /// <summary>
            /// 解析索引條目
            /// </summary>
            public List<DatIndexEntry> ParseEntries()
            {
                if (IndexData == null)
                    DecryptIndex();

                Entries = ParseIndex(IndexData, FilePath);
                return Entries;
            }

            /// <summary>
            /// 解密內容檔案
            /// </summary>
            public byte[] DecryptContent(byte[] encryptedData)
            {
                return DecryptAesCbc(encryptedData, AES_KEY, IV);
            }

            /// <summary>
            /// 提取並解密單個檔案
            /// </summary>
            public byte[] ExtractFile(DatIndexEntry entry, bool decrypt = true)
            {
                byte[] data;
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    data = new byte[entry.Size];
                    fs.Read(data, 0, entry.Size);
                }

                if (decrypt && Footer != null && Footer.IsEncrypted)
                {
                    data = DecryptContent(data);
                    // 移除可能的 padding (解密後大小可能超過原始大小)
                    if (data.Length > entry.Size)
                    {
                        byte[] trimmed = new byte[entry.Size];
                        Array.Copy(data, trimmed, entry.Size);
                        return trimmed;
                    }
                }

                return data;
            }

            /// <summary>
            /// 提取所有檔案到指定目錄
            /// </summary>
            public (int extracted, int errors) ExtractAll(string outputDir, Action<int, int, string> progressCallback = null)
            {
                if (Entries == null || Entries.Count == 0)
                    ParseEntries();

                Directory.CreateDirectory(outputDir);

                int extracted = 0;
                int errors = 0;

                for (int i = 0; i < Entries.Count; i++)
                {
                    var entry = Entries[i];
                    try
                    {
                        string safePath = entry.Path.TrimStart('/', '\\');
                        string destPath = Path.Combine(outputDir, safePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                        byte[] data = ExtractFile(entry);
                        File.WriteAllBytes(destPath, data);

                        extracted++;
                        progressCallback?.Invoke(i + 1, Entries.Count, entry.Path);
                    }
                    catch (Exception)
                    {
                        errors++;
                    }
                }

                return (extracted, errors);
            }

            /// <summary>
            /// 將 DAT 內容匯出為 ZIP
            /// </summary>
            public void ExportToZip(string zipPath, Action<int, int, string> progressCallback = null)
            {
                if (Entries == null || Entries.Count == 0)
                    ParseEntries();

                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < Entries.Count; i++)
                    {
                        var entry = Entries[i];
                        try
                        {
                            string entryPath = entry.Path.TrimStart('/', '\\').Replace('\\', '/');
                            var zipEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

                            byte[] data = ExtractFile(entry);
                            using (var entryStream = zipEntry.Open())
                            {
                                entryStream.Write(data, 0, data.Length);
                            }

                            progressCallback?.Invoke(i + 1, Entries.Count, entry.Path);
                        }
                        catch (Exception)
                        {
                            // 跳過失敗的檔案
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 解析解密後的索引數據
        ///
        /// Index Entry 結構：
        /// - 4 bytes: path_len (路徑長度，little-endian)
        /// - path_len bytes: path (路徑字串)
        /// - 4 bytes: offset (檔案偏移，little-endian)
        /// - 4 bytes: size (檔案大小，little-endian)
        /// - 1 byte: type (類型)
        /// </summary>
        public static List<DatIndexEntry> ParseIndex(byte[] data, string sourceDatFile = null)
        {
            var entries = new List<DatIndexEntry>();
            int pos = 0;

            while (pos + 13 <= data.Length)  // 最小 entry 大小: 4 + 0 + 4 + 4 + 1 = 13
            {
                try
                {
                    // 讀取 path_len (4 bytes, little-endian)
                    int pathLen = BitConverter.ToInt32(data, pos);

                    // 驗證 path_len 合理性
                    if (pathLen <= 0 || pathLen > 500 || pos + 4 + pathLen + 9 > data.Length)
                    {
                        // 無效的 entry，可能已經到達結尾或資料損壞
                        break;
                    }

                    // 讀取 path (path_len bytes)
                    string path = Encoding.UTF8.GetString(data, pos + 4, pathLen);

                    // 讀取 offset (4 bytes, little-endian)
                    int offset = BitConverter.ToInt32(data, pos + 4 + pathLen);

                    // 讀取 size (4 bytes, little-endian)
                    int size = BitConverter.ToInt32(data, pos + 4 + pathLen + 4);

                    // 讀取 type (1 byte)
                    byte type = data[pos + 4 + pathLen + 8];

                    // 驗證資料合理性
                    if (size >= 0 && size < 500_000_000 && offset >= 0)
                    {
                        var entry = new DatIndexEntry(path, offset, size, sourceDatFile);
                        entry.Type = type;
                        entries.Add(entry);
                    }

                    // 移動到下一個 entry
                    pos += 4 + pathLen + 4 + 4 + 1;
                }
                catch
                {
                    // 解析錯誤，結束
                    break;
                }
            }

            return entries;
        }

        private static bool StartsWith(byte[] data, int offset, byte[] prefix)
        {
            if (offset + prefix.Length > data.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[offset + i] != prefix[i])
                    return false;
            }
            return true;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start, int end)
        {
            for (int i = start; i <= end - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 檢查檔案是否為 DAT 格式
        /// </summary>
        public static bool IsDatFile(string filepath)
        {
            if (!File.Exists(filepath))
                return false;

            string ext = Path.GetExtension(filepath).ToLower();
            if (ext != ".dat")
                return false;

            try
            {
                var fi = new FileInfo(filepath);
                if (fi.Length < DatFooter.SIZE + 1)
                    return false;

                // 讀取 footer 檢查格式
                using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(-DatFooter.SIZE, SeekOrigin.End);
                    byte[] footerData = new byte[DatFooter.SIZE];
                    fs.Read(footerData, 0, DatFooter.SIZE);

                    var footer = new DatFooter(footerData);

                    // 檢查 index offset 和 size 是否合理
                    return footer.IndexOffset >= 0 &&
                           footer.IndexOffset < fi.Length &&
                           footer.IndexSize > 0 &&
                           footer.IndexSize < fi.Length;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
