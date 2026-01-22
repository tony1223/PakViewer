using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Lin.Helper.Core.Pak
{
    /// <summary>
    /// Lineage 1 PAK/IDX 檔案讀寫器
    /// </summary>
    public class PakFile : IDisposable
    {
        private readonly string _idxPath;
        private readonly string _pakPath;
        private List<IndexRecord> _records;
        private bool _isProtected;
        private string _encryptionType;
        private bool _disposed;
        private bool _modified;

        // 待處理的變更
        private readonly List<PendingChange> _pendingChanges = new List<PendingChange>();

        /// <summary>
        /// IDX 檔案路徑
        /// </summary>
        public string IdxPath => _idxPath;

        /// <summary>
        /// PAK 檔案路徑
        /// </summary>
        public string PakPath => _pakPath;

        /// <summary>
        /// 是否已加密
        /// </summary>
        public bool IsProtected => _isProtected;

        /// <summary>
        /// 加密類型 (L1/DES/ExtB/None)
        /// </summary>
        public string EncryptionType => _encryptionType;

        /// <summary>
        /// 檔案列表
        /// </summary>
        public IReadOnlyList<IndexRecord> Files => _records.AsReadOnly();

        /// <summary>
        /// 檔案數量
        /// </summary>
        public int Count => _records.Count;

        /// <summary>
        /// 開啟 PAK 檔案
        /// </summary>
        /// <param name="idxPath">IDX 檔案路徑</param>
        public PakFile(string idxPath)
        {
            if (!File.Exists(idxPath))
                throw new FileNotFoundException("IDX file not found", idxPath);

            _idxPath = idxPath;
            _pakPath = Path.ChangeExtension(idxPath, ".pak");

            if (!File.Exists(_pakPath))
                throw new FileNotFoundException("PAK file not found", _pakPath);

            LoadIndex();
        }

        /// <summary>
        /// 建立新的 PAK 檔案
        /// </summary>
        public static PakFile Create(string idxPath, bool encrypted = true)
        {
            string pakPath = Path.ChangeExtension(idxPath, ".pak");

            // 建立空的 IDX 和 PAK 檔案
            byte[] emptyIdx = encrypted
                ? new byte[] { 0, 0, 0, 0 } // 4 bytes header (count = 0)
                : new byte[] { 0, 0, 0, 0 };

            File.WriteAllBytes(idxPath, emptyIdx);
            File.WriteAllBytes(pakPath, new byte[0]);

            return new PakFile(idxPath);
        }

        private void LoadIndex()
        {
            byte[] idxData = File.ReadAllBytes(_idxPath);

            // 嘗試自動偵測格式
            var result = LoadIndexAuto(idxData);
            if (result == null)
                throw new InvalidDataException("Cannot parse IDX file");

            (_records, _isProtected, _encryptionType) = result.Value;
        }

        private (List<IndexRecord> records, bool isProtected, string encryptionType)? LoadIndexAuto(byte[] idxData)
        {
            // 1. 嘗試 ExtB 格式
            if (IsExtBFormat(idxData))
            {
                var extbResult = LoadIndexExtB(idxData);
                if (extbResult != null)
                    return (extbResult.Value.records, true, "ExtB");
            }

            // 2. 嘗試標準 L1 加密
            var l1Result = LoadIndexL1(idxData);
            if (l1Result != null)
                return (l1Result.Value.records, l1Result.Value.isProtected, l1Result.Value.isProtected ? "L1" : "None");

            // 3. 嘗試 DES 加密
            var desResult = LoadIndexDES(idxData);
            if (desResult != null)
                return (desResult.Value.records, true, "DES");

            return null;
        }

        private (List<IndexRecord> records, bool isProtected)? LoadIndexL1(byte[] idxData)
        {
            if (idxData.Length < 32)
                return null;

            // 檢查是否加密
            IndexRecord firstRecord = PakTools.DecodeIndexFirstRecord(idxData);
            bool isProtected = !Regex.IsMatch(
                Encoding.Default.GetString(idxData, 8, 20),
                "^([a-zA-Z0-9_\\-\\.']+)",
                RegexOptions.IgnoreCase);

            if (isProtected)
            {
                if (!Regex.IsMatch(firstRecord.FileName, "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
                    return null;
            }

            byte[] indexData = isProtected ? PakTools.Decode(idxData, 4) : idxData;

            int recordSize = 28;
            int startOffset = isProtected ? 0 : 4;
            int recordCount = (indexData.Length - startOffset) / recordSize;
            var records = new List<IndexRecord>(recordCount);

            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new IndexRecord(indexData, startOffset + i * recordSize));
            }

            return (records, isProtected);
        }

        private (List<IndexRecord> records, bool isProtected)? LoadIndexDES(byte[] idxData)
        {
            if (idxData.Length < 32)
                return null;

            int recordCount = BitConverter.ToInt32(idxData, 0);
            int expectedSize = 4 + recordCount * 28;

            if (idxData.Length != expectedSize || recordCount <= 0 || recordCount > 100000)
                return null;

            byte[] key = new byte[] { 0x7e, 0x21, 0x40, 0x23, 0x25, 0x5e, 0x24, 0x3c }; // ~!@#%^$<
            byte[] entriesData = new byte[idxData.Length - 4];
            Array.Copy(idxData, 4, entriesData, 0, entriesData.Length);

            try
            {
                using (var des = DES.Create())
                {
                    des.Key = key;
                    des.Mode = CipherMode.ECB;
                    des.Padding = PaddingMode.None;

                    using (var decryptor = des.CreateDecryptor())
                    {
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

                var records = new List<IndexRecord>(recordCount);
                for (int i = 0; i < recordCount; i++)
                {
                    var rec = new IndexRecord(entriesData, i * 28);
                    // 驗證檔名是否合理
                    if (!Regex.IsMatch(rec.FileName, "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
                        return null;
                    records.Add(rec);
                }

                return (records, true);
            }
            catch
            {
                return null;
            }
        }

        private bool IsExtBFormat(byte[] data)
        {
            if (data.Length < 6) return false;
            return data[0] == '_' && data[1] == 'E' && data[2] == 'X' &&
                   data[3] == 'T' && data[4] == 'B' && data[5] == '$';
        }

        private (List<IndexRecord> records, bool isProtected)? LoadIndexExtB(byte[] idxData)
        {
            if (!IsExtBFormat(idxData))
                return null;

            try
            {
                int pos = 6;
                int recordCount = BitConverter.ToInt32(idxData, pos);
                pos += 4;

                var records = new List<IndexRecord>(recordCount);

                for (int i = 0; i < recordCount; i++)
                {
                    // 使用 unsigned 讀取以支援超過 2GB 的 PAK
                    long offset = BitConverter.ToUInt32(idxData, pos);
                    pos += 4;
                    int size = BitConverter.ToInt32(idxData, pos);
                    pos += 4;
                    int compressedSize = BitConverter.ToInt32(idxData, pos);
                    pos += 4;

                    int nameLen = 0;
                    while (pos + nameLen < idxData.Length && idxData[pos + nameLen] != 0)
                        nameLen++;

                    string fileName = Encoding.Default.GetString(idxData, pos, nameLen);
                    pos += nameLen + 1;

                    var rec = new IndexRecord(fileName, size, offset)
                    {
                        CompressedSize = compressedSize
                    };
                    records.Add(rec);
                }

                return (records, true);
            }
            catch
            {
                return null;
            }
        }

        #region 讀取操作

        /// <summary>
        /// 提取檔案
        /// </summary>
        public byte[] Extract(string fileName)
        {
            int index = FindFileIndex(fileName);
            if (index < 0)
                throw new FileNotFoundException($"File not found in PAK: {fileName}");
            return Extract(index);
        }

        /// <summary>
        /// 提取檔案 (依索引)
        /// </summary>
        public byte[] Extract(int index)
        {
            if (index < 0 || index >= _records.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var rec = _records[index];

            using (var fs = new FileStream(_pakPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] data;

                if (_encryptionType == "ExtB" && rec.CompressedSize > 0)
                {
                    // ExtB 格式需要解壓縮
                    data = new byte[rec.CompressedSize];
                    fs.Seek(rec.Offset, SeekOrigin.Begin);
                    fs.Read(data, 0, rec.CompressedSize);
                    data = DecompressExtB(data, rec.FileSize);
                }
                else
                {
                    data = new byte[rec.FileSize];
                    fs.Seek(rec.Offset, SeekOrigin.Begin);
                    fs.Read(data, 0, rec.FileSize);

                    if (_isProtected && _encryptionType == "L1")
                    {
                        data = PakTools.Decode(data, 0);
                    }
                }

                return data;
            }
        }

        private byte[] DecompressExtB(byte[] compressedData, int originalSize)
        {
            // 嘗試 Brotli 解壓縮
            try
            {
                using (var input = new MemoryStream(compressedData))
                using (var output = new MemoryStream())
                using (var brotli = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress))
                {
                    brotli.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch
            {
                // 嘗試 Zlib/Deflate 解壓縮
                try
                {
                    using (var input = new MemoryStream(compressedData, 2, compressedData.Length - 2)) // skip zlib header
                    using (var output = new MemoryStream())
                    using (var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
                    {
                        deflate.CopyTo(output);
                        return output.ToArray();
                    }
                }
                catch
                {
                    // 返回原始資料
                    return compressedData;
                }
            }
        }

        /// <summary>
        /// 提取所有檔案到資料夾
        /// </summary>
        public void ExtractAll(string outputFolder)
        {
            ExtractAll(outputFolder, null);
        }

        /// <summary>
        /// 提取所有檔案到資料夾 (含進度回報)
        /// </summary>
        public void ExtractAll(string outputFolder, Action<int, int, string> progress)
        {
            Directory.CreateDirectory(outputFolder);

            for (int i = 0; i < _records.Count; i++)
            {
                var rec = _records[i];
                progress?.Invoke(i + 1, _records.Count, rec.FileName);

                byte[] data = Extract(i);
                string outputPath = Path.Combine(outputFolder, rec.FileName);
                File.WriteAllBytes(outputPath, data);
            }
        }

        /// <summary>
        /// 尋找檔案索引
        /// </summary>
        public int FindFileIndex(string fileName)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 檔案是否存在
        /// </summary>
        public bool Contains(string fileName)
        {
            return FindFileIndex(fileName) >= 0;
        }

        #endregion

        #region 排序與驗證

        /// <summary>
        /// 排序類型
        /// </summary>
        public enum SortType
        {
            /// <summary>
            /// ASCII 不區分大小寫排序
            /// </summary>
            Ascii,
            /// <summary>
            /// 底線優先排序 (數字 → 底線 → 字母)
            /// </summary>
            UnderscoreFirst
        }

        /// <summary>
        /// 驗證檔案排序是否正確
        /// </summary>
        /// <returns>排序錯誤列表 (索引, 實際檔名, 期望檔名)</returns>
        public List<(int index, string fileName, string expectedFileName)> VerifySortOrder(SortType sortType = SortType.Ascii)
        {
            var result = new List<(int index, string fileName, string expectedFileName)>();
            var comparer = GetComparer(sortType);

            var sortedNames = _records.Select(r => r.FileName).OrderBy(n => n, comparer).ToList();

            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].FileName != sortedNames[i])
                {
                    result.Add((i, _records[i].FileName, sortedNames[i]));
                }
            }

            return result;
        }

        /// <summary>
        /// 檢查排序是否正確
        /// </summary>
        public bool IsSorted(SortType sortType = SortType.Ascii)
        {
            return VerifySortOrder(sortType).Count == 0;
        }

        /// <summary>
        /// 找到新增檔案時應插入的索引位置 (維持排序)
        /// </summary>
        public int FindInsertIndex(string fileName, SortType sortType = SortType.Ascii)
        {
            var comparer = GetComparer(sortType);
            int left = 0;
            int right = _records.Count;

            while (left < right)
            {
                int mid = (left + right) / 2;
                if (comparer.Compare(_records[mid].FileName, fileName) < 0)
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

        private static IComparer<string> GetComparer(SortType sortType)
        {
            return sortType switch
            {
                SortType.UnderscoreFirst => new UnderscoreFirstComparer(),
                _ => new AsciiStringComparer()
            };
        }

        #endregion

        #region 寫入操作

        /// <summary>
        /// 新增檔案
        /// </summary>
        /// <param name="fileName">檔案名稱</param>
        /// <param name="data">檔案資料</param>
        /// <param name="maintainSort">是否維持排序順序</param>
        /// <param name="sortType">排序類型</param>
        public void Add(string fileName, byte[] data, bool maintainSort = false, SortType sortType = SortType.Ascii)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            if (Encoding.Default.GetByteCount(fileName) > 19)
                throw new ArgumentException("File name too long (max 19 bytes)", nameof(fileName));

            if (Contains(fileName))
                throw new InvalidOperationException($"File already exists: {fileName}");

            int insertIndex = maintainSort ? FindInsertIndex(fileName, sortType) : -1;

            _pendingChanges.Add(new PendingChange
            {
                Type = ChangeType.Add,
                FileName = fileName,
                Data = data,
                InsertIndex = insertIndex
            });
            _modified = true;
        }

        /// <summary>
        /// 新增檔案 (簡易版)
        /// </summary>
        public void Add(string fileName, byte[] data)
        {
            Add(fileName, data, false, SortType.Ascii);
        }

        /// <summary>
        /// 新增檔案 (從檔案路徑)
        /// </summary>
        public void Add(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] data = File.ReadAllBytes(filePath);
            Add(fileName, data);
        }

        /// <summary>
        /// 刪除檔案
        /// </summary>
        public void Delete(string fileName)
        {
            int index = FindFileIndex(fileName);
            if (index < 0)
                throw new FileNotFoundException($"File not found: {fileName}");
            Delete(index);
        }

        /// <summary>
        /// 刪除檔案 (依索引)
        /// </summary>
        public void Delete(int index)
        {
            if (index < 0 || index >= _records.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _pendingChanges.Add(new PendingChange
            {
                Type = ChangeType.Delete,
                Index = index,
                FileName = _records[index].FileName
            });
            _modified = true;
        }

        /// <summary>
        /// 替換檔案內容
        /// </summary>
        public void Replace(string fileName, byte[] newData)
        {
            int index = FindFileIndex(fileName);
            if (index < 0)
                throw new FileNotFoundException($"File not found: {fileName}");

            _pendingChanges.Add(new PendingChange
            {
                Type = ChangeType.Replace,
                Index = index,
                FileName = fileName,
                Data = newData
            });
            _modified = true;
        }

        /// <summary>
        /// 儲存變更
        /// </summary>
        public void Save()
        {
            if (!_modified || _pendingChanges.Count == 0)
                return;

            if (_encryptionType == "ExtB" || _encryptionType == "DES")
                throw new NotSupportedException($"Writing to {_encryptionType} format is not supported");

            // 建立備份
            string pakBackup = _pakPath + ".bak";
            string idxBackup = _idxPath + ".bak";

            if (File.Exists(pakBackup)) File.Delete(pakBackup);
            if (File.Exists(idxBackup)) File.Delete(idxBackup);

            File.Copy(_pakPath, pakBackup);
            File.Copy(_idxPath, idxBackup);

            try
            {
                // 收集要刪除的索引
                var deleteIndices = new HashSet<int>();
                foreach (var change in _pendingChanges.Where(c => c.Type == ChangeType.Delete))
                {
                    deleteIndices.Add(change.Index);
                }

                // 收集要替換的資料
                var replaceData = new Dictionary<int, byte[]>();
                foreach (var change in _pendingChanges.Where(c => c.Type == ChangeType.Replace))
                {
                    replaceData[change.Index] = change.Data;
                }

                // 收集要新增的檔案 (分成有指定位置和沒指定位置)
                var addFilesWithIndex = _pendingChanges
                    .Where(c => c.Type == ChangeType.Add && c.InsertIndex >= 0)
                    .OrderBy(c => c.InsertIndex)
                    .ToList();
                var addFilesNoIndex = _pendingChanges
                    .Where(c => c.Type == ChangeType.Add && c.InsertIndex < 0)
                    .ToList();

                // 建立中間資料結構 (檔名, 原始資料來源)
                var entries = new List<(string fileName, int srcIndex, byte[] newData)>();

                // 保留的現有檔案
                for (int i = 0; i < _records.Count; i++)
                {
                    if (!deleteIndices.Contains(i))
                    {
                        if (replaceData.TryGetValue(i, out byte[] newData))
                            entries.Add((_records[i].FileName, -1, newData));
                        else
                            entries.Add((_records[i].FileName, i, null));
                    }
                }

                // 插入有指定位置的新增檔案 (從後往前插入以維持索引正確)
                foreach (var add in addFilesWithIndex.OrderByDescending(a => a.InsertIndex))
                {
                    int idx = Math.Min(add.InsertIndex, entries.Count);
                    entries.Insert(idx, (add.FileName, -1, add.Data));
                }

                // 附加沒指定位置的新增檔案
                foreach (var add in addFilesNoIndex)
                {
                    entries.Add((add.FileName, -1, add.Data));
                }

                // 重建 PAK 和 IDX
                string tempPak = _pakPath + ".tmp";
                string tempIdx = _idxPath + ".tmp";

                var newRecords = new List<IndexRecord>();
                long currentOffset = 0;  // 使用 long 支援大型 PAK

                using (var srcStream = new FileStream(_pakPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dstStream = new FileStream(tempPak, FileMode.Create, FileAccess.Write))
                {
                    foreach (var (fileName, srcIndex, newData) in entries)
                    {
                        byte[] fileData;

                        if (newData != null)
                        {
                            // 新增或替換的資料
                            fileData = _isProtected ? PakTools.Encode(newData, 0) : newData;
                        }
                        else
                        {
                            // 從原始 PAK 讀取
                            var rec = _records[srcIndex];
                            fileData = new byte[rec.FileSize];
                            srcStream.Seek(rec.Offset, SeekOrigin.Begin);
                            srcStream.Read(fileData, 0, rec.FileSize);
                        }

                        dstStream.Write(fileData, 0, fileData.Length);
                        newRecords.Add(new IndexRecord(fileName, fileData.Length, currentOffset));
                        currentOffset += fileData.Length;
                    }
                }

                // 重建 IDX
                RebuildIndex(tempIdx, newRecords, _isProtected);

                // 替換檔案
                File.Delete(_pakPath);
                File.Move(tempPak, _pakPath);
                File.Delete(_idxPath);
                File.Move(tempIdx, _idxPath);

                // 刪除備份
                File.Delete(pakBackup);
                File.Delete(idxBackup);

                // 更新記錄
                _records = newRecords;
                _pendingChanges.Clear();
                _modified = false;
            }
            catch
            {
                // 還原備份
                if (File.Exists(pakBackup))
                {
                    File.Copy(pakBackup, _pakPath, true);
                    File.Delete(pakBackup);
                }
                if (File.Exists(idxBackup))
                {
                    File.Copy(idxBackup, _idxPath, true);
                    File.Delete(idxBackup);
                }
                throw;
            }
        }

        private void RebuildIndex(string idxFile, List<IndexRecord> records, bool isProtected)
        {
            int recordSize = 28;
            byte[] indexData = new byte[records.Count * recordSize];

            for (int i = 0; i < records.Count; i++)
            {
                int offset = i * recordSize;

                // 將 long offset 寫為 uint (限制最大 2GB 以確保相容性)
                if (records[i].Offset > int.MaxValue)
                    throw new InvalidOperationException($"File offset exceeds 2GB limit: {records[i].FileName}");
                byte[] offsetBytes = BitConverter.GetBytes((uint)records[i].Offset);
                Array.Copy(offsetBytes, 0, indexData, offset, 4);

                byte[] nameBytes = Encoding.Default.GetBytes(records[i].FileName);
                Array.Copy(nameBytes, 0, indexData, offset + 4, Math.Min(nameBytes.Length, 20));

                byte[] sizeBytes = BitConverter.GetBytes(records[i].FileSize);
                Array.Copy(sizeBytes, 0, indexData, offset + 24, 4);
            }

            byte[] finalData;
            if (isProtected)
            {
                byte[] encoded = PakTools.Encode(indexData, 0);
                finalData = new byte[4 + encoded.Length];
                byte[] countBytes = BitConverter.GetBytes(records.Count);
                Array.Copy(countBytes, 0, finalData, 0, 4);
                Array.Copy(encoded, 0, finalData, 4, encoded.Length);
            }
            else
            {
                finalData = new byte[4 + indexData.Length];
                byte[] countBytes = BitConverter.GetBytes(records.Count);
                Array.Copy(countBytes, 0, finalData, 0, 4);
                Array.Copy(indexData, 0, finalData, 4, indexData.Length);
            }

            File.WriteAllBytes(idxFile, finalData);
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        private enum ChangeType
        {
            Add,
            Delete,
            Replace
        }

        private class PendingChange
        {
            public ChangeType Type;
            public int Index;
            public string FileName;
            public byte[] Data;
            public int InsertIndex = -1; // -1 = 不指定位置 (加到最後)
        }
    }

    #region 排序比較器

    /// <summary>
    /// 不區分大小寫的 ASCII 排序比較器
    /// </summary>
    public class AsciiStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 底線優先排序比較器 (數字 → 底線 → 字母)
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

                int ox = GetOrder(cx);
                int oy = GetOrder(cy);

                if (ox != oy) return ox.CompareTo(oy);
                return cx.CompareTo(cy);
            }
            return x.Length.CompareTo(y.Length);
        }

        private static int GetOrder(char c)
        {
            // 其他符號 → -1, 數字 → 0, 底線 → 1, 字母 → 2
            if (c >= '0' && c <= '9') return 0;
            if (c == '_') return 1;
            if (c >= 'a' && c <= 'z') return 2;
            return c < '0' ? -1 : 3;
        }
    }

    #endregion
}
