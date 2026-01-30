using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto;
using Eto.Forms;
using Eto.Drawing;
using Lin.Helper.Core.Pak;
using Lin.Helper.Core.Sprite;
using Lin.Helper.Core.Image;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using L1ImageConverter = Lin.Helper.Core.Image.ImageConverter;
using Lin.Helper.Core.Tile;
using System.Text.Json;
using PakViewer.Utility;
using PakViewer.Localization;
using PakViewer.Providers;

namespace PakViewer
{
    /// <summary>
    /// Cross-platform main form using Eto.Forms
    /// </summary>
    public partial class MainForm : Form
    {
        // Data
        private string _selectedFolder;
        private PakFile _currentPak;
        private IFileProvider _currentProvider;  // 檔案提供者 (資料夾/單一檔案模式)
        private List<int> _filteredIndexes;
        private HashSet<int> _checkedIndexes = new HashSet<int>();
        private string _currentFilter = "";
        private string _currentExtFilter = "All";
        private string _currentLangFilter = "All";
        private string _contentSearchKeyword = "";
        private HashSet<int> _contentSearchResults;

        // File grid sorting
        private enum SortColumn { None, Index, FileName, Size, IdxName }
        private SortColumn _currentSortColumn = SortColumn.None;
        private bool _sortAscending = true;
        private GridColumn _sortedGridColumn;  // Track the currently sorted column for header update

        // SPR List Mode
        private SprListFile _sprListFile;
        private List<SprListEntry> _filteredSprListEntries;
        private Dictionary<int, PakFile> _spritePakFiles;  // SpriteId -> PakFile mapping

        // 模式控制 (Radio: 一般/SPR/SPR List)
        private const int MODE_NORMAL = 0;
        private const int MODE_SPR = 1;
        private const int MODE_SPR_LIST = 2;
        private RadioButtonList _viewModeRadio;
        private int _previousModeIndex = 0;

        // SPR Mode
        private Dictionary<int, SprGroup> _sprGroups;
        private int? _sprModeTypeFilter = null;
        private bool _sprModeUnreferencedFilter = false;
        private GridView _sprGroupGrid;
        private int _sprGroupSortColumn = 1;  // 預設按 ID 排序 (column index 1)
        private bool _sprGroupSortAscending = true;
        private CheckBox _sprModeListSprCheck;      // list.spr 是否已設定
        private Label _sprModeListSprLabel;         // list.spr 路徑顯示
        private string _originalIdxText;            // 儲存原本的 IDX 選項

        // All IDX mode
        private bool _isAllIdxMode = false;
        private Dictionary<string, PakFile> _allPakFiles;  // IdxName -> PakFile mapping

        // 當前預覽檔案的來源（用於儲存功能）
        private PakFile _currentViewerPak;
        private int _currentViewerIndex;
        private string _currentViewerFileName;

        // UI Controls
        private GridView _fileGrid;
        private GridView _sprListGrid;
        private int _sprListSortColumn = -1;  // 當前排序欄位 (-1 表示無排序)
        private bool _sprListSortAscending = true;  // 排序方向
        private TextBox _searchBox;
        private UITimer _searchDebounceTimer;
        private TextBox _contentSearchBox;
        private Button _contentSearchBtn;
        private Button _clearSearchBtn;
        private Button _openFolderBtn;
        private DropDown _idxDropDown;
        private DropDown _extFilterDropDown;
        private DropDown _langFilterDropDown;
        private DropDown _sprTypeFilterDropDown;
        private Label _sprTypeLabel;
        private Label _folderLabel;
        private Label _statusLabel;
        private Label _recordCountLabel;
        private Panel _leftListPanel;

        // Viewers (模組化架構)
        private Panel _viewerPanel;
        private Viewers.IFileViewer _currentViewer;

        // 右側面板模式切換
        private Panel _rightPanelContainer;
        private RadioButton _previewModeRadio;
        private RadioButton _galleryModeRadio;
        private Controls.GalleryPanel _rightGalleryPanel;
        private Slider _rightThumbnailSlider;
        private int _cachedThumbnailSize = 80;  // 快取的縮圖大小，避免從背景執行緒存取 UI 控制項
        private List<Controls.GalleryItem> _rightGalleryItems = new();

        // Tab control
        private TabControl _mainTabControl;
        private TabPage _browserPage;
        private Dictionary<string, TabPage> _openTabs = new Dictionary<string, TabPage>();

        // Right panel search toolbar
        private TextBox _textSearchBox;
        private Button _textSearchNextBtn;
        private Button _textSearchPrevBtn;
        private Label _textSearchResultLabel;
        private Panel _searchToolbarContainer;  // 用於動態替換搜尋工具列
        private Control _defaultSearchToolbar;  // 預設搜尋工具列
        private Button _exportPivotTableBtn;    // SPR List 模式的動作矩陣匯出按鈕

        // Settings
        private AppSettings _settings;

        public MainForm()
        {
            _settings = AppSettings.Load();

            // Apply saved language before creating UI
            if (!string.IsNullOrEmpty(_settings.Language))
            {
                I18n.SetLanguage(_settings.Language);
            }

            Title = I18n.T("AppTitle");
            ClientSize = new Size(1200, 800);
            MinimumSize = new Size(800, 600);

            CreateMenu();
            CreateLayout();

            // Load last session
            LoadLastSession();
        }

        protected override void OnClosed(EventArgs e)
        {
            _currentViewer?.Dispose();
            _currentPak?.Dispose();
            _currentProvider?.Dispose();
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// File item for grid display
    /// </summary>
    public class FileItem
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public int FileSize { get; set; }
        public long Offset { get; set; }  // 使用 long 支援超過 2GB 的 PAK
        public string IdxName { get; set; }  // 來源 IDX 檔名
        public PakFile SourcePak { get; set; }  // 來源 PAK 檔案

        public string SizeText => FileSize >= 1024
            ? $"{FileSize / 1024.0:F1} KB"
            : $"{FileSize} B";
    }

    /// <summary>
    /// SPR List item for grid display
    /// </summary>
    public class SprListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SpriteId { get; set; }
        public int ImageCount { get; set; }
        public int? TypeId { get; set; }
        public int ActionCount { get; set; }
        public SprListEntry Entry { get; set; }
        public bool? IsChecked { get; set; } = false;  // 勾選狀態

        public string TypeName => Entry?.TypeName ?? I18n.T("Filter.Unknown");
    }

    /// <summary>
    /// DAT file item for grid display
    /// </summary>
    public class DatFileItem
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public int FileSize { get; set; }
        public int Offset { get; set; }
        public DatTools.DatIndexEntry Entry { get; set; }

        public string SizeText => FileSize >= 1024
            ? $"{FileSize / 1024.0:F1} KB"
            : $"{FileSize} B";
    }

    /// <summary>
    /// Application settings
    /// </summary>
    public class AppSettings
    {
        public string LastFolder { get; set; }           // 檔案對話框預設路徑
        public string LastClientFolder { get; set; }     // 有效的 client 資料夾（含 .idx 檔案）
        public string LastIdxFile { get; set; }
        public string LastSprListFile { get; set; }
        public string SprModeListSprPath { get; set; }  // SPR 模式用的 list.spr 路徑
        public string Language { get; set; } = "zh-TW"; // 預設語言
        public int SprPreviewBgColor { get; set; } = 0;  // SPR 預覽背景色 (0=黑, 1=紅, 2=透明, 3=白)

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PakViewer",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }

    /// <summary>
    /// Natural string comparer for sorting filenames like 1.til, 2.til, 10.til, 100.til correctly
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0, iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                // Check if both are digits
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    // Extract numeric parts
                    long numX = 0, numY = 0;
                    while (ix < x.Length && char.IsDigit(x[ix]))
                    {
                        numX = numX * 10 + (x[ix] - '0');
                        ix++;
                    }
                    while (iy < y.Length && char.IsDigit(y[iy]))
                    {
                        numY = numY * 10 + (y[iy] - '0');
                        iy++;
                    }

                    int numCompare = numX.CompareTo(numY);
                    if (numCompare != 0) return numCompare;
                }
                else
                {
                    // Compare characters (case-insensitive)
                    int charCompare = char.ToLowerInvariant(x[ix]).CompareTo(char.ToLowerInvariant(y[iy]));
                    if (charCompare != 0) return charCompare;
                    ix++;
                    iy++;
                }
            }

            // Shorter string comes first
            return x.Length.CompareTo(y.Length);
        }
    }
}
