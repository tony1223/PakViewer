using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lin.Helper.Core.Pak
{
    /// <summary>
    /// Lineage 1 PAK/IDX 檔案讀寫器
    /// </summary>
    public class PakFile : IDisposable
    {
        private static readonly IdxHandler[] _prototypes = IdxHandler.CreatePrototypes();

        private readonly string _idxPath;
        private readonly string _pakPath;
        private List<IndexRecord> _records;
        private bool _isProtected;
        private string _encryptionType;
        private bool _disposed;
        private bool _modified;
        private IdxHandler _handler;

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
        /// 加密類型 (L1/DES/ExtB/Ext/Ext+DES/Idx/Idx+DES/None)
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

        private PakFile(string idxPath, string pakPath, IdxHandler handler,
                        bool isProtected, string encryptionType)
        {
            _idxPath = idxPath;
            _pakPath = pakPath;
            _handler = handler;
            _isProtected = isProtected;
            _encryptionType = encryptionType;
            _records = new List<IndexRecord>();
        }

        /// <summary>
        /// 建立新的 PAK 檔案
        /// </summary>
        public static PakFile Create(string idxPath, bool encrypted = true)
        {
            string pakPath = Path.ChangeExtension(idxPath, ".pak");

            var handler = new OldL1Handler(encrypted);
            byte[] emptyIdx = handler.BuildIndex(new List<IndexRecord>());
            File.WriteAllBytes(idxPath, emptyIdx);
            File.WriteAllBytes(pakPath, Array.Empty<byte>());

            return new PakFile(idxPath, pakPath, handler,
                               encrypted, encrypted ? "L1" : "None");
        }

        private void LoadIndex()
        {
            byte[] idxData = File.ReadAllBytes(_idxPath);

            foreach (var proto in _prototypes)
            {
                if (!proto.CanHandle(idxData)) continue;

                var handler = proto.CreateInstance();
                var result = handler.TryParse(idxData);
                if (result != null)
                {
                    _handler = handler;
                    _records = result.Records;
                    _isProtected = result.IsProtected;
                    _encryptionType = result.EncryptionType;
                    return;
                }
            }

            throw new InvalidDataException("Cannot parse IDX file");
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
                return _handler.ExtractEntry(fs, rec);
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

            int maxBytes = _handler.MaxFileNameBytes;
            if (Encoding.Default.GetByteCount(fileName) > maxBytes)
                throw new ArgumentException($"File name too long (max {maxBytes} bytes)", nameof(fileName));

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

            if (!_handler.CanWrite)
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
                long currentOffset = 0;

                using (var srcStream = new FileStream(_pakPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dstStream = new FileStream(tempPak, FileMode.Create, FileAccess.Write))
                {
                    foreach (var (fileName, srcIndex, newData) in entries)
                    {
                        byte[] pakData;

                        if (newData != null)
                        {
                            // 新增或替換的資料：用 handler 編碼
                            pakData = _handler.EncodeEntry(newData);
                            newRecords.Add(new IndexRecord(fileName, pakData.Length, currentOffset));
                        }
                        else
                        {
                            // 從原始 PAK 複製 (保留原始 metadata)
                            var rec = _records[srcIndex];
                            int rawSize = _handler.GetRawSize(rec);
                            pakData = new byte[rawSize];
                            srcStream.Seek(rec.Offset, SeekOrigin.Begin);
                            srcStream.ReadExactly(pakData, 0, rawSize);

                            var newRec = rec;
                            newRec.Offset = currentOffset;
                            newRecords.Add(newRec);
                        }

                        dstStream.Write(pakData, 0, pakData.Length);
                        currentOffset += pakData.Length;
                    }
                }

                // 重建 IDX
                File.WriteAllBytes(tempIdx, _handler.BuildIndex(newRecords));

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
