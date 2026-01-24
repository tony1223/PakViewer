using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;

namespace PakViewer.Providers
{
    /// <summary>
    /// 單一 PAK/IDX 檔案提供者
    /// </summary>
    public class PakProvider : IFileProvider
    {
        private readonly PakFile _pak;
        private readonly List<FileEntry> _files;
        private bool _disposed;

        public PakProvider(string idxPath)
        {
            _pak = new PakFile(idxPath);
            _files = _pak.Files.Select((f, i) => new FileEntry
            {
                Index = i,
                FileName = f.FileName,
                FileSize = f.FileSize,
                Offset = f.Offset,
                SourceName = Path.GetFileName(idxPath),
                Source = this
            }).ToList();
        }

        /// <summary>
        /// 取得內部 PakFile 實例 (用於進階操作)
        /// </summary>
        public PakFile PakFile => _pak;

        public string Name => Path.GetFileName(_pak.IdxPath);

        public int Count => _files.Count;

        public IReadOnlyList<FileEntry> Files => _files.AsReadOnly();

        public byte[] Extract(int index)
        {
            return _pak.Extract(index);
        }

        public byte[] Extract(FileEntry entry)
        {
            return _pak.Extract(entry.Index);
        }

        public IEnumerable<string> GetExtensions()
        {
            return _files
                .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext);
        }

        // Source options - 單一 PAK 模式只有一個選項
        public IEnumerable<string> GetSourceOptions() => new[] { Name };
        public void SetSourceOption(string option) { /* 不需要處理 */ }
        public string CurrentSourceOption => Name;
        public bool HasMultipleSourceOptions => false;

        public void Dispose()
        {
            if (!_disposed)
            {
                _pak?.Dispose();
                _disposed = true;
            }
        }
    }
}
