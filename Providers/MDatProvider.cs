using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Dat;
using PakViewer.Localization;

namespace PakViewer.Providers
{
    /// <summary>
    /// ZIP-based .dat 容器提供者 - 支援單一或多個 .dat 檔案
    /// </summary>
    public class MDatProvider : IFileProvider
    {
        public static string AllSourcesOption => I18n.T("Filter.All");

        private readonly Dictionary<string, MDat> _datFiles;  // dat filename -> MDat
        private readonly List<FileEntry> _allFiles;
        private List<FileEntry> _filteredFiles;
        private string _currentSourceOption;
        private bool _disposed;

        /// <summary>
        /// 建立 MDat 提供者
        /// </summary>
        /// <param name="datPaths">.dat 檔案路徑 (一或多個)</param>
        /// <param name="password">ZIP 密碼 (null 表示無密碼)</param>
        public MDatProvider(string[] datPaths, string password = null)
        {
            if (datPaths == null || datPaths.Length == 0)
                throw new ArgumentException("At least one DAT path is required", nameof(datPaths));

            _datFiles = new Dictionary<string, MDat>(StringComparer.OrdinalIgnoreCase);
            _allFiles = new List<FileEntry>();

            int globalIndex = 0;
            foreach (var datPath in datPaths.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var datName = Path.GetFileName(datPath);
                try
                {
                    var dat = new MDat(datPath, password);

                    // 跳過 Sealed 狀態
                    if (dat.Status == MDatStatus.Sealed)
                    {
                        dat.Dispose();
                        continue;
                    }

                    _datFiles[datName] = dat;

                    foreach (var entry in dat.Entries)
                    {
                        _allFiles.Add(new FileEntry
                        {
                            Index = globalIndex++,
                            FileName = entry.FileName,
                            FileSize = entry.UncompressedSize,
                            SourceName = datName,
                            Source = this
                        });
                    }
                }
                catch
                {
                    // 忽略無法開啟的 .dat 檔案
                }
            }

            // 預設選第一個 dat
            var defaultOption = _datFiles.Keys.FirstOrDefault() ?? AllSourcesOption;
            SetSourceOption(defaultOption);
        }

        public string Name
        {
            get
            {
                if (_datFiles.Count == 1)
                    return _datFiles.Keys.First();
                return $"MDat ({_datFiles.Count} files)";
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

            if (!_datFiles.TryGetValue(entry.SourceName, out var dat))
                throw new InvalidOperationException($"DAT file not found: {entry.SourceName}");

            var datEntry = dat.Entries.FirstOrDefault(e => e.FileName == entry.FileName);
            if (datEntry == null)
                throw new InvalidOperationException($"Entry not found in DAT: {entry.FileName}");

            return dat.Extract(datEntry);
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
            if (_datFiles.Count > 1)
                yield return AllSourcesOption;

            foreach (var name in _datFiles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
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

        public bool HasMultipleSourceOptions => _datFiles.Count > 1;

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var dat in _datFiles.Values)
                {
                    dat?.Dispose();
                }
                _datFiles.Clear();
                _disposed = true;
            }
        }
    }
}
