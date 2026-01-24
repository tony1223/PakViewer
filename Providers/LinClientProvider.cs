using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lin.Helper.Core.Pak;
using PakViewer.Localization;

namespace PakViewer.Providers
{
    /// <summary>
    /// 天堂客戶端資料夾提供者 - 處理多個 IDX/PAK 檔案
    /// </summary>
    public class LinClientProvider : IFileProvider
    {
        public static string AllSourcesOption => I18n.T("Filter.All");

        private readonly string _folderPath;
        private readonly Dictionary<string, PakFile> _pakFiles;  // IdxName -> PakFile
        private readonly List<FileEntry> _allFiles;              // 所有檔案
        private List<FileEntry> _filteredFiles;                   // 篩選後的檔案
        private string _currentSourceOption;
        private bool _disposed;

        /// <summary>
        /// 建立天堂客戶端提供者
        /// </summary>
        /// <param name="folderPath">客戶端資料夾路徑</param>
        public LinClientProvider(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

            _folderPath = folderPath;
            _pakFiles = new Dictionary<string, PakFile>(StringComparer.OrdinalIgnoreCase);
            _allFiles = new List<FileEntry>();

            // 找出所有 IDX 檔案
            var idxFiles = Directory.GetFiles(folderPath, "*.idx", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            int globalIndex = 0;
            foreach (var idxPath in idxFiles)
            {
                var idxName = Path.GetFileName(idxPath);

                try
                {
                    var pak = new PakFile(idxPath);
                    _pakFiles[idxName] = pak;

                    // 將此 PAK 的檔案加入總列表
                    foreach (var record in pak.Files)
                    {
                        _allFiles.Add(new FileEntry
                        {
                            Index = globalIndex++,
                            FileName = record.FileName,
                            FileSize = record.FileSize,
                            Offset = record.Offset,
                            SourceName = idxName,
                            Source = this
                        });
                    }
                }
                catch
                {
                    // 忽略無法開啟的 IDX 檔案
                }
            }

            // 預設選擇第一個 IDX (如果有 text.idx 優先選它)
            var defaultOption = _pakFiles.Keys
                .FirstOrDefault(k => k.Equals("text.idx", StringComparison.OrdinalIgnoreCase))
                ?? _pakFiles.Keys.FirstOrDefault()
                ?? AllSourcesOption;
            SetSourceOption(defaultOption);
        }

        public string Name => Path.GetFileName(_folderPath);

        public int Count => _filteredFiles?.Count ?? _allFiles.Count;

        public IReadOnlyList<FileEntry> Files => (_filteredFiles ?? _allFiles).AsReadOnly();

        /// <summary>
        /// 取得所有已載入的 PAK 檔案
        /// </summary>
        public IReadOnlyDictionary<string, PakFile> PakFiles => _pakFiles;

        /// <summary>
        /// 取得指定 IDX 的 PakFile
        /// </summary>
        public PakFile GetPakFile(string idxName)
        {
            return _pakFiles.TryGetValue(idxName, out var pak) ? pak : null;
        }

        public byte[] Extract(int index)
        {
            var files = _filteredFiles ?? _allFiles;
            if (index < 0 || index >= files.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var entry = files[index];
            return Extract(entry);
        }

        public byte[] Extract(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!_pakFiles.TryGetValue(entry.SourceName, out var pak))
                throw new InvalidOperationException($"PAK file not found: {entry.SourceName}");

            // 找出在該 PAK 內的實際索引
            var pakIndex = pak.Files.ToList().FindIndex(f =>
                f.FileName == entry.FileName && f.Offset == entry.Offset);

            if (pakIndex < 0)
                throw new InvalidOperationException($"File not found in PAK: {entry.FileName}");

            return pak.Extract(pakIndex);
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

        /// <summary>
        /// 取得所有已載入的 IDX 檔案名稱
        /// </summary>
        public IEnumerable<string> GetIdxNames()
        {
            return _pakFiles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        }

        // Source options - 天堂客戶端有多個 IDX 選項
        public IEnumerable<string> GetSourceOptions()
        {
            // 如果有多個 IDX，提供 "全部" 選項
            if (_pakFiles.Count > 1)
                yield return AllSourcesOption;

            foreach (var idxName in _pakFiles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                yield return idxName;
        }

        public void SetSourceOption(string option)
        {
            _currentSourceOption = option;

            if (option == AllSourcesOption || string.IsNullOrEmpty(option))
            {
                // 顯示所有檔案
                _filteredFiles = null;
            }
            else
            {
                // 篩選特定 IDX 的檔案
                _filteredFiles = _allFiles
                    .Where(f => f.SourceName.Equals(option, StringComparison.OrdinalIgnoreCase))
                    .Select((f, i) => new FileEntry
                    {
                        Index = i,  // 重新編號
                        FileName = f.FileName,
                        FileSize = f.FileSize,
                        Offset = f.Offset,
                        SourceName = f.SourceName,
                        Source = f.Source
                    })
                    .ToList();
            }
        }

        public string CurrentSourceOption => _currentSourceOption ?? AllSourcesOption;

        public bool HasMultipleSourceOptions => _pakFiles.Count > 1;

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var pak in _pakFiles.Values)
                {
                    pak?.Dispose();
                }
                _pakFiles.Clear();
                _disposed = true;
            }
        }
    }
}
