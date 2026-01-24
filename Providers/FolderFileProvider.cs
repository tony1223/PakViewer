using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PakViewer.Providers
{
    /// <summary>
    /// 資料夾檔案提供者 - 直接讀取資料夾內的圖片檔案
    /// </summary>
    public class FolderFileProvider : IFileProvider
    {
        /// <summary>
        /// 支援的圖片副檔名
        /// </summary>
        public static readonly string[] SupportedExtensions =
            { ".tbt", ".spr", ".img", ".til", ".png", ".gif", ".jpg", ".jpeg", ".bmp" };

        private readonly string _folderPath;
        private readonly List<FileEntry> _files;
        private bool _disposed;

        public FolderFileProvider(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

            _folderPath = folderPath;
            _files = Directory.GetFiles(folderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select((f, i) =>
                {
                    var fileInfo = new FileInfo(f);
                    return new FileEntry
                    {
                        Index = i,
                        FileName = Path.GetFileName(f),
                        FileSize = fileInfo.Length,
                        FilePath = f,
                        SourceName = Path.GetFileName(folderPath),
                        Source = this
                    };
                })
                .ToList();
        }

        public string Name => Path.GetFileName(_folderPath);

        public int Count => _files.Count;

        public IReadOnlyList<FileEntry> Files => _files.AsReadOnly();

        public byte[] Extract(int index)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FolderFileProvider));
            if (index < 0 || index >= _files.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return File.ReadAllBytes(_files[index].FilePath);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FolderFileProvider));
            if (string.IsNullOrEmpty(entry?.FilePath))
                throw new ArgumentException("FileEntry has no FilePath", nameof(entry));

            return File.ReadAllBytes(entry.FilePath);
        }

        public IEnumerable<string> GetExtensions()
        {
            return _files
                .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext);
        }

        // Source options - 資料夾模式只有一個選項
        public IEnumerable<string> GetSourceOptions() => new[] { Name };
        public void SetSourceOption(string option) { /* 不需要處理 */ }
        public string CurrentSourceOption => Name;
        public bool HasMultipleSourceOptions => false;

        public void Dispose()
        {
            // 資料夾模式不需要釋放資源
            _disposed = true;
        }
    }
}
