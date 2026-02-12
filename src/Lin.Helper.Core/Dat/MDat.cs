using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;

namespace Lin.Helper.Core.Dat
{
    /// <summary>
    /// ZIP-based .dat 容器狀態
    /// </summary>
    public enum MDatStatus
    {
        /// <summary>ZIP 無保護，可直接讀取</summary>
        Available,
        /// <summary>ZIP 有密碼保護</summary>
        Protected,
        /// <summary>非 ZIP 格式，無法讀取</summary>
        Sealed
    }

    /// <summary>
    /// ZIP-based .dat 容器條目
    /// </summary>
    public class MDatEntry
    {
        public int Index { get; internal set; }
        public string FileName { get; internal set; }
        public long CompressedSize { get; internal set; }
        public long UncompressedSize { get; internal set; }

        public override string ToString()
        {
            return $"[{Index}] {FileName} ({CompressedSize}/{UncompressedSize})";
        }
    }

    /// <summary>
    /// ZIP-based .dat 容器讀取器。
    /// 支援三種狀態: Available (無保護), Protected (密碼保護), Sealed (非 ZIP)。
    /// .ti2 條目提取時自動進行 Brotli 解壓。
    /// </summary>
    public class MDat : IDisposable
    {
        private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

        private readonly string _filePath;
        private readonly string _password;
        private readonly List<MDatEntry> _entries;
        private ZipFile _zipFile;
        private bool _disposed;

        /// <summary>
        /// 開啟 .dat 容器
        /// </summary>
        /// <param name="datPath">.dat 檔案路徑</param>
        /// <param name="password">ZIP 密碼 (null 表示無密碼)</param>
        public MDat(string datPath, string password = null)
        {
            if (!File.Exists(datPath))
                throw new FileNotFoundException("DAT file not found", datPath);

            _filePath = datPath;
            _password = password;
            _entries = new List<MDatEntry>();

            Status = DetectStatus(datPath);

            if (Status == MDatStatus.Sealed)
                return;

            OpenAndLoadEntries();
        }

        public string FilePath => _filePath;
        public string FileName => Path.GetFileName(_filePath);
        public int Count => _entries.Count;
        public MDatStatus Status { get; private set; }
        public IReadOnlyList<MDatEntry> Entries => _entries.AsReadOnly();

        /// <summary>
        /// 偵測 .dat 檔案的狀態
        /// </summary>
        public static MDatStatus DetectStatus(string datPath)
        {
            if (!File.Exists(datPath))
                return MDatStatus.Sealed;

            try
            {
                using var fs = new FileStream(datPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 4)
                    return MDatStatus.Sealed;

                byte[] magic = new byte[4];
                fs.ReadExactly(magic, 0, 4);

                if (magic[0] != ZipMagic[0] || magic[1] != ZipMagic[1]
                    || magic[2] != ZipMagic[2] || magic[3] != ZipMagic[3])
                    return MDatStatus.Sealed;
            }
            catch
            {
                return MDatStatus.Sealed;
            }

            // 用 SharpZipLib 檢查是否有密碼保護
            try
            {
                using var zf = new ZipFile(datPath);
                foreach (ZipEntry entry in zf)
                {
                    if (!entry.IsFile) continue;
                    if (entry.IsCrypted)
                        return MDatStatus.Protected;
                }
                return MDatStatus.Available;
            }
            catch
            {
                return MDatStatus.Sealed;
            }
        }

        /// <summary>
        /// 從檔名取得類型前綴 (e.g. "Image1.dat" → "Image")
        /// </summary>
        public static string GetTypePrefix(string fileName)
        {
            var match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"^([A-Za-z]+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        private void OpenAndLoadEntries()
        {
            _zipFile = new ZipFile(_filePath);

            if (!string.IsNullOrEmpty(_password))
                _zipFile.Password = _password;

            int index = 0;
            foreach (ZipEntry entry in _zipFile)
            {
                if (!entry.IsFile) continue;

                _entries.Add(new MDatEntry
                {
                    Index = index++,
                    FileName = entry.Name,
                    CompressedSize = entry.CompressedSize,
                    UncompressedSize = entry.Size
                });
            }
        }

        /// <summary>
        /// 提取條目 (by index)。.ti2 自動 Brotli 解壓。
        /// </summary>
        public byte[] Extract(int index)
        {
            if (index < 0 || index >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Extract(_entries[index]);
        }

        /// <summary>
        /// 提取條目。.ti2 自動 Brotli 解壓。
        /// </summary>
        public byte[] Extract(MDatEntry entry)
        {
            byte[] raw = ExtractRaw(entry);

            if (entry.FileName.EndsWith(".ti2", StringComparison.OrdinalIgnoreCase))
                return DecompressBrotli(raw);

            return raw;
        }

        /// <summary>
        /// 提取原始條目資料 (不做後處理)
        /// </summary>
        public byte[] ExtractRaw(MDatEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (_zipFile == null)
                throw new InvalidOperationException("DAT file is not open or is sealed");

            var zipEntry = _zipFile.GetEntry(entry.FileName);
            if (zipEntry == null)
                throw new InvalidOperationException($"Entry not found in ZIP: {entry.FileName}");

            using var stream = _zipFile.GetInputStream(zipEntry);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static byte[] DecompressBrotli(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using (var brotli = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress))
            {
                brotli.CopyTo(output);
            }
            return output.ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _zipFile?.Close();
                _zipFile = null;
                _disposed = true;
            }
        }
    }
}
