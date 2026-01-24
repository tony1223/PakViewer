using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PakViewer.Utility;

namespace PakViewer.Providers
{
    /// <summary>
    /// DAT 檔案提供者 - Lineage M DAT 格式
    /// </summary>
    public class DatProvider : IFileProvider
    {
        private readonly DatTools.DatFile _dat;
        private readonly List<FileEntry> _files;
        private bool _disposed;

        public DatProvider(string datPath)
        {
            if (!File.Exists(datPath))
                throw new FileNotFoundException("DAT file not found", datPath);

            if (!DatTools.IsDatFile(datPath))
                throw new InvalidDataException("Not a valid DAT file");

            _dat = new DatTools.DatFile(datPath);
            _dat.ParseEntries();

            _files = _dat.Entries.Select((e, i) => new FileEntry
            {
                Index = i,
                FileName = Path.GetFileName(e.Path),
                FileSize = e.Size,
                Offset = e.Offset,
                FilePath = e.Path,  // DAT 內的完整路徑
                SourceName = Path.GetFileName(datPath),
                Source = this
            }).ToList();
        }

        /// <summary>
        /// 取得內部 DatFile 實例 (用於進階操作)
        /// </summary>
        public DatTools.DatFile DatFile => _dat;

        public string Name => _dat.FileName;

        public int Count => _files.Count;

        public IReadOnlyList<FileEntry> Files => _files.AsReadOnly();

        public byte[] Extract(int index)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DatProvider));
            if (index < 0 || index >= _files.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var entry = _dat.Entries[index];
            return _dat.ExtractFile(entry);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DatProvider));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var datEntry = _dat.Entries.FirstOrDefault(e =>
                e.Path == entry.FilePath && e.Offset == entry.Offset);

            if (datEntry == null)
                throw new InvalidOperationException($"File not found in DAT: {entry.FileName}");

            return _dat.ExtractFile(datEntry);
        }

        public IEnumerable<string> GetExtensions()
        {
            return _files
                .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext);
        }

        // Source options - DAT 模式只有一個選項
        public IEnumerable<string> GetSourceOptions() => new[] { Name };
        public void SetSourceOption(string option) { /* 不需要處理 */ }
        public string CurrentSourceOption => Name;
        public bool HasMultipleSourceOptions => false;

        public void Dispose()
        {
            // DatFile 沒有需要釋放的資源
            _disposed = true;
        }
    }
}
