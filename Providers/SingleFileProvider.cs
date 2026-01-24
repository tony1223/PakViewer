using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PakViewer.Providers
{
    /// <summary>
    /// 單一檔案提供者 - 直接開啟單一圖片檔案
    /// </summary>
    public class SingleFileProvider : IFileProvider
    {
        private readonly string _filePath;
        private readonly List<FileEntry> _files;
        private bool _disposed;

        public SingleFileProvider(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            _filePath = filePath;
            var fileInfo = new FileInfo(filePath);

            _files = new List<FileEntry>
            {
                new FileEntry
                {
                    Index = 0,
                    FileName = Path.GetFileName(filePath),
                    FileSize = fileInfo.Length,
                    FilePath = filePath,
                    SourceName = Path.GetFileName(filePath),
                    Source = this
                }
            };
        }

        public string Name => Path.GetFileName(_filePath);

        public int Count => 1;

        public IReadOnlyList<FileEntry> Files => _files.AsReadOnly();

        public byte[] Extract(int index)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SingleFileProvider));
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return File.ReadAllBytes(_filePath);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SingleFileProvider));
            return File.ReadAllBytes(_filePath);
        }

        public IEnumerable<string> GetExtensions()
        {
            var ext = Path.GetExtension(_filePath)?.ToLowerInvariant() ?? "";
            return string.IsNullOrEmpty(ext)
                ? Enumerable.Empty<string>()
                : new[] { ext };
        }

        // Source options - 單一檔案模式只有一個選項
        public IEnumerable<string> GetSourceOptions() => new[] { Name };
        public void SetSourceOption(string option) { /* 不需要處理 */ }
        public string CurrentSourceOption => Name;
        public bool HasMultipleSourceOptions => false;

        public void Dispose()
        {
            // 單一檔案模式不需要釋放資源
            _disposed = true;
        }
    }
}
