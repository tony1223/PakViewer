using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;

namespace PakViewer.Providers
{
    /// <summary>
    /// 單一 PAK 檔案提供者 - 包裝單一 IDX/PAK 檔案
    /// </summary>
    public class SinglePakProvider : IFileProvider
    {
        private readonly PakFile _pakFile;
        private readonly List<FileEntry> _files;
        private readonly string _idxName;
        private bool _disposed;

        /// <summary>
        /// 建立單一 PAK 提供者
        /// </summary>
        /// <param name="idxPath">IDX 檔案路徑</param>
        public SinglePakProvider(string idxPath)
        {
            if (!File.Exists(idxPath))
                throw new FileNotFoundException($"File not found: {idxPath}");

            _pakFile = new PakFile(idxPath);
            _idxName = Path.GetFileName(idxPath);
            _files = new List<FileEntry>();

            // 將 PAK 的檔案加入列表
            for (int i = 0; i < _pakFile.Count; i++)
            {
                var record = _pakFile.Files[i];
                _files.Add(new FileEntry
                {
                    Index = i,
                    FileName = record.FileName,
                    FileSize = record.FileSize,
                    Offset = record.Offset,
                    SourceName = _idxName,
                    Source = this
                });
            }
        }

        /// <summary>
        /// 取得底層的 PakFile（用於刪除等直接操作）
        /// </summary>
        public PakFile PakFile => _pakFile;

        /// <summary>
        /// 取得 IDX 檔案名稱
        /// </summary>
        public string IdxName => _idxName;

        public string Name => _idxName;

        public int Count => _files.Count;

        public IReadOnlyList<FileEntry> Files => _files.AsReadOnly();

        public byte[] Extract(int index)
        {
            if (index < 0 || index >= _files.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _pakFile.Extract(index);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            // 找出在 PAK 內的實際索引
            var pakIndex = _files.FindIndex(f =>
                f.FileName == entry.FileName && f.Offset == entry.Offset);

            if (pakIndex < 0)
                throw new InvalidOperationException($"File not found in PAK: {entry.FileName}");

            return _pakFile.Extract(pakIndex);
        }

        public IEnumerable<string> GetExtensions()
        {
            return _files
                .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext);
        }

        // 單一 PAK 沒有多個來源選項
        public IEnumerable<string> GetSourceOptions()
        {
            yield return _idxName;
        }

        public void SetSourceOption(string option)
        {
            // 單一 PAK 不需要切換來源
        }

        public string CurrentSourceOption => _idxName;

        public bool HasMultipleSourceOptions => false;

        /// <summary>
        /// 重新整理檔案列表（刪除後使用）
        /// </summary>
        public void Refresh()
        {
            _files.Clear();
            for (int i = 0; i < _pakFile.Count; i++)
            {
                var record = _pakFile.Files[i];
                _files.Add(new FileEntry
                {
                    Index = i,
                    FileName = record.FileName,
                    FileSize = record.FileSize,
                    Offset = record.Offset,
                    SourceName = _idxName,
                    Source = this
                });
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _pakFile?.Dispose();
                _disposed = true;
            }
        }
    }
}
