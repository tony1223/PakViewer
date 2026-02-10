using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Lin.Helper.Core.Pak
{
    /// <summary>
    /// IDX 格式解析結果
    /// </summary>
    internal sealed class IdxParseResult
    {
        public List<IndexRecord> Records;
        public bool IsProtected;
        public string EncryptionType;
    }

    /// <summary>
    /// IDX 格式處理器基底類別。每種 IDX magic header 對應一個子類別。
    /// </summary>
    internal abstract class IdxHandler
    {
        public abstract bool CanHandle(byte[] idxData);
        public abstract IdxHandler CreateInstance();
        public abstract IdxParseResult TryParse(byte[] idxData);
        public abstract byte[] ExtractEntry(FileStream pakStream, IndexRecord rec);

        public virtual int GetRawSize(IndexRecord rec) => rec.FileSize;
        public virtual bool CanWrite => false;
        public virtual byte[] EncodeEntry(byte[] rawData) => rawData;
        public virtual int MaxFileNameBytes => 19;

        public virtual byte[] BuildIndex(List<IndexRecord> records)
        {
            throw new NotSupportedException(
                $"Writing is not supported for this IDX format.");
        }

        /// <summary>
        /// 按偵測優先順序建立所有 handler prototype
        /// </summary>
        public static IdxHandler[] CreatePrototypes() => new IdxHandler[]
        {
            new ExtBHandler(),
            new ExtHandler(),
            new IdxV2Handler(),
            new OldL1Handler(),
            new OldDesHandler(),
        };

        #region 共用工具

        protected static readonly byte[] DesKey =
            { 0x7e, 0x21, 0x40, 0x23, 0x25, 0x5e, 0x24, 0x3c }; // ~!@#%^$<

        protected static void DesTransformInPlace(byte[] data, bool encrypt)
        {
            using (var des = DES.Create())
            {
                des.Key = DesKey;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;

                using (var transform = encrypt ? des.CreateEncryptor() : des.CreateDecryptor())
                {
                    int blocks = data.Length / 8;
                    byte[] block = new byte[8];
                    for (int i = 0; i < blocks; i++)
                    {
                        int offset = i * 8;
                        Array.Copy(data, offset, block, 0, 8);
                        byte[] result = transform.TransformFinalBlock(block, 0, 8);
                        Array.Copy(result, 0, data, offset, 8);
                    }
                }
            }
        }

        protected static bool IsValidFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            char first = name[0];
            return char.IsLetterOrDigit(first) || first == '_' || first == '.';
        }

        protected static byte[] DecompressBrotli(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var brotli = new BrotliStream(input, CompressionMode.Decompress))
            {
                brotli.CopyTo(output);
                return output.ToArray();
            }
        }

        #endregion
    }

    // ================================================================
    //  ExtBHandler — _EXTB$ magic, variable-length entries, read-only
    // ================================================================
    internal sealed class ExtBHandler : IdxHandler
    {
        public override bool CanHandle(byte[] data)
        {
            return data.Length >= 6
                && data[0] == '_' && data[1] == 'E' && data[2] == 'X'
                && data[3] == 'T' && data[4] == 'B' && data[5] == '$';
        }

        public override IdxHandler CreateInstance() => new ExtBHandler();

        public override IdxParseResult TryParse(byte[] idxData)
        {
            try
            {
                int pos = 6;
                int count = BitConverter.ToInt32(idxData, pos);
                pos += 4;

                var records = new List<IndexRecord>(count);

                for (int i = 0; i < count; i++)
                {
                    long offset = BitConverter.ToUInt32(idxData, pos); pos += 4;
                    int size = BitConverter.ToInt32(idxData, pos); pos += 4;
                    int compressedSize = BitConverter.ToInt32(idxData, pos); pos += 4;

                    int nameLen = 0;
                    while (pos + nameLen < idxData.Length && idxData[pos + nameLen] != 0)
                        nameLen++;

                    string fileName = Encoding.Default.GetString(idxData, pos, nameLen);
                    pos += nameLen + 1;

                    records.Add(new IndexRecord(fileName, size, offset)
                    {
                        CompressedSize = compressedSize
                    });
                }

                return new IdxParseResult
                {
                    Records = records,
                    IsProtected = true,
                    EncryptionType = "ExtB"
                };
            }
            catch { return null; }
        }

        public override byte[] ExtractEntry(FileStream pakStream, IndexRecord rec)
        {
            if (rec.CompressedSize > 0)
            {
                byte[] compressed = new byte[rec.CompressedSize];
                pakStream.Seek(rec.Offset, SeekOrigin.Begin);
                pakStream.ReadExactly(compressed, 0, rec.CompressedSize);

                // Brotli first, then Zlib fallback
                try { return DecompressBrotli(compressed); }
                catch
                {
                    try
                    {
                        using (var input = new MemoryStream(compressed, 2, compressed.Length - 2))
                        using (var output = new MemoryStream())
                        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                        {
                            deflate.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                    catch { return compressed; }
                }
            }

            byte[] data = new byte[rec.FileSize];
            pakStream.Seek(rec.Offset, SeekOrigin.Begin);
            pakStream.ReadExactly(data, 0, rec.FileSize);
            return data;
        }

        public override int GetRawSize(IndexRecord rec)
        {
            return rec.CompressedSize > 0 ? rec.CompressedSize : rec.FileSize;
        }

        public override int MaxFileNameBytes => 200;
    }

    // ================================================================
    //  ExtHandler — _EXT magic, 128-byte entries, DES+Brotli (V880)
    // ================================================================
    internal sealed class ExtHandler : IdxHandler
    {
        private bool _isDesEncrypted;

        public override bool CanHandle(byte[] data)
        {
            if (data.Length < 8) return false;
            return data[0] == '_' && data[1] == 'E' && data[2] == 'X' && data[3] == 'T'
                && !(data.Length >= 6 && data[4] == 'B' && data[5] == '$');
        }

        public override IdxHandler CreateInstance() => new ExtHandler();

        public override IdxParseResult TryParse(byte[] idxData)
        {
            try
            {
                int count = BitConverter.ToInt32(idxData, 4);
                if (count <= 0 || count > 200000) return null;
                if (idxData.Length < 8 + count * 128) return null;

                byte[] entriesData = new byte[count * 128];
                Array.Copy(idxData, 8, entriesData, 0, entriesData.Length);

                // 先嘗試不解密
                var records = ParseEntries(entriesData, count);
                if (records != null)
                {
                    _isDesEncrypted = false;
                    return new IdxParseResult
                    {
                        Records = records,
                        IsProtected = false,
                        EncryptionType = "Ext"
                    };
                }

                // 嘗試 DES 解密
                DesTransformInPlace(entriesData, false);
                records = ParseEntries(entriesData, count);
                if (records != null)
                {
                    _isDesEncrypted = true;
                    return new IdxParseResult
                    {
                        Records = records,
                        IsProtected = true,
                        EncryptionType = "Ext+DES"
                    };
                }

                return null;
            }
            catch { return null; }
        }

        public override byte[] ExtractEntry(FileStream pakStream, IndexRecord rec)
        {
            int readSize = (rec.Flags == 2 && rec.CompressedSize > 0)
                ? rec.CompressedSize : rec.FileSize;

            byte[] data = new byte[readSize];
            pakStream.Seek(rec.Offset, SeekOrigin.Begin);
            pakStream.ReadExactly(data, 0, readSize);

            if (_isDesEncrypted)
                DesTransformInPlace(data, false);

            if (rec.Flags == 2 && rec.CompressedSize > 0)
                data = DecompressBrotli(data);

            return data;
        }

        public override int GetRawSize(IndexRecord rec)
        {
            return (rec.Flags == 2 && rec.CompressedSize > 0)
                ? rec.CompressedSize : rec.FileSize;
        }

        public override bool CanWrite => true;
        public override int MaxFileNameBytes => 111;

        public override byte[] EncodeEntry(byte[] rawData)
        {
            if (_isDesEncrypted)
            {
                byte[] copy = (byte[])rawData.Clone();
                DesTransformInPlace(copy, true);
                return copy;
            }
            return rawData;
        }

        public override byte[] BuildIndex(List<IndexRecord> records)
        {
            byte[] entriesData = new byte[records.Count * 128];
            for (int i = 0; i < records.Count; i++)
            {
                records[i].ToExtBytes().CopyTo(entriesData, i * 128);
            }

            if (_isDesEncrypted)
                DesTransformInPlace(entriesData, true);

            byte[] result = new byte[8 + entriesData.Length];
            result[0] = (byte)'_'; result[1] = (byte)'E';
            result[2] = (byte)'X'; result[3] = (byte)'T';
            BitConverter.GetBytes(records.Count).CopyTo(result, 4);
            Array.Copy(entriesData, 0, result, 8, entriesData.Length);
            return result;
        }

        private static List<IndexRecord> ParseEntries(byte[] data, int count)
        {
            var records = new List<IndexRecord>(count);
            for (int i = 0; i < count; i++)
            {
                var rec = IndexRecord.FromExtEntry(data, i * 128);
                if (!IsValidFileName(rec.FileName)) return null;
                records.Add(rec);
            }
            return records;
        }
    }

    // ================================================================
    //  IdxV2Handler — _IDX magic, 32-byte entries
    // ================================================================
    internal sealed class IdxV2Handler : IdxHandler
    {
        private bool _isDesEncrypted;

        public override bool CanHandle(byte[] data)
        {
            return data.Length >= 8
                && data[0] == '_' && data[1] == 'I' && data[2] == 'D' && data[3] == 'X';
        }

        public override IdxHandler CreateInstance() => new IdxV2Handler();

        public override IdxParseResult TryParse(byte[] idxData)
        {
            try
            {
                int count = BitConverter.ToInt32(idxData, 4);
                if (count <= 0 || count > 200000) return null;
                if (idxData.Length < 8 + count * 32) return null;

                byte[] entriesData = new byte[count * 32];
                Array.Copy(idxData, 8, entriesData, 0, entriesData.Length);

                var records = ParseEntries(entriesData, count);
                if (records != null)
                {
                    _isDesEncrypted = false;
                    return new IdxParseResult
                    {
                        Records = records,
                        IsProtected = false,
                        EncryptionType = "Idx"
                    };
                }

                DesTransformInPlace(entriesData, false);
                records = ParseEntries(entriesData, count);
                if (records != null)
                {
                    _isDesEncrypted = true;
                    return new IdxParseResult
                    {
                        Records = records,
                        IsProtected = true,
                        EncryptionType = "Idx+DES"
                    };
                }

                return null;
            }
            catch { return null; }
        }

        public override byte[] ExtractEntry(FileStream pakStream, IndexRecord rec)
        {
            byte[] data = new byte[rec.FileSize];
            pakStream.Seek(rec.Offset, SeekOrigin.Begin);
            pakStream.ReadExactly(data, 0, rec.FileSize);
            if (_isDesEncrypted)
                DesTransformInPlace(data, false);
            return data;
        }

        public override bool CanWrite => true;

        public override byte[] EncodeEntry(byte[] rawData)
        {
            if (_isDesEncrypted)
            {
                byte[] copy = (byte[])rawData.Clone();
                DesTransformInPlace(copy, true);
                return copy;
            }
            return rawData;
        }

        public override byte[] BuildIndex(List<IndexRecord> records)
        {
            byte[] entriesData = new byte[records.Count * 32];
            for (int i = 0; i < records.Count; i++)
            {
                records[i].ToIdxV2Bytes().CopyTo(entriesData, i * 32);
            }

            if (_isDesEncrypted)
                DesTransformInPlace(entriesData, true);

            byte[] result = new byte[8 + entriesData.Length];
            result[0] = (byte)'_'; result[1] = (byte)'I';
            result[2] = (byte)'D'; result[3] = (byte)'X';
            BitConverter.GetBytes(records.Count).CopyTo(result, 4);
            Array.Copy(entriesData, 0, result, 8, entriesData.Length);
            return result;
        }

        private static List<IndexRecord> ParseEntries(byte[] data, int count)
        {
            var records = new List<IndexRecord>(count);
            for (int i = 0; i < count; i++)
            {
                int pos = i * 32;
                var rec = new IndexRecord
                {
                    Offset = BitConverter.ToUInt32(data, pos),
                    FileName = Encoding.Default.GetString(data, pos + 4, 20).TrimEnd('\0'),
                    FileSize = BitConverter.ToInt32(data, pos + 24),
                    Flags = BitConverter.ToInt32(data, pos + 28),
                    CompressedSize = 0,
                    SourcePak = null
                };
                if (!IsValidFileName(rec.FileName)) return null;
                records.Add(rec);
            }
            return records;
        }
    }

    // ================================================================
    //  OldL1Handler — 無 magic, 28-byte entries, L1 加密/無加密
    // ================================================================
    internal sealed class OldL1Handler : IdxHandler
    {
        private bool _isL1Protected;

        public OldL1Handler() { }

        public OldL1Handler(bool isProtected)
        {
            _isL1Protected = isProtected;
        }

        public override bool CanHandle(byte[] data) => data.Length >= 4;

        public override IdxHandler CreateInstance() => new OldL1Handler();

        public override IdxParseResult TryParse(byte[] idxData)
        {
            // 空檔 (Create 產生的 4 bytes count=0)
            if (idxData.Length < 32)
            {
                if (idxData.Length >= 4 && BitConverter.ToInt32(idxData, 0) == 0)
                {
                    _isL1Protected = false;
                    return new IdxParseResult
                    {
                        Records = new List<IndexRecord>(),
                        IsProtected = false,
                        EncryptionType = "None"
                    };
                }
                return null;
            }

            try
            {
                IndexRecord firstRecord = PakTools.DecodeIndexFirstRecord(idxData);
                bool isProtected = !Regex.IsMatch(
                    Encoding.Default.GetString(idxData, 8, 20),
                    "^([a-zA-Z0-9_\\-\\.']+)",
                    RegexOptions.IgnoreCase);

                if (isProtected)
                {
                    if (!Regex.IsMatch(firstRecord.FileName,
                        "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
                        return null;
                }

                byte[] indexData = isProtected ? PakTools.Decode(idxData, 4) : idxData;
                int startOffset = isProtected ? 0 : 4;
                int recordCount = (indexData.Length - startOffset) / 28;
                var records = new List<IndexRecord>(recordCount);

                for (int i = 0; i < recordCount; i++)
                {
                    records.Add(new IndexRecord(indexData, startOffset + i * 28));
                }

                _isL1Protected = isProtected;
                return new IdxParseResult
                {
                    Records = records,
                    IsProtected = isProtected,
                    EncryptionType = isProtected ? "L1" : "None"
                };
            }
            catch { return null; }
        }

        public override byte[] ExtractEntry(FileStream pakStream, IndexRecord rec)
        {
            byte[] data = new byte[rec.FileSize];
            pakStream.Seek(rec.Offset, SeekOrigin.Begin);
            pakStream.ReadExactly(data, 0, rec.FileSize);
            if (_isL1Protected)
                data = PakTools.Decode(data, 0);
            return data;
        }

        public override bool CanWrite => true;

        public override byte[] EncodeEntry(byte[] rawData)
        {
            return _isL1Protected ? PakTools.Encode(rawData, 0) : rawData;
        }

        public override byte[] BuildIndex(List<IndexRecord> records)
        {
            byte[] indexData = new byte[records.Count * 28];
            for (int i = 0; i < records.Count; i++)
            {
                records[i].ToOldBytes().CopyTo(indexData, i * 28);
            }

            byte[] finalData;
            if (_isL1Protected)
            {
                byte[] encoded = PakTools.Encode(indexData, 0);
                finalData = new byte[4 + encoded.Length];
                BitConverter.GetBytes(records.Count).CopyTo(finalData, 0);
                Array.Copy(encoded, 0, finalData, 4, encoded.Length);
            }
            else
            {
                finalData = new byte[4 + indexData.Length];
                BitConverter.GetBytes(records.Count).CopyTo(finalData, 0);
                Array.Copy(indexData, 0, finalData, 4, indexData.Length);
            }

            return finalData;
        }
    }

    // ================================================================
    //  OldDesHandler — 無 magic, 28-byte entries, DES 加密, read-only
    // ================================================================
    internal sealed class OldDesHandler : IdxHandler
    {
        public override bool CanHandle(byte[] data) => data.Length >= 32;

        public override IdxHandler CreateInstance() => new OldDesHandler();

        public override IdxParseResult TryParse(byte[] idxData)
        {
            if (idxData.Length < 32) return null;

            int recordCount = BitConverter.ToInt32(idxData, 0);
            int expectedSize = 4 + recordCount * 28;

            if (idxData.Length != expectedSize || recordCount <= 0 || recordCount > 100000)
                return null;

            byte[] entriesData = new byte[idxData.Length - 4];
            Array.Copy(idxData, 4, entriesData, 0, entriesData.Length);

            try
            {
                DesTransformInPlace(entriesData, false);

                var records = new List<IndexRecord>(recordCount);
                for (int i = 0; i < recordCount; i++)
                {
                    var rec = new IndexRecord(entriesData, i * 28);
                    if (!Regex.IsMatch(rec.FileName,
                        "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
                        return null;
                    records.Add(rec);
                }

                return new IdxParseResult
                {
                    Records = records,
                    IsProtected = true,
                    EncryptionType = "DES"
                };
            }
            catch { return null; }
        }

        public override byte[] ExtractEntry(FileStream pakStream, IndexRecord rec)
        {
            // Old DES: PAK data is NOT encrypted, only IDX was
            byte[] data = new byte[rec.FileSize];
            pakStream.Seek(rec.Offset, SeekOrigin.Begin);
            pakStream.ReadExactly(data, 0, rec.FileSize);
            return data;
        }
    }
}
