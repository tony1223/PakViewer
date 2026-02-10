using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Lcx;
using PakViewer.Localization;

namespace PakViewer.Providers
{
    /// <summary>
    /// LCX 檔案提供者 - 支援單一或多個 LCX 檔案
    /// </summary>
    public class LcxProvider : IFileProvider
    {
        public static string AllSourcesOption => I18n.T("Filter.All");

        private readonly Dictionary<string, LcxFile> _lcxFiles;  // LCX filename -> LcxFile
        private readonly List<FileEntry> _allFiles;
        private List<FileEntry> _filteredFiles;
        private string _currentSourceOption;
        private bool _disposed;

        /// <summary>
        /// 建立 LCX 提供者
        /// </summary>
        /// <param name="lcxPaths">LCX 檔案路徑 (一或多個)</param>
        /// <param name="keys">ChaCha20-Poly1305 金鑰列表</param>
        public LcxProvider(string[] lcxPaths, byte[][] keys)
        {
            if (lcxPaths == null || lcxPaths.Length == 0)
                throw new ArgumentException("At least one LCX path is required", nameof(lcxPaths));

            _lcxFiles = new Dictionary<string, LcxFile>(StringComparer.OrdinalIgnoreCase);
            _allFiles = new List<FileEntry>();

            int globalIndex = 0;
            foreach (var lcxPath in lcxPaths.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var lcxName = Path.GetFileName(lcxPath);
                try
                {
                    var lcx = new LcxFile(lcxPath, keys);
                    _lcxFiles[lcxName] = lcx;

                    foreach (var entry in lcx.Entries)
                    {
                        _allFiles.Add(new FileEntry
                        {
                            Index = globalIndex++,
                            FileName = entry.FileName,
                            FileSize = entry.CompressedSize,
                            SourceName = lcxName,
                            Source = this
                        });
                    }
                }
                catch
                {
                    // 忽略無法開啟的 LCX 檔案
                }
            }

            // 預設選第一個 LCX
            var defaultOption = _lcxFiles.Keys.FirstOrDefault() ?? AllSourcesOption;
            SetSourceOption(defaultOption);
        }

        public string Name
        {
            get
            {
                if (_lcxFiles.Count == 1)
                    return _lcxFiles.Keys.First();
                return $"LCX ({_lcxFiles.Count} files)";
            }
        }

        public int Count => _filteredFiles?.Count ?? _allFiles.Count;

        public IReadOnlyList<FileEntry> Files => (_filteredFiles ?? _allFiles).AsReadOnly();

        public byte[] Extract(int index)
        {
            var files = _filteredFiles ?? _allFiles;
            if (index < 0 || index >= files.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Extract(files[index]);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!_lcxFiles.TryGetValue(entry.SourceName, out var lcx))
                throw new InvalidOperationException($"LCX file not found: {entry.SourceName}");

            // 用 FileName 在 LcxFile 中找到對應 entry
            var lcxEntry = lcx.Entries.FirstOrDefault(e => e.FileName == entry.FileName);
            if (lcxEntry == null)
                throw new InvalidOperationException($"Entry not found in LCX: {entry.FileName}");

            return lcx.Extract(lcxEntry);
        }

        public IEnumerable<string> GetExtensions()
        {
            var files = _filteredFiles ?? _allFiles;
            return files
                .Select(f => Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "")
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext);
        }

        public IEnumerable<string> GetSourceOptions()
        {
            if (_lcxFiles.Count > 1)
                yield return AllSourcesOption;

            foreach (var name in _lcxFiles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                yield return name;
        }

        public void SetSourceOption(string option)
        {
            _currentSourceOption = option;

            if (option == AllSourcesOption || string.IsNullOrEmpty(option))
            {
                _filteredFiles = null;
            }
            else
            {
                _filteredFiles = _allFiles
                    .Where(f => f.SourceName.Equals(option, StringComparison.OrdinalIgnoreCase))
                    .Select((f, i) => new FileEntry
                    {
                        Index = i,
                        FileName = f.FileName,
                        FileSize = f.FileSize,
                        SourceName = f.SourceName,
                        Source = f.Source
                    })
                    .ToList();
            }
        }

        public string CurrentSourceOption => _currentSourceOption ?? AllSourcesOption;

        public bool HasMultipleSourceOptions => _lcxFiles.Count > 1;

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var lcx in _lcxFiles.Values)
                {
                    lcx?.Dispose();
                }
                _lcxFiles.Clear();
                _disposed = true;
            }
        }
    }
}
