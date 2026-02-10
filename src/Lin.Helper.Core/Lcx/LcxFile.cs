using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using ZstdSharp;

namespace Lin.Helper.Core.Lcx
{
    /// <summary>
    /// LCX 條目資訊
    /// </summary>
    public class LcxEntry
    {
        /// <summary>
        /// 條目索引
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// 原始檔名 (去除 .lcf 後綴)
        /// </summary>
        public string FileName { get; internal set; }

        /// <summary>
        /// ZIP 內的完整路徑 (含 .lcf)
        /// </summary>
        public string EntryName { get; internal set; }

        /// <summary>
        /// ZIP 中的壓縮大小 (含 nonce + tag overhead)
        /// </summary>
        public long CompressedSize { get; internal set; }
    }

    /// <summary>
    /// LCX 打包檔讀寫器 (ZIP 容器 + LCF 條目格式)。
    /// LCF 條目格式: nonce(12) + payload(N) + tag(16)，
    /// 使用 ChaCha20-Poly1305 AEAD 轉換，轉換後資料為 Zstandard 壓縮格式。
    /// </summary>
    public class LcxFile : IDisposable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int OverheadSize = NonceSize + TagSize; // 28

        private readonly string _filePath;
        private readonly byte[][] _keys;
        private readonly List<LcxEntry> _entries;
        private bool _disposed;

        /// <summary>
        /// 開啟 LCX 檔案
        /// </summary>
        /// <param name="lcxPath">LCX 檔案路徑</param>
        /// <param name="keys">ChaCha20-Poly1305 金鑰列表 (32 bytes each)，依序嘗試</param>
        public LcxFile(string lcxPath, byte[][] keys)
        {
            if (!File.Exists(lcxPath))
                throw new FileNotFoundException("LCX file not found", lcxPath);
            if (keys == null || keys.Length == 0)
                throw new ArgumentException("At least one key is required", nameof(keys));

            _filePath = lcxPath;
            _keys = keys;
            _entries = new List<LcxEntry>();

            LoadEntries();
        }

        /// <summary>
        /// LCX 檔案路徑
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// 檔案名稱
        /// </summary>
        public string FileName => Path.GetFileName(_filePath);

        /// <summary>
        /// 條目數量
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// 條目列表
        /// </summary>
        public IReadOnlyList<LcxEntry> Entries => _entries.AsReadOnly();

        private void LoadEntries()
        {
            using var archive = ZipFile.OpenRead(_filePath);

            int index = 0;
            foreach (var entry in archive.Entries)
            {
                // 跳過目錄
                if (string.IsNullOrEmpty(entry.Name)) continue;

                string originalName = entry.FullName;
                if (originalName.EndsWith(".lcf", StringComparison.OrdinalIgnoreCase))
                    originalName = originalName.Substring(0, originalName.Length - 4);

                _entries.Add(new LcxEntry
                {
                    Index = index++,
                    FileName = originalName,
                    EntryName = entry.FullName,
                    CompressedSize = entry.CompressedLength
                });
            }
        }

        /// <summary>
        /// 提取條目: 讀取 ZIP → decode LCF → 解壓 Zstd → 原始資料
        /// </summary>
        public byte[] Extract(int index)
        {
            if (index < 0 || index >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Extract(_entries[index]);
        }

        /// <summary>
        /// 提取條目
        /// </summary>
        public byte[] Extract(LcxEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            byte[] lcfData;
            using (var archive = ZipFile.OpenRead(_filePath))
            {
                var zipEntry = archive.GetEntry(entry.EntryName);
                if (zipEntry == null)
                    throw new InvalidOperationException($"Entry not found in ZIP: {entry.EntryName}");

                using var stream = zipEntry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                lcfData = ms.ToArray();
            }

            byte[] decoded = DecodeLcf(lcfData);
            return DecompressZstd(decoded);
        }

        /// <summary>
        /// 儲存修改後的 LCX：將 replacements 中的條目替換後寫入新檔案。
        /// 使用第一把 key 進行 encode。
        /// </summary>
        /// <param name="outputPath">輸出路徑</param>
        /// <param name="replacements">要替換的條目 (ZIP entry name → 原始資料)</param>
        public void Save(string outputPath, Dictionary<string, byte[]> replacements)
        {
            if (replacements == null || replacements.Count == 0)
                return;

            File.Copy(_filePath, outputPath, true);

            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Update);

            foreach (var kv in replacements)
            {
                string entryName = kv.Key;
                if (!entryName.EndsWith(".lcf", StringComparison.OrdinalIgnoreCase))
                    entryName += ".lcf";

                var existing = archive.GetEntry(entryName);
                existing?.Delete();

                var newEntry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                byte[] compressed = CompressZstd(kv.Value);
                byte[] encoded = EncodeLcf(compressed, _keys[0]);

                using var entryStream = newEntry.Open();
                entryStream.Write(encoded, 0, encoded.Length);
            }
        }

        /// <summary>
        /// Decode LCF: nonce(12) | ciphertext(N) | tag(16) → plaintext
        /// </summary>
        private byte[] DecodeLcf(byte[] data)
        {
            if (data.Length < OverheadSize)
                throw new InvalidDataException(
                    $"LCF data too short ({data.Length} bytes, minimum {OverheadSize})");

            byte[] nonce = new byte[NonceSize];
            Array.Copy(data, 0, nonce, 0, NonceSize);

            // BouncyCastle expects: ciphertext + tag concatenated
            byte[] ciphertextAndTag = new byte[data.Length - NonceSize];
            Array.Copy(data, NonceSize, ciphertextAndTag, 0, ciphertextAndTag.Length);

            foreach (var key in _keys)
            {
                try
                {
                    var cipher = new ChaCha20Poly1305();
                    var parameters = new AeadParameters(
                        new KeyParameter(key), TagSize * 8, nonce);
                    cipher.Init(false, parameters);

                    byte[] output = new byte[cipher.GetOutputSize(ciphertextAndTag.Length)];
                    int len = cipher.ProcessBytes(ciphertextAndTag, 0, ciphertextAndTag.Length, output, 0);
                    len += cipher.DoFinal(output, len);

                    if (len < output.Length)
                        Array.Resize(ref output, len);

                    return output;
                }
                catch (Org.BouncyCastle.Crypto.InvalidCipherTextException)
                {
                    continue;
                }
            }

            throw new InvalidDataException(
                "All keys failed to decode the LCF entry. The data may be corrupted or the key is incorrect.");
        }

        /// <summary>
        /// Encode: plaintext → nonce(12) | ciphertext(N) | tag(16)
        /// </summary>
        private static byte[] EncodeLcf(byte[] plaintext, byte[] key)
        {
            byte[] nonce = new byte[NonceSize];
            System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);

            var cipher = new ChaCha20Poly1305();
            var parameters = new AeadParameters(
                new KeyParameter(key), TagSize * 8, nonce);
            cipher.Init(true, parameters);

            byte[] output = new byte[cipher.GetOutputSize(plaintext.Length)];
            int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
            len += cipher.DoFinal(output, len);

            // result = nonce + output (ciphertext + tag)
            byte[] result = new byte[NonceSize + len];
            Array.Copy(nonce, 0, result, 0, NonceSize);
            Array.Copy(output, 0, result, NonceSize, len);
            return result;
        }

        private static byte[] DecompressZstd(byte[] data)
        {
            if (data.Length >= 4 && data[0] == 0x28 && data[1] == 0xB5
                && data[2] == 0x2F && data[3] == 0xFD)
            {
                using var decompressor = new Decompressor();
                return decompressor.Unwrap(data).ToArray();
            }
            return data;
        }

        private static byte[] CompressZstd(byte[] data)
        {
            using var compressor = new Compressor();
            return compressor.Wrap(data).ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
