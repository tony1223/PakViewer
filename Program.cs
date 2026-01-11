using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eto;
using Eto.Forms;
using Eto.Drawing;
using Lin.Helper.Core.Pak;
using Lin.Helper.Core.Sprite;
using Lin.Helper.Core.Image;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using System.Text.Json;
using PakViewer.Utility;

namespace PakViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Register Big5, GB2312, Shift_JIS, EUC-KR etc. encoding support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // CLI mode
            if (args.Length > 0 && args[0] == "-cli")
            {
                Console.WriteLine("CLI mode not yet implemented in cross-platform version.");
                Console.WriteLine("Use Lin.Helper.Core library directly for CLI operations.");
                return;
            }

            // GUI mode - use Eto.Forms for cross-platform
            new Application(Platform.Detect).Run(new MainForm());
        }
    }

    /// <summary>
    /// Cross-platform main form using Eto.Forms
    /// </summary>
    public class MainForm : Form
    {
        // Data
        private string _selectedFolder;
        private PakFile _currentPak;
        private List<int> _filteredIndexes;
        private HashSet<int> _checkedIndexes = new HashSet<int>();
        private string _currentFilter = "";
        private string _currentExtFilter = "All";
        private string _currentLangFilter = "All";
        private string _contentSearchKeyword = "";
        private HashSet<int> _contentSearchResults;

        // SPR List Mode
        private bool _isSprListMode = false;
        private SprListFile _sprListFile;
        private List<SprListEntry> _filteredSprListEntries;
        private int? _sprListTypeFilter = null;
        private Dictionary<int, PakFile> _spritePakFiles;  // SpriteId -> PakFile mapping

        // UI Controls
        private GridView _fileGrid;
        private GridView _sprListGrid;
        private TextBox _searchBox;
        private TextBox _contentSearchBox;
        private Button _contentSearchBtn;
        private Button _clearSearchBtn;
        private DropDown _idxDropDown;
        private DropDown _extFilterDropDown;
        private DropDown _langFilterDropDown;
        private DropDown _sprTypeFilterDropDown;
        private CheckBox _sprListModeCheck;
        private Label _folderLabel;
        private Label _statusLabel;
        private Label _recordCountLabel;
        private Panel _leftListPanel;

        // Viewers (模組化架構)
        private Panel _viewerPanel;
        private Viewers.IFileViewer _currentViewer;

        // Tab control
        private TabControl _mainTabControl;
        private TabPage _browserPage;
        private Dictionary<string, TabPage> _openTabs = new Dictionary<string, TabPage>();

        // Right panel search toolbar
        private TextBox _textSearchBox;
        private Button _textSearchNextBtn;
        private Button _textSearchPrevBtn;
        private Label _textSearchResultLabel;

        // Settings
        private AppSettings _settings;

        public MainForm()
        {
            Title = "PakViewer (Cross-Platform)";
            ClientSize = new Size(1200, 800);
            MinimumSize = new Size(800, 600);

            _settings = AppSettings.Load();

            CreateMenu();
            CreateLayout();

            // Load last session
            LoadLastSession();
        }

        private void LoadLastSession()
        {
            // 只載入有效的 client 資料夾（含 .idx 檔案的資料夾）
            if (!string.IsNullOrEmpty(_settings.LastClientFolder) && Directory.Exists(_settings.LastClientFolder))
            {
                _selectedFolder = _settings.LastClientFolder;
                _folderLabel.Text = Path.GetFileName(_selectedFolder);

                // Find .idx files
                var idxFiles = Directory.GetFiles(_selectedFolder, "*.idx", SearchOption.TopDirectoryOnly);

                _idxDropDown.Items.Clear();
                foreach (var file in idxFiles.OrderBy(f => f))
                {
                    _idxDropDown.Items.Add(Path.GetFileName(file));
                }

                if (_idxDropDown.Items.Count > 0)
                {
                    // Select last idx file or text.idx
                    int selectIndex = -1;
                    if (!string.IsNullOrEmpty(_settings.LastIdxFile))
                    {
                        selectIndex = _idxDropDown.Items.ToList().FindIndex(i => i.Text == _settings.LastIdxFile);
                    }
                    if (selectIndex < 0)
                    {
                        var textIdx = _idxDropDown.Items.FirstOrDefault(i => i.Text.Equals("text.idx", StringComparison.OrdinalIgnoreCase));
                        selectIndex = textIdx != null ? _idxDropDown.Items.IndexOf(textIdx) : 0;
                    }
                    _idxDropDown.SelectedIndex = selectIndex >= 0 ? selectIndex : 0;
                }

                _statusLabel.Text = $"Loaded last session: {Path.GetFileName(_selectedFolder)}";
            }
        }

        private void CreateMenu()
        {
            var menu = new MenuBar();

            // File menu
            var fileMenu = new SubMenuItem { Text = "&File" };

            var openFolderCmd = new Command { MenuText = "Open &Folder...", Shortcut = Keys.Control | Keys.O };
            openFolderCmd.Executed += OnOpenFolder;
            fileMenu.Items.Add(openFolderCmd);

            var openIdxCmd = new Command { MenuText = "Open &IDX File...", Shortcut = Keys.Control | Keys.I };
            openIdxCmd.Executed += OnOpenIdxFile;
            fileMenu.Items.Add(openIdxCmd);

            var openSprListCmd = new Command { MenuText = "Open &SPR List (list.spr)...", Shortcut = Keys.Control | Keys.L };
            openSprListCmd.Executed += OnOpenSprList;
            fileMenu.Items.Add(openSprListCmd);

            var openDatCmd = new Command { MenuText = "Open Lineage &M DAT...", Shortcut = Keys.Control | Keys.M };
            openDatCmd.Executed += OnOpenDatFile;
            fileMenu.Items.Add(openDatCmd);

            fileMenu.Items.Add(new SeparatorMenuItem());

            var quitCmd = new Command { MenuText = "&Quit", Shortcut = Keys.Control | Keys.Q };
            quitCmd.Executed += (s, e) => Application.Instance.Quit();
            fileMenu.Items.Add(quitCmd);

            menu.Items.Add(fileMenu);

            // Tools menu
            var toolsMenu = new SubMenuItem { Text = "&Tools" };

            var exportCmd = new Command { MenuText = "&Export Selected..." };
            exportCmd.Executed += OnExportSelected;
            toolsMenu.Items.Add(exportCmd);

            var exportAllCmd = new Command { MenuText = "Export &All..." };
            exportAllCmd.Executed += OnExportAll;
            toolsMenu.Items.Add(exportAllCmd);

            menu.Items.Add(toolsMenu);

            Menu = menu;
        }

        private void CreateLayout()
        {
            // Create main tab control
            _mainTabControl = new TabControl();

            // Create browser tab content
            var mainSplitter = new Splitter
            {
                Orientation = Orientation.Horizontal,
                Position = 400,
                FixedPanel = SplitterFixedPanel.Panel1
            };

            // Left panel - file list
            mainSplitter.Panel1 = CreateLeftPanel();

            // Right panel - viewer
            mainSplitter.Panel2 = CreateRightPanel();

            // Create browser page (non-closable)
            _browserPage = new TabPage
            {
                Text = "Lin Client",
                Content = mainSplitter
            };
            _mainTabControl.Pages.Add(_browserPage);

            // Status bar
            _statusLabel = new Label { Text = "Ready" };
            _recordCountLabel = new Label { Text = "Records: 0" };

            var statusBar = new TableLayout
            {
                Padding = new Padding(5),
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_statusLabel, true),
                        new TableCell(_recordCountLabel)
                    )
                }
            };

            Content = new TableLayout
            {
                Rows =
                {
                    new TableRow(_mainTabControl) { ScaleHeight = true },
                    new TableRow(statusBar)
                }
            };
        }

        private Control CreateLeftPanel()
        {
            // Folder label with Open button
            _folderLabel = new Label { Text = "(none)", VerticalAlignment = VerticalAlignment.Center };
            var openFolderBtn = new Button { Text = "Open...", Width = 70 };
            openFolderBtn.Click += OnOpenFolder;

            // IDX dropdown
            _idxDropDown = new DropDown();
            _idxDropDown.SelectedIndexChanged += OnIdxChanged;

            // SPR List mode checkbox
            _sprListModeCheck = new CheckBox { Text = "SPR List Mode" };
            _sprListModeCheck.CheckedChanged += OnSprListModeChanged;

            // Search box (filename)
            _searchBox = new TextBox { PlaceholderText = "Search..." };
            _searchBox.TextChanged += OnSearchChanged;

            // Extension filter
            _extFilterDropDown = new DropDown();
            _extFilterDropDown.Items.Add("All");
            _extFilterDropDown.SelectedIndex = 0;
            _extFilterDropDown.SelectedIndexChanged += OnExtFilterChanged;

            // Language filter
            _langFilterDropDown = new DropDown();
            _langFilterDropDown.Items.Add("All");
            _langFilterDropDown.Items.Add("-c (繁中)");
            _langFilterDropDown.Items.Add("-h (港)");
            _langFilterDropDown.Items.Add("-j (日)");
            _langFilterDropDown.Items.Add("-k (韓)");
            _langFilterDropDown.SelectedIndex = 0;
            _langFilterDropDown.SelectedIndexChanged += OnLangFilterChanged;

            // SPR Type filter (for SPR List mode)
            _sprTypeFilterDropDown = new DropDown { Visible = false };
            _sprTypeFilterDropDown.Items.Add("All Types");
            _sprTypeFilterDropDown.Items.Add("0 - 影子/法術");
            _sprTypeFilterDropDown.Items.Add("1 - 裝飾品");
            _sprTypeFilterDropDown.Items.Add("5 - 玩家/NPC");
            _sprTypeFilterDropDown.Items.Add("6 - 可對話NPC");
            _sprTypeFilterDropDown.Items.Add("7 - 寶箱/開關");
            _sprTypeFilterDropDown.Items.Add("8 - 門");
            _sprTypeFilterDropDown.Items.Add("9 - 物品");
            _sprTypeFilterDropDown.Items.Add("10 - 怪物");
            _sprTypeFilterDropDown.Items.Add("11 - 城牆/城門");
            _sprTypeFilterDropDown.Items.Add("12 - 新NPC");
            _sprTypeFilterDropDown.SelectedIndex = 0;
            _sprTypeFilterDropDown.SelectedIndexChanged += OnSprTypeFilterChanged;

            // Content search
            _contentSearchBox = new TextBox { PlaceholderText = "Content search..." };
            _contentSearchBtn = new Button { Text = "Search" };
            _contentSearchBtn.Click += OnContentSearch;
            _clearSearchBtn = new Button { Text = "Clear" };
            _clearSearchBtn.Click += OnClearContentSearch;

            // File grid (normal mode)
            _fileGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "No.",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "FileName",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.FileName) },
                Width = 180
            });

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Size",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.SizeText) },
                Width = 80
            });

            _fileGrid.SelectionChanged += OnFileSelected;
            _fileGrid.CellDoubleClick += OnFileDoubleClick;

            // Context menu for file grid
            var fileContextMenu = new ContextMenu();

            var openInTabMenuItem = new ButtonMenuItem { Text = "Open in New Tab" };
            openInTabMenuItem.Click += OnOpenInNewTab;
            fileContextMenu.Items.Add(openInTabMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var exportMenuItem = new ButtonMenuItem { Text = "Export Selected..." };
            exportMenuItem.Click += OnExportSelected;
            fileContextMenu.Items.Add(exportMenuItem);

            var exportToMenuItem = new ButtonMenuItem { Text = "Export To..." };
            exportToMenuItem.Click += OnExportSelectedTo;
            fileContextMenu.Items.Add(exportToMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var copyFileNameMenuItem = new ButtonMenuItem { Text = "Copy Filename" };
            copyFileNameMenuItem.Click += OnCopyFileName;
            fileContextMenu.Items.Add(copyFileNameMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var selectAllMenuItem = new ButtonMenuItem { Text = "Select All" };
            selectAllMenuItem.Click += OnSelectAll;
            fileContextMenu.Items.Add(selectAllMenuItem);

            var unselectAllMenuItem = new ButtonMenuItem { Text = "Unselect All" };
            unselectAllMenuItem.Click += OnUnselectAll;
            fileContextMenu.Items.Add(unselectAllMenuItem);

            _fileGrid.ContextMenu = fileContextMenu;

            // SPR List grid (SPR List mode)
            _sprListGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true,
                Visible = false
            };

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "ID",
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.Id.ToString()) },
                Width = 50
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "名稱",
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.Name) },
                Width = 120
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "圖檔",
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.SpriteId.ToString()) },
                Width = 50
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "圖數",
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.ImageCount.ToString()) },
                Width = 40
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "類型",
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.TypeName) },
                Width = 80
            });

            _sprListGrid.SelectionChanged += OnSprListSelected;

            // 統一標籤寬度
            const int labelWidth = 50;

            var topBar = new TableLayout
            {
                Padding = new Padding(5),
                Spacing = new Size(5, 5),
                Rows =
                {
                    // Folder row
                    new TableRow(
                        new TableCell(new Label { Text = "Folder:", Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Items = { _folderLabel, null, openFolderBtn }
                        }, true)
                    ),
                    // IDX row
                    new TableRow(
                        new TableCell(new Label { Text = "IDX:", Width = labelWidth }, false),
                        new TableCell(_idxDropDown, true)
                    ),
                    // SPR List Mode (indent to align with controls)
                    new TableRow(
                        new TableCell(new Panel { Width = labelWidth }, false),
                        new TableCell(_sprListModeCheck, true)
                    ),
                    // Ext row
                    new TableRow(
                        new TableCell(new Label { Text = "Ext:", Width = labelWidth }, false),
                        new TableCell(_extFilterDropDown, true)
                    ),
                    // Lang row
                    new TableRow(
                        new TableCell(new Label { Text = "Lang:", Width = labelWidth }, false),
                        new TableCell(_langFilterDropDown, true)
                    ),
                    // Type row
                    new TableRow(
                        new TableCell(new Label { Text = "Type:", Width = labelWidth }, false),
                        new TableCell(_sprTypeFilterDropDown, true)
                    ),
                    // Search row
                    new TableRow(
                        new TableCell(new Label { Text = "Filter:", Width = labelWidth }, false),
                        new TableCell(_searchBox, true)
                    ),
                    // Content search row
                    new TableRow(
                        new TableCell(new Label { Text = "Search:", Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 2,
                            Items = { new StackLayoutItem(_contentSearchBox, true), _contentSearchBtn, _clearSearchBtn }
                        }, true)
                    )
                }
            };

            _leftListPanel = new Panel();
            // Start with file grid visible
            _leftListPanel.Content = _fileGrid;

            return new TableLayout
            {
                Rows =
                {
                    new TableRow(topBar),
                    new TableRow(_leftListPanel) { ScaleHeight = true }
                }
            };
        }

        private Control CreateRightPanel()
        {
            // Search toolbar for text viewer
            _textSearchBox = new TextBox { PlaceholderText = "Search in text...", Width = 200 };
            _textSearchBox.KeyDown += OnTextSearchKeyDown;

            _textSearchPrevBtn = new Button { Text = "◀", Width = 30 };
            _textSearchPrevBtn.Click += OnTextSearchPrev;

            _textSearchNextBtn = new Button { Text = "▶", Width = 30 };
            _textSearchNextBtn.Click += OnTextSearchNext;

            _textSearchResultLabel = new Label { Text = "" };

            var searchToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    new Label { Text = "Find:" },
                    _textSearchBox,
                    _textSearchPrevBtn,
                    _textSearchNextBtn,
                    _textSearchResultLabel
                }
            };

            _viewerPanel = new Panel { BackgroundColor = Colors.DarkGray };

            // Wrap viewer panel with search toolbar
            return new TableLayout
            {
                Rows =
                {
                    new TableRow(searchToolbar),
                    new TableRow(_viewerPanel) { ScaleHeight = true }
                }
            };
        }

        /// <summary>
        /// 使用 ViewerFactory 顯示檔案預覽
        /// </summary>
        private void ShowFilePreview(string ext, byte[] data, string fileName)
        {
            // 釋放舊的 viewer
            _currentViewer?.Dispose();
            _currentViewer = null;

            // 使用 ViewerFactory 建立新的 viewer
            _currentViewer = Viewers.ViewerFactory.CreateViewerSmart(ext, data);
            _currentViewer.LoadData(data, fileName);

            // 顯示 viewer 控件
            _viewerPanel.Content = _currentViewer.GetControl();

            // 重置搜尋狀態
            _textSearchResultLabel.Text = "";
        }

        // 使用 ViewerFactory 的方法
        private static bool IsTextContent(byte[] data) => Viewers.ViewerFactory.IsTextContent(data);
        private static bool IsPngContent(byte[] data) => Viewers.ViewerFactory.IsPngContent(data);

        private static Encoding DetectEncoding(byte[] data, string fileName)
        {
            // Register code pages if not already done
            var lowerName = fileName?.ToLower() ?? "";
            if (lowerName.Contains("-c") || lowerName.Contains("_c"))
                return Encoding.GetEncoding("big5");
            if (lowerName.Contains("-j") || lowerName.Contains("_j"))
                return Encoding.GetEncoding("shift_jis");
            if (lowerName.Contains("-k") || lowerName.Contains("_k"))
                return Encoding.GetEncoding("euc-kr");
            if (lowerName.Contains("-h") || lowerName.Contains("_h"))
                return Encoding.GetEncoding("gb2312");

            // Check BOM
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode;

            // Default to Big5 for Lineage files
            return Encoding.GetEncoding("big5");
        }

        #region Event Handlers

        private void OnOpenFolder(object sender, EventArgs e)
        {
            using var dialog = new SelectFolderDialog
            {
                Title = "Select Lineage Client Folder"
            };

            // Use last folder as starting point
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = _settings.LastFolder;
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                _selectedFolder = dialog.Directory;
                _folderLabel.Text = Path.GetFileName(_selectedFolder);

                // Save to settings (這是有效的 client 資料夾)
                _settings.LastFolder = _selectedFolder;
                _settings.LastClientFolder = _selectedFolder;
                _settings.Save();

                // Find .idx files
                var idxFiles = Directory.GetFiles(_selectedFolder, "*.idx", SearchOption.TopDirectoryOnly);

                _idxDropDown.Items.Clear();
                foreach (var file in idxFiles.OrderBy(f => f))
                {
                    _idxDropDown.Items.Add(Path.GetFileName(file));
                }

                if (_idxDropDown.Items.Count > 0)
                {
                    // Prefer text.idx
                    var textIdx = _idxDropDown.Items.FirstOrDefault(i => i.Text.Equals("text.idx", StringComparison.OrdinalIgnoreCase));
                    _idxDropDown.SelectedIndex = textIdx != null ? _idxDropDown.Items.IndexOf(textIdx) : 0;
                }

                _statusLabel.Text = $"Found {idxFiles.Length} IDX files";
            }
        }

        private void OnOpenIdxFile(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open IDX File",
                Filters = { new FileFilter("IDX Files", ".idx"), new FileFilter("All Files", ".*") }
            };

            // Use last folder as starting point
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = new Uri(_settings.LastFolder);
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                OpenIdxInNewTab(dialog.FileName);
            }
        }

        private void OpenIdxInNewTab(string idxPath)
        {
            var tabKey = $"idx:{idxPath}";

            // Check if already open
            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var pak = new PakFile(idxPath);
                var idxName = Path.GetFileName(idxPath);

                // Create browser content for this IDX
                var browserContent = CreateIdxBrowserContent(pak, idxPath);

                var docPage = new TabPage
                {
                    Text = $"{idxName}",
                    Content = browserContent
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                // Save to settings
                _settings.LastFolder = Path.GetDirectoryName(idxPath);
                _settings.LastIdxFile = idxName;
                _settings.Save();

                _statusLabel.Text = $"Opened: {idxName} ({pak.Count} files)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening IDX: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private Control CreateIdxBrowserContent(PakFile pak, string idxPath)
        {
            // Create file grid for this pak
            var fileGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "No.",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "FileName",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.FileName) },
                Width = 180
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Size",
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.SizeText) },
                Width = 80
            });

            // Create viewer panel
            var viewerPanel = new Panel { BackgroundColor = Colors.DarkGray };

            // Create image/text/sprite viewers
            var imageViewer = new ImageView();
            // Wrap image in centered container
            var imageCenterContainer = new TableLayout
            {
                BackgroundColor = Colors.DarkGray,
                Rows =
                {
                    new TableRow { ScaleHeight = true },
                    new TableRow(new TableCell(null, true), new TableCell(imageViewer, false), new TableCell(null, true)),
                    new TableRow { ScaleHeight = true }
                }
            };
            var textViewer = new RichTextArea { ReadOnly = true, Font = new Font("Menlo, Monaco, Consolas, monospace", 12) };
            var spriteViewer = new Drawable { BackgroundColor = Colors.DarkRed };

            // Sprite animation state
            SprFrame[] currentFrames = null;
            int currentFrameIndex = 0;
            var animTimer = new UITimer { Interval = 0.15 };

            spriteViewer.Paint += (s, e) =>
            {
                if (currentFrames == null || currentFrames.Length == 0) return;
                var frame = currentFrames[currentFrameIndex];
                if (frame.Image == null) return;

                using var ms = new MemoryStream();
                frame.Image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using var bitmap = new Bitmap(ms);

                var scale = 2.0f;
                var x = (spriteViewer.Width - frame.Width * scale) / 2;
                var y = (spriteViewer.Height - frame.Height * scale) / 2;
                e.Graphics.DrawImage(bitmap, (float)x, (float)y, frame.Width * scale, frame.Height * scale);

                var info = $"Frame {currentFrameIndex + 1}/{currentFrames.Length}  Size: {frame.Width}x{frame.Height}";
                e.Graphics.DrawText(new Font(SystemFont.Default), Colors.White, 10, 10, info);
            };

            animTimer.Elapsed += (s, e) =>
            {
                if (currentFrames != null && currentFrames.Length > 0)
                {
                    currentFrameIndex = (currentFrameIndex + 1) % currentFrames.Length;
                    spriteViewer.Invalidate();
                }
            };

            // Load file list
            var items = new List<FileItem>();
            for (int i = 0; i < pak.Count; i++)
            {
                var file = pak.Files[i];
                items.Add(new FileItem
                {
                    Index = i,
                    FileName = file.FileName,
                    FileSize = file.FileSize,
                    Offset = file.Offset
                });
            }
            fileGrid.DataStore = items;

            // Selection handler
            fileGrid.SelectionChanged += (s, e) =>
            {
                var selected = fileGrid.SelectedItem as FileItem;
                if (selected == null) return;

                animTimer.Stop();
                currentFrames = null;

                try
                {
                    var data = pak.Extract(selected.Index);
                    var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();

                    if (ext == ".spr")
                    {
                        currentFrames = SprReader.Load(data);
                        currentFrameIndex = 0;
                        viewerPanel.Content = spriteViewer;
                        spriteViewer.Invalidate();
                        if (currentFrames.Length > 1) animTimer.Start();
                    }
                    else if (ext == ".png" || ext == ".bmp" || ext == ".jpg" || ext == ".gif")
                    {
                        using var ms = new MemoryStream(data);
                        imageViewer.Image = new Bitmap(ms);
                        viewerPanel.Content = imageCenterContainer;
                    }
                    else if (IsTextContent(data))
                    {
                        var encoding = DetectEncoding(data, selected.FileName);
                        textViewer.Text = encoding.GetString(data);
                        viewerPanel.Content = textViewer;
                    }
                    else
                    {
                        textViewer.Text = "Binary file";
                        viewerPanel.Content = textViewer;
                    }

                    _statusLabel.Text = $"Selected: {selected.FileName} ({selected.SizeText})";
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"Error: {ex.Message}";
                }
            };

            // Double click to open in new tab
            fileGrid.CellDoubleClick += (s, e) =>
            {
                var selected = fileGrid.SelectedItem as FileItem;
                if (selected == null) return;

                try
                {
                    var data = pak.Extract(selected.Index);
                    var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();
                    var content = CreateTabContent(ext, data, selected.FileName);
                    if (content == null) return;

                    var tabKey = $"{idxPath}:{selected.Index}";
                    if (_openTabs.ContainsKey(tabKey))
                    {
                        _mainTabControl.SelectedPage = _openTabs[tabKey];
                        return;
                    }

                    var docPage = new TabPage
                    {
                        Text = selected.FileName,
                        Content = content
                    };
                    docPage.Tag = tabKey;

                    _openTabs[tabKey] = docPage;
                    _mainTabControl.Pages.Add(docPage);
                    _mainTabControl.SelectedPage = docPage;
                }
                catch { }
            };

            // Layout
            var splitter = new Splitter
            {
                Orientation = Orientation.Horizontal,
                Position = 350,
                FixedPanel = SplitterFixedPanel.Panel1,
                Panel1 = fileGrid,
                Panel2 = viewerPanel
            };

            return splitter;
        }

        private void OnIdxChanged(object sender, EventArgs e)
        {
            if (_idxDropDown.SelectedIndex < 0 || string.IsNullOrEmpty(_selectedFolder))
                return;

            var idxFile = Path.Combine(_selectedFolder, _idxDropDown.SelectedValue.ToString());
            LoadIdxFile(idxFile);
        }

        private void OnOpenDatFile(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open Lineage M DAT File",
                Filters = { new FileFilter("DAT Files", ".dat"), new FileFilter("All Files", ".*") }
            };

            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = new Uri(_settings.LastFolder);
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                OpenDatInNewTab(dialog.FileName);
            }
        }

        private void OpenDatInNewTab(string datPath)
        {
            var tabKey = $"dat:{datPath}";

            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var datFile = new DatTools.DatFile(datPath);
                datFile.ReadFooter();
                datFile.DecryptIndex();
                datFile.ParseEntries();

                var datName = Path.GetFileName(datPath);
                var browserContent = CreateDatBrowserContent(datFile);

                var docPage = new TabPage
                {
                    Text = $"{datName}",
                    Content = browserContent
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                _settings.LastFolder = Path.GetDirectoryName(datPath);
                _settings.Save();

                _statusLabel.Text = $"Opened DAT: {datName} ({datFile.Entries.Count} files, Encrypted: {datFile.Footer.IsEncrypted})";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening DAT: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private Control CreateDatBrowserContent(DatTools.DatFile datFile)
        {
            // Filter toolbar
            var filterLabel = new Label { Text = "Filter:", VerticalAlignment = VerticalAlignment.Center };
            var filterBox = new TextBox { PlaceholderText = "Type to filter...", Width = 200 };
            var recordLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
            var filterToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                Items = { filterLabel, filterBox, null, recordLabel }
            };

            var fileGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "No.",
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "FileName",
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.FileName) },
                Width = 200
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Size",
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.SizeText) },
                Width = 80
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Offset",
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => $"0x{r.Offset:X}") },
                Width = 100
            });

            // Create viewer panel
            var viewerPanel = new Panel { BackgroundColor = Colors.DarkGray };
            var imageViewer = new ImageView();
            // Wrap image in centered container
            var imageCenterContainer = new TableLayout
            {
                BackgroundColor = Colors.DarkGray,
                Rows =
                {
                    new TableRow { ScaleHeight = true },
                    new TableRow(new TableCell(null, true), new TableCell(imageViewer, false), new TableCell(null, true)),
                    new TableRow { ScaleHeight = true }
                }
            };
            var textViewer = new RichTextArea { ReadOnly = true, Font = new Font("Menlo, Monaco, Consolas, monospace", 12) };

            // Load file list
            var allItems = new List<DatFileItem>();
            for (int i = 0; i < datFile.Entries.Count; i++)
            {
                var entry = datFile.Entries[i];
                allItems.Add(new DatFileItem
                {
                    Index = i,
                    FileName = entry.Path,
                    FileSize = entry.Size,
                    Offset = entry.Offset,
                    Entry = entry
                });
            }
            fileGrid.DataStore = allItems;
            recordLabel.Text = $"Records: {allItems.Count} / {allItems.Count}";

            // Filter handler
            filterBox.TextChanged += (s, e) =>
            {
                var filter = filterBox.Text?.Trim().ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(filter))
                {
                    fileGrid.DataStore = allItems;
                    recordLabel.Text = $"Records: {allItems.Count} / {allItems.Count}";
                }
                else
                {
                    var filtered = allItems.Where(f => f.FileName.ToLowerInvariant().Contains(filter)).ToList();
                    fileGrid.DataStore = filtered;
                    recordLabel.Text = $"Records: {filtered.Count} / {allItems.Count}";
                }
            };

            // Selection handler
            fileGrid.SelectionChanged += (s, e) =>
            {
                var selected = fileGrid.SelectedItem as DatFileItem;
                if (selected == null) return;

                try
                {
                    var data = datFile.ExtractFile(selected.Entry);
                    var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();

                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
                    {
                        using var ms = new MemoryStream(data);
                        imageViewer.Image = new Bitmap(ms);
                        viewerPanel.Content = imageCenterContainer;
                    }
                    else if (ext == ".json" || ext == ".xml" || ext == ".txt" || ext == ".atlas" || ext == ".fnt")
                    {
                        textViewer.Text = Encoding.UTF8.GetString(data);
                        viewerPanel.Content = textViewer;
                    }
                    else
                    {
                        // Show hex
                        var sb = new StringBuilder();
                        int lines = Math.Min(data.Length / 16 + 1, 100);
                        for (int i = 0; i < lines; i++)
                        {
                            int offset = i * 16;
                            sb.Append($"{offset:X8}  ");
                            for (int j = 0; j < 16 && offset + j < data.Length; j++)
                                sb.Append($"{data[offset + j]:X2} ");
                            sb.AppendLine();
                        }
                        textViewer.Text = sb.ToString();
                        viewerPanel.Content = textViewer;
                    }

                    _statusLabel.Text = $"Selected: {selected.FileName} ({selected.SizeText})";
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"Error: {ex.Message}";
                }
            };

            // Context menu for export
            var contextMenu = new ContextMenu();
            var exportItem = new ButtonMenuItem { Text = "Export Selected..." };
            exportItem.Click += (s, e) =>
            {
                using var dialog = new SelectFolderDialog { Title = "Export To" };
                if (dialog.ShowDialog(this) != DialogResult.Ok) return;

                int exported = 0;
                foreach (var row in fileGrid.SelectedRows)
                {
                    var item = (DatFileItem)fileGrid.DataStore.ElementAt(row);
                    try
                    {
                        var data = datFile.ExtractFile(item.Entry);
                        var outputPath = Path.Combine(dialog.Directory, item.FileName);
                        var dir = Path.GetDirectoryName(outputPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllBytes(outputPath, data);
                        exported++;
                    }
                    catch { }
                }
                _statusLabel.Text = $"Exported {exported} files";
            };
            contextMenu.Items.Add(exportItem);
            fileGrid.ContextMenu = contextMenu;

            // Layout - left panel with filter and grid
            var leftPanel = new TableLayout
            {
                Rows =
                {
                    new TableRow(filterToolbar),
                    new TableRow(fileGrid) { ScaleHeight = true }
                }
            };

            var splitter = new Splitter
            {
                Orientation = Orientation.Horizontal,
                Position = 450,
                FixedPanel = SplitterFixedPanel.Panel1,
                Panel1 = leftPanel,
                Panel2 = viewerPanel
            };

            return splitter;
        }

        private void LoadIdxFile(string idxPath)
        {
            try
            {
                _currentPak?.Dispose();
                _currentPak = new PakFile(idxPath);

                _selectedFolder = Path.GetDirectoryName(idxPath);
                _folderLabel.Text = Path.GetFileName(_selectedFolder);

                // Save to settings (這是有效的 client 資料夾)
                _settings.LastFolder = _selectedFolder;
                _settings.LastClientFolder = _selectedFolder;
                _settings.LastIdxFile = Path.GetFileName(idxPath);
                _settings.Save();

                // Update extension filter
                UpdateExtensionFilter();

                // Update file list
                RefreshFileList();

                _statusLabel.Text = $"Loaded: {Path.GetFileName(idxPath)} ({_currentPak.Count} files, {_currentPak.EncryptionType})";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading file: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private void UpdateExtensionFilter()
        {
            var extensions = _currentPak.Files
                .Select(f => Path.GetExtension(f.FileName).ToLowerInvariant())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Distinct()
                .OrderBy(ext => ext)
                .ToList();

            _extFilterDropDown.Items.Clear();
            _extFilterDropDown.Items.Add("All");
            foreach (var ext in extensions)
            {
                _extFilterDropDown.Items.Add(ext);
            }
            _extFilterDropDown.SelectedIndex = 0;
        }

        private void OnSearchChanged(object sender, EventArgs e)
        {
            _currentFilter = _searchBox.Text ?? "";
            RefreshFileList();
        }

        private void OnExtFilterChanged(object sender, EventArgs e)
        {
            _currentExtFilter = _extFilterDropDown.SelectedValue?.ToString() ?? "All";
            RefreshFileList();
        }

        private void OnLangFilterChanged(object sender, EventArgs e)
        {
            var selected = _langFilterDropDown.SelectedValue?.ToString() ?? "All";
            if (selected == "All")
                _currentLangFilter = "All";
            else if (selected.StartsWith("-c"))
                _currentLangFilter = "-c";
            else if (selected.StartsWith("-h"))
                _currentLangFilter = "-h";
            else if (selected.StartsWith("-j"))
                _currentLangFilter = "-j";
            else if (selected.StartsWith("-k"))
                _currentLangFilter = "-k";
            else
                _currentLangFilter = "All";

            RefreshFileList();
        }

        private void OnContentSearch(object sender, EventArgs e)
        {
            var keyword = _contentSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(keyword) || _currentPak == null)
                return;

            _contentSearchKeyword = keyword;
            _contentSearchResults = new HashSet<int>();
            _statusLabel.Text = "Searching content...";

            int found = 0;
            for (int i = 0; i < _currentPak.Count; i++)
            {
                try
                {
                    var file = _currentPak.Files[i];
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                    // Only search text files
                    if (ext == ".txt" || ext == ".html" || ext == ".htm" || ext == ".xml" || ext == ".s" || ext == ".tbl")
                    {
                        var data = _currentPak.Extract(i);
                        var encoding = DetectEncoding(data, file.FileName);
                        var text = encoding.GetString(data);

                        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            _contentSearchResults.Add(i);
                            found++;
                        }
                    }
                }
                catch { }

                if (i % 100 == 0)
                {
                    _statusLabel.Text = $"Searching... {i}/{_currentPak.Count}";
                }
            }

            _statusLabel.Text = $"Content search: found {found} files containing '{keyword}'";
            RefreshFileList();
        }

        private void OnClearContentSearch(object sender, EventArgs e)
        {
            _contentSearchKeyword = "";
            _contentSearchResults = null;
            _contentSearchBox.Text = "";
            _statusLabel.Text = "Content search cleared";
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            if (_currentPak == null) return;

            var items = new List<FileItem>();
            _filteredIndexes = new List<int>();

            for (int i = 0; i < _currentPak.Count; i++)
            {
                var file = _currentPak.Files[i];
                var fileName = file.FileName;
                var lowerName = fileName.ToLowerInvariant();

                // Apply extension filter
                if (_currentExtFilter != "All")
                {
                    var ext = Path.GetExtension(fileName).ToLowerInvariant();
                    if (ext != _currentExtFilter)
                        continue;
                }

                // Apply language filter
                if (_currentLangFilter != "All")
                {
                    bool hasLangSuffix = lowerName.Contains(_currentLangFilter + ".") ||
                                         lowerName.Contains(_currentLangFilter + "_") ||
                                         lowerName.EndsWith(_currentLangFilter);
                    if (!hasLangSuffix)
                        continue;
                }

                // Apply filename search filter
                if (!string.IsNullOrEmpty(_currentFilter))
                {
                    if (!fileName.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // Apply content search filter
                if (_contentSearchResults != null && _contentSearchResults.Count > 0)
                {
                    if (!_contentSearchResults.Contains(i))
                        continue;
                }

                _filteredIndexes.Add(i);
                items.Add(new FileItem
                {
                    Index = i,
                    FileName = fileName,
                    FileSize = file.FileSize,
                    Offset = file.Offset
                });
            }

            _fileGrid.DataStore = items;
            _recordCountLabel.Text = $"Records: {_filteredIndexes.Count} / {_currentPak.Count}";
        }

        private void OnFileSelected(object sender, EventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected == null || _currentPak == null) return;

            try
            {
                var data = _currentPak.Extract(selected.Index);
                var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();

                ShowFilePreview(ext, data, selected.FileName);
                _statusLabel.Text = $"Selected: {selected.FileName} ({selected.SizeText})";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void OnExportSelected(object sender, EventArgs e)
        {
            if (_currentPak == null || _fileGrid.SelectedRows.Count() == 0)
            {
                MessageBox.Show(this, "No files selected", "Export", MessageBoxType.Information);
                return;
            }

            // Export to same folder as idx file
            var outputFolder = _selectedFolder;
            int exported = 0;
            foreach (var row in _fileGrid.SelectedRows)
            {
                var item = (FileItem)_fileGrid.DataStore.ElementAt(row);
                try
                {
                    var data = _currentPak.Extract(item.Index);
                    var outputPath = Path.Combine(outputFolder, item.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exported++;
                }
                catch { }
            }

            _statusLabel.Text = $"Exported {exported} files to {outputFolder}";
        }

        private void OnExportSelectedTo(object sender, EventArgs e)
        {
            if (_currentPak == null || _fileGrid.SelectedRows.Count() == 0)
            {
                MessageBox.Show(this, "No files selected", "Export", MessageBoxType.Information);
                return;
            }

            using var dialog = new SelectFolderDialog { Title = "Select Export Folder" };
            if (dialog.ShowDialog(this) != DialogResult.Ok) return;

            int exported = 0;
            foreach (var row in _fileGrid.SelectedRows)
            {
                var item = (FileItem)_fileGrid.DataStore.ElementAt(row);
                try
                {
                    var data = _currentPak.Extract(item.Index);
                    var outputPath = Path.Combine(dialog.Directory, item.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exported++;
                }
                catch { }
            }

            MessageBox.Show(this, $"Exported {exported} files", "Export Complete", MessageBoxType.Information);
        }

        private void OnCopyFileName(object sender, EventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected == null) return;

            var clipboard = new Clipboard();
            clipboard.Text = selected.FileName;
            _statusLabel.Text = $"Copied: {selected.FileName}";
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            _fileGrid.SelectAll();
        }

        private void OnUnselectAll(object sender, EventArgs e)
        {
            _fileGrid.UnselectAll();
        }

        #region Tab Management

        private void OnFileDoubleClick(object sender, GridCellMouseEventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected != null)
            {
                OpenFileInNewTab(selected);
            }
        }

        private void OnOpenInNewTab(object sender, EventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected != null)
            {
                OpenFileInNewTab(selected);
            }
        }

        private void OpenFileInNewTab(FileItem item)
        {
            if (_currentPak == null) return;

            var tabKey = $"{_currentPak.IdxPath}:{item.Index}";

            // Check if already open
            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var data = _currentPak.Extract(item.Index);
                var ext = Path.GetExtension(item.FileName).ToLowerInvariant();

                // Create tab content based on file type
                Control content = CreateTabContent(ext, data, item.FileName);
                if (content == null) return;

                // Create tab page
                var docPage = new TabPage
                {
                    Text = item.FileName,
                    Content = content
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                _statusLabel.Text = $"Opened: {item.FileName} in new tab";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error opening file: {ex.Message}";
            }
        }

        private Control CreateTabContent(string ext, byte[] data, string fileName)
        {
            // 使用模組化的 ViewerFactory
            var viewer = Viewers.ViewerFactory.CreateViewerSmart(ext, data);
            viewer.LoadData(data, fileName);

            // 如果 viewer 支援搜尋，加上搜尋工具列
            if (viewer.CanSearch)
            {
                var toolbar = viewer.GetSearchToolbar();
                if (toolbar != null)
                {
                    return new TableLayout
                    {
                        Rows =
                        {
                            new TableRow(toolbar),
                            new TableRow(viewer.GetControl()) { ScaleHeight = true }
                        }
                    };
                }
            }

            return viewer.GetControl();
        }

        private Control CreateSpriteTabContent(byte[] data)
        {
            var frames = SprReader.Load(data);
            if (frames == null || frames.Length == 0) return null;

            var drawable = new Drawable { BackgroundColor = Colors.DarkRed };
            int frameIndex = 0;

            var timer = new UITimer { Interval = 0.15 };
            timer.Elapsed += (s, e) =>
            {
                frameIndex = (frameIndex + 1) % frames.Length;
                drawable.Invalidate();
            };

            drawable.Paint += (s, e) =>
            {
                var frame = frames[frameIndex];
                if (frame.Image == null) return;

                using var ms = new MemoryStream();
                frame.Image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using var bitmap = new Bitmap(ms);

                var scale = 2.0f;
                var x = (drawable.Width - frame.Width * scale) / 2;
                var y = (drawable.Height - frame.Height * scale) / 2;
                e.Graphics.DrawImage(bitmap, (float)x, (float)y, frame.Width * scale, frame.Height * scale);

                var info = $"Frame {frameIndex + 1}/{frames.Length}  Size: {frame.Width}x{frame.Height}";
                e.Graphics.DrawText(new Font(SystemFont.Default), Colors.White, 10, 10, info);
            };

            if (frames.Length > 1)
                timer.Start();

            return drawable;
        }

        private Control CreateImageTabContent(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                var imageView = new ImageView { Image = new Bitmap(ms) };
                return imageView;
            }
            catch
            {
                return CreateHexTabContent(data);
            }
        }

        private Control CreateTextTabContent(byte[] data, string fileName)
        {
            var encoding = DetectEncoding(data, fileName);
            var text = encoding.GetString(data);

            var searchBox = new TextBox { PlaceholderText = "Search...", Width = 200 };
            var searchBtn = new Button { Text = "Find" };
            var textArea = new RichTextArea
            {
                ReadOnly = true,
                Text = text,
                Font = new Font("Menlo, Monaco, Consolas, monospace", 12)
            };

            searchBtn.Click += (s, e) =>
            {
                var keyword = searchBox.Text?.Trim();
                if (string.IsNullOrEmpty(keyword)) return;

                var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    textArea.Selection = new Range<int>(idx, idx + keyword.Length);
                    textArea.Focus();
                }
            };

            var toolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                Items = { new Label { Text = "Find:" }, searchBox, searchBtn }
            };

            return new TableLayout
            {
                Rows =
                {
                    new TableRow(toolbar),
                    new TableRow(textArea) { ScaleHeight = true }
                }
            };
        }

        private Control CreateHexTabContent(byte[] data)
        {
            var sb = new StringBuilder();
            int bytesPerLine = 16;
            int lines = Math.Min(data.Length / bytesPerLine + 1, 1000);

            for (int i = 0; i < lines; i++)
            {
                int offset = i * bytesPerLine;
                sb.Append($"{offset:X8}  ");

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (offset + j < data.Length)
                        sb.Append($"{data[offset + j]:X2} ");
                    else
                        sb.Append("   ");
                    if (j == 7) sb.Append(" ");
                }

                sb.Append(" |");
                for (int j = 0; j < bytesPerLine && offset + j < data.Length; j++)
                {
                    byte b = data[offset + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine("|");
            }

            if (data.Length > lines * bytesPerLine)
                sb.AppendLine($"... ({data.Length - lines * bytesPerLine} more bytes)");

            return new RichTextArea
            {
                ReadOnly = true,
                Text = sb.ToString(),
                Font = new Font("Menlo, Monaco, Consolas, monospace", 12)
            };
        }

        private void CloseTab(TabPage page)
        {
            if (page == _browserPage) return; // Don't close browser page

            var key = page.Tag as string;
            if (key != null && _openTabs.ContainsKey(key))
            {
                _openTabs.Remove(key);
            }
            _mainTabControl.Pages.Remove(page);
        }

        private void CloseOtherTabs(TabPage keepPage)
        {
            var toClose = _mainTabControl.Pages.Where(p => p != _browserPage && p != keepPage).ToList();
            foreach (var page in toClose)
            {
                CloseTab(page);
            }
        }

        private void CloseAllTabs()
        {
            var toClose = _mainTabControl.Pages.Where(p => p != _browserPage).ToList();
            foreach (var page in toClose)
            {
                CloseTab(page);
            }
        }

        #endregion

        #region Text Search

        // 文字搜尋功能已整合到 TextViewer 類別中
        // 這裡的搜尋列保留供未來擴充使用

        private void OnTextSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter)
            {
                _textSearchResultLabel.Text = "Use viewer's built-in search";
                e.Handled = true;
            }
        }

        private void OnTextSearchNext(object sender, EventArgs e)
        {
            _textSearchResultLabel.Text = "Use viewer's built-in search";
        }

        private void OnTextSearchPrev(object sender, EventArgs e)
        {
            _textSearchResultLabel.Text = "Use viewer's built-in search";
        }

        #endregion

        private void OnExportAll(object sender, EventArgs e)
        {
            if (_currentPak == null)
            {
                MessageBox.Show(this, "No PAK file loaded", "Export", MessageBoxType.Information);
                return;
            }

            using var dialog = new SelectFolderDialog { Title = "Select Export Folder" };
            if (dialog.ShowDialog(this) != DialogResult.Ok) return;

            int exported = 0;
            for (int i = 0; i < _currentPak.Count; i++)
            {
                try
                {
                    var file = _currentPak.Files[i];
                    var data = _currentPak.Extract(i);
                    var outputPath = Path.Combine(dialog.Directory, file.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exported++;
                }
                catch { }

                if (i % 100 == 0)
                {
                    _statusLabel.Text = $"Exporting... {i}/{_currentPak.Count}";
                }
            }

            _statusLabel.Text = $"Export complete: {exported} files";
            MessageBox.Show(this, $"Exported {exported} files", "Export Complete", MessageBoxType.Information);
        }

        #endregion

        #region SPR List Mode

        private void OnOpenSprList(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open SPR List File (list.spr / wlist.spr)",
                Filters = { new FileFilter("SPR List Files", ".spr", ".txt"), new FileFilter("All Files", ".*") }
            };

            if (!string.IsNullOrEmpty(_selectedFolder))
                dialog.Directory = new Uri(_selectedFolder);

            if (dialog.ShowDialog(this) != DialogResult.Ok)
                return;

            try
            {
                _sprListFile = Lin.Helper.Core.Sprite.SprListParser.LoadFromFile(dialog.FileName);
                _filteredSprListEntries = _sprListFile.Entries;
                _isSprListMode = true;
                _sprListModeCheck.Checked = true;

                // Load sprite*.idx files for sprite data
                if (!string.IsNullOrEmpty(_selectedFolder))
                {
                    LoadSpriteIdxFiles(_selectedFolder);
                }

                UpdateSprListDisplay();
                _statusLabel.Text = $"Loaded SPR List: {Path.GetFileName(dialog.FileName)} ({_sprListFile.Entries.Count} entries)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading SPR List: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private void OnSprListModeChanged(object sender, EventArgs e)
        {
            _isSprListMode = _sprListModeCheck.Checked ?? false;

            if (_isSprListMode && _sprListFile == null)
            {
                // Prompt to load SPR List file
                OnOpenSprList(sender, e);
                if (_sprListFile == null)
                {
                    _sprListModeCheck.Checked = false;
                    _isSprListMode = false;
                }
            }

            UpdateModeDisplay();
        }

        private void OnSprTypeFilterChanged(object sender, EventArgs e)
        {
            var selected = _sprTypeFilterDropDown.SelectedValue?.ToString() ?? "All Types";
            if (selected == "All Types")
            {
                _sprListTypeFilter = null;
            }
            else
            {
                // Parse type ID from string like "10 - 怪物"
                var parts = selected.Split('-');
                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int typeId))
                {
                    _sprListTypeFilter = typeId;
                }
            }

            UpdateSprListDisplay();
        }

        private void UpdateModeDisplay()
        {
            // Swap grid content
            _leftListPanel.Content = _isSprListMode ? _sprListGrid : _fileGrid;

            // Toggle visibility of mode-specific filter controls
            _sprTypeFilterDropDown.Visible = _isSprListMode;

            if (_isSprListMode)
            {
                UpdateSprListDisplay();
            }
            else
            {
                RefreshFileList();
            }
        }

        private void LoadSpriteIdxFiles(string folder)
        {
            _spritePakFiles = new Dictionary<int, PakFile>();

            // Find all sprite*.idx files
            var spriteIdxFiles = Directory.GetFiles(folder, "sprite*.idx", SearchOption.TopDirectoryOnly);
            foreach (var idxFile in spriteIdxFiles)
            {
                try
                {
                    var pak = new PakFile(idxFile);
                    // Map sprite IDs to this pak file
                    foreach (var file in pak.Files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file.FileName);
                        if (int.TryParse(name, out int spriteId))
                        {
                            if (!_spritePakFiles.ContainsKey(spriteId))
                            {
                                _spritePakFiles[spriteId] = pak;
                            }
                        }
                    }
                }
                catch { }
            }

            _statusLabel.Text = $"Loaded {spriteIdxFiles.Length} sprite.idx files";
        }

        private void UpdateSprListDisplay()
        {
            if (_sprListFile == null) return;

            var filter = _currentFilter?.ToLowerInvariant() ?? "";
            var items = new List<SprListItem>();

            foreach (var entry in _sprListFile.Entries)
            {
                // Apply type filter
                if (_sprListTypeFilter.HasValue && entry.TypeId != _sprListTypeFilter)
                    continue;

                // Apply search filter
                if (!string.IsNullOrEmpty(filter))
                {
                    if (!entry.Name.ToLowerInvariant().Contains(filter) &&
                        !entry.Id.ToString().Contains(filter))
                        continue;
                }

                items.Add(new SprListItem
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    SpriteId = entry.SpriteId,
                    ImageCount = entry.ImageCount,
                    TypeId = entry.TypeId,
                    ActionCount = entry.Actions.Count,
                    Entry = entry
                });
            }

            _filteredSprListEntries = items.Select(i => i.Entry).ToList();
            _sprListGrid.DataStore = items;
            _recordCountLabel.Text = $"Records: {items.Count} / {_sprListFile.Entries.Count}";
        }

        private void OnSprListSelected(object sender, EventArgs e)
        {
            var selected = _sprListGrid.SelectedItem as SprListItem;
            if (selected == null) return;

            // Try to load and display the sprite
            try
            {
                if (_spritePakFiles != null && _spritePakFiles.TryGetValue(selected.SpriteId, out var pak))
                {
                    var fileName = $"{selected.SpriteId}.spr";
                    var fileIndex = pak.Files.ToList().FindIndex(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (fileIndex >= 0)
                    {
                        var data = pak.Extract(fileIndex);
                        ShowFilePreview(".spr", data, fileName);
                    }
                }

                // Show entry info in status
                var entry = selected.Entry;
                _statusLabel.Text = $"#{entry.Id} {entry.Name} - {entry.TypeName} ({entry.Actions.Count} actions, {entry.ImageCount} images)";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error loading sprite: {ex.Message}";
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _currentViewer?.Dispose();
            _currentPak?.Dispose();
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
        public int Offset { get; set; }

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

        public string TypeName => Entry?.TypeName ?? "未知";
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
}
