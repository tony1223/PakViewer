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
    public partial class MainForm
    {
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
                if (idxFiles.Length > 1)
                    _idxDropDown.Items.Add(I18n.T("Filter.All"));  // Add "All" option when multiple IDX files
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
            var fileMenu = new SubMenuItem { Text = I18n.T("Menu.File") };

            var openClientFolderCmd = new Command { MenuText = I18n.T("Menu.File.OpenClientFolder"), Shortcut = Keys.Control | Keys.O };
            openClientFolderCmd.Executed += OnOpenFolder;
            fileMenu.Items.Add(openClientFolderCmd);

            var openIdxCmd = new Command { MenuText = I18n.T("Menu.File.OpenIdx"), Shortcut = Keys.Control | Keys.I };
            openIdxCmd.Executed += OnOpenIdxFile;
            fileMenu.Items.Add(openIdxCmd);

            var openDatCmd = new Command { MenuText = I18n.T("Menu.File.OpenDat"), Shortcut = Keys.Control | Keys.M };
            openDatCmd.Executed += OnOpenDatFile;
            fileMenu.Items.Add(openDatCmd);

            fileMenu.Items.Add(new SeparatorMenuItem());

            var openFileFolderCmd = new Command { MenuText = I18n.T("Menu.File.OpenFileFolder"), Shortcut = Keys.Control | Keys.Shift | Keys.O };
            openFileFolderCmd.Executed += OnOpenFileFolder;
            fileMenu.Items.Add(openFileFolderCmd);

            var openFileCmd = new Command { MenuText = I18n.T("Menu.File.OpenFile"), Shortcut = Keys.Control | Keys.F };
            openFileCmd.Executed += OnOpenFile;
            fileMenu.Items.Add(openFileCmd);

            fileMenu.Items.Add(new SeparatorMenuItem());

            var quitCmd = new Command { MenuText = I18n.T("Menu.File.Quit"), Shortcut = Keys.Control | Keys.Q };
            quitCmd.Executed += (s, e) => Application.Instance.Quit();
            fileMenu.Items.Add(quitCmd);

            menu.Items.Add(fileMenu);

            // Tools menu
            var toolsMenu = new SubMenuItem { Text = I18n.T("Menu.Tools") };

            var exportCmd = new Command { MenuText = I18n.T("Menu.Tools.ExportSelected") };
            exportCmd.Executed += OnExportSelected;
            toolsMenu.Items.Add(exportCmd);

            var exportAllCmd = new Command { MenuText = I18n.T("Menu.Tools.ExportAll") };
            exportAllCmd.Executed += OnExportAll;
            toolsMenu.Items.Add(exportAllCmd);

            toolsMenu.Items.Add(new SeparatorMenuItem());

            var deleteCmd = new Command { MenuText = I18n.T("Menu.Tools.DeleteSelected") };
            deleteCmd.Executed += OnDeleteSelected;
            toolsMenu.Items.Add(deleteCmd);

            menu.Items.Add(toolsMenu);

            // Language menu (top-level)
            var languageMenu = new SubMenuItem { Text = I18n.T("Menu.Language") };
            foreach (var lang in I18n.AvailableLanguages)
            {
                var langItem = new RadioMenuItem { Text = I18n.LanguageNames[lang] };
                langItem.Checked = (lang == I18n.CurrentLanguage);
                var langCode = lang;  // Capture for closure
                langItem.Click += (s, e) => OnLanguageChanged(langCode);
                languageMenu.Items.Add(langItem);
            }
            menu.Items.Add(languageMenu);

            Menu = menu;
        }

        private void OnLanguageChanged(string langCode)
        {
            I18n.SetLanguage(langCode);

            // Save language preference
            _settings.Language = langCode;
            _settings.Save();

            // Recreate menu and refresh UI
            CreateMenu();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            // Update window title
            Title = I18n.T("AppTitle");

            // Update status
            _statusLabel.Text = I18n.T("Status.Ready");

            // Update browser tab title
            if (_browserPage != null)
                _browserPage.Text = I18n.T("Tab.LinClient");

            // Refresh record count
            if (_filteredIndexes != null)
                _recordCountLabel.Text = I18n.T("Status.Records", _filteredIndexes.Count, _currentPak?.Files.Count ?? 0);

            // Update buttons
            if (_openFolderBtn != null)
                _openFolderBtn.Text = I18n.T("Button.Open");
            if (_contentSearchBtn != null)
                _contentSearchBtn.Text = I18n.T("Button.Search");
            if (_clearSearchBtn != null)
                _clearSearchBtn.Text = I18n.T("Button.Clear");
            if (_textSearchNextBtn != null)
                _textSearchNextBtn.Text = I18n.T("Button.Next");
            if (_textSearchPrevBtn != null)
                _textSearchPrevBtn.Text = I18n.T("Button.Prev");

            // Update search box placeholder
            if (_searchBox != null)
                _searchBox.PlaceholderText = I18n.T("Label.Search");
            if (_contentSearchBox != null)
                _contentSearchBox.PlaceholderText = I18n.T("Label.Search");

            // Update mode radio items
            if (_viewModeRadio != null && _viewModeRadio.Items.Count >= 3)
            {
                _viewModeRadio.Items[MODE_NORMAL].Text = I18n.T("Mode.Normal");
                _viewModeRadio.Items[MODE_SPR].Text = I18n.T("Mode.SPR");
                _viewModeRadio.Items[MODE_SPR_LIST].Text = I18n.T("Mode.SPRList");
            }

            // Update SPR type filter dropdown
            if (_sprTypeFilterDropDown != null && _sprTypeFilterDropDown.Items.Count > 0)
            {
                var selectedIdx = _sprTypeFilterDropDown.SelectedIndex;
                _sprTypeFilterDropDown.Items.Clear();
                _sprTypeFilterDropDown.Items.Add(I18n.T("Filter.AllTypes"));
                _sprTypeFilterDropDown.Items.Add(I18n.T("Filter.Unreferenced"));
                _sprTypeFilterDropDown.Items.Add($"0 - {I18n.T("SprType.0")}");
                _sprTypeFilterDropDown.Items.Add($"1 - {I18n.T("SprType.1")}");
                _sprTypeFilterDropDown.Items.Add($"5 - {I18n.T("SprType.5")}");
                _sprTypeFilterDropDown.Items.Add($"6 - {I18n.T("SprType.6")}");
                _sprTypeFilterDropDown.Items.Add($"7 - {I18n.T("SprType.7")}");
                _sprTypeFilterDropDown.Items.Add($"8 - {I18n.T("SprType.8")}");
                _sprTypeFilterDropDown.Items.Add($"9 - {I18n.T("SprType.9")}");
                _sprTypeFilterDropDown.Items.Add($"10 - {I18n.T("SprType.10")}");
                _sprTypeFilterDropDown.Items.Add($"11 - {I18n.T("SprType.11")}");
                _sprTypeFilterDropDown.Items.Add($"12 - {I18n.T("SprType.12")}");
                if (selectedIdx >= 0 && selectedIdx < _sprTypeFilterDropDown.Items.Count)
                    _sprTypeFilterDropDown.SelectedIndex = selectedIdx;
            }

            // Update right panel mode radios
            if (_previewModeRadio != null)
                _previewModeRadio.Text = I18n.T("RightPanel.Preview");
            if (_galleryModeRadio != null)
                _galleryModeRadio.Text = I18n.T("RightPanel.Gallery");
        }

        private void CreateLayout()
        {
            // Create main tab control
            _mainTabControl = new TabControl();

            // Tab right-click menu
            _mainTabControl.MouseDown += OnTabMouseDown;

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
                Text = I18n.T("Tab.LinClient"),
                Content = mainSplitter
            };
            _mainTabControl.Pages.Add(_browserPage);

            // Status bar
            _statusLabel = new Label { Text = I18n.T("Status.Ready") };
            _recordCountLabel = new Label { Text = I18n.T("Status.Records", 0, 0) };

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
            _openFolderBtn = new Button { Text = I18n.T("Button.Open"), Width = 70 };
            _openFolderBtn.Click += OnOpenFolder;

            // IDX dropdown
            _idxDropDown = new DropDown();
            _idxDropDown.SelectedIndexChanged += OnIdxChanged;

            // 模式選擇 Radio (一般/SPR/SPR List)
            _viewModeRadio = new RadioButtonList
            {
                Orientation = Orientation.Horizontal,
                Spacing = new Size(15, 0)
            };
            _viewModeRadio.Items.Add(I18n.T("Mode.Normal"));
            _viewModeRadio.Items.Add(I18n.T("Mode.SPR"));
            _viewModeRadio.Items.Add(I18n.T("Mode.SPRList"));
            _viewModeRadio.SelectedIndex = MODE_NORMAL;
            _viewModeRadio.SelectedIndexChanged += OnViewModeChanged;

            // list.spr 路徑選擇器
            _sprModeListSprCheck = new CheckBox { Text = "", Enabled = false };
            _sprModeListSprLabel = new Label { Text = I18n.T("SprMode.NotSet"), TextColor = Eto.Drawing.Colors.Gray };
            var sprModeListSprBtn = new Button { Text = "...", Width = 30 };
            sprModeListSprBtn.Click += OnSprModeListSprBrowse;

            // 載入已儲存的 list.spr 路徑
            if (!string.IsNullOrEmpty(_settings.SprModeListSprPath) && File.Exists(_settings.SprModeListSprPath))
            {
                _sprModeListSprCheck.Checked = true;
                _sprModeListSprLabel.Text = Path.GetFileName(_settings.SprModeListSprPath);
                _sprModeListSprLabel.TextColor = Eto.Drawing.Colors.Black;
                _sprModeListSprLabel.ToolTip = _settings.SprModeListSprPath;
            }

            // Search box (filename)
            _searchBox = new TextBox { PlaceholderText = I18n.T("Label.Search") };
            _searchBox.TextChanged += OnSearchChanged;

            // Extension filter
            _extFilterDropDown = new DropDown();
            _extFilterDropDown.Items.Add(I18n.T("Filter.All"));
            _extFilterDropDown.SelectedIndex = 0;
            _extFilterDropDown.SelectedIndexChanged += OnExtFilterChanged;

            // Language filter
            _langFilterDropDown = new DropDown();
            _langFilterDropDown.Items.Add(I18n.T("Filter.All"));
            _langFilterDropDown.Items.Add("-c (繁中)");
            _langFilterDropDown.Items.Add("-h (港)");
            _langFilterDropDown.Items.Add("-j (日)");
            _langFilterDropDown.Items.Add("-k (韓)");
            _langFilterDropDown.SelectedIndex = 0;
            _langFilterDropDown.SelectedIndexChanged += OnLangFilterChanged;

            // SPR Type filter (for SPR mode with list.spr)
            _sprTypeFilterDropDown = new DropDown { Visible = false };
            _sprTypeFilterDropDown.Items.Add(I18n.T("Filter.AllTypes"));
            _sprTypeFilterDropDown.Items.Add(I18n.T("Filter.Unreferenced"));
            _sprTypeFilterDropDown.Items.Add($"0 - {I18n.T("SprType.0")}");
            _sprTypeFilterDropDown.Items.Add($"1 - {I18n.T("SprType.1")}");
            _sprTypeFilterDropDown.Items.Add($"5 - {I18n.T("SprType.5")}");
            _sprTypeFilterDropDown.Items.Add($"6 - {I18n.T("SprType.6")}");
            _sprTypeFilterDropDown.Items.Add($"7 - {I18n.T("SprType.7")}");
            _sprTypeFilterDropDown.Items.Add($"8 - {I18n.T("SprType.8")}");
            _sprTypeFilterDropDown.Items.Add($"9 - {I18n.T("SprType.9")}");
            _sprTypeFilterDropDown.Items.Add($"10 - {I18n.T("SprType.10")}");
            _sprTypeFilterDropDown.Items.Add($"11 - {I18n.T("SprType.11")}");
            _sprTypeFilterDropDown.Items.Add($"12 - {I18n.T("SprType.12")}");
            _sprTypeFilterDropDown.SelectedIndex = 0;
            _sprTypeFilterDropDown.SelectedIndexChanged += OnSprTypeFilterChanged;

            // Content search
            _contentSearchBox = new TextBox { PlaceholderText = I18n.T("Label.Search") };
            _contentSearchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    OnContentSearch(s, e);
                    e.Handled = true;
                }
            };
            _contentSearchBtn = new Button { Text = I18n.T("Button.Search") };
            _contentSearchBtn.Click += OnContentSearch;
            _clearSearchBtn = new Button { Text = I18n.T("Button.Clear") };
            _clearSearchBtn.Click += OnClearContentSearch;

            // File grid (normal mode)
            _fileGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.No"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.Index.ToString()) },
                Width = 60,
                Sortable = true,
                ID = "Index"
            });

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.FileName"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.FileName) },
                Width = 180,
                Sortable = true,
                ID = "FileName"
            });

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Size"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.SizeText) },
                Width = 80,
                Sortable = true,
                ID = "Size"
            });

            _fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.IDX"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.IdxName ?? "") },
                Width = 100,
                Sortable = true,
                ID = "IdxName"
            });

            _fileGrid.ColumnHeaderClick += OnFileGridColumnHeaderClick;
            _fileGrid.SelectionChanged += OnFileSelected;
            _fileGrid.CellDoubleClick += OnFileDoubleClick;

            // Context menu for file grid
            var fileContextMenu = new ContextMenu();

            var openInTabMenuItem = new ButtonMenuItem { Text = I18n.T("Context.OpenInNewTab") };
            openInTabMenuItem.Click += OnOpenInNewTab;
            fileContextMenu.Items.Add(openInTabMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var exportMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelected") };
            exportMenuItem.Click += OnExportSelected;
            fileContextMenu.Items.Add(exportMenuItem);

            var exportToMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
            exportToMenuItem.Click += OnExportSelectedTo;
            fileContextMenu.Items.Add(exportToMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var deleteMenuItem = new ButtonMenuItem { Text = I18n.T("Context.DeleteSelected") };
            deleteMenuItem.Click += OnDeleteSelected;
            fileContextMenu.Items.Add(deleteMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var copyFileNameMenuItem = new ButtonMenuItem { Text = I18n.T("Context.CopyFilename") };
            copyFileNameMenuItem.Click += OnCopyFileName;
            fileContextMenu.Items.Add(copyFileNameMenuItem);

            fileContextMenu.Items.Add(new SeparatorMenuItem());

            var selectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.SelectAll") };
            selectAllMenuItem.Click += OnSelectAll;
            fileContextMenu.Items.Add(selectAllMenuItem);

            var unselectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.UnselectAll") };
            unselectAllMenuItem.Click += OnUnselectAll;
            fileContextMenu.Items.Add(unselectAllMenuItem);

            _fileGrid.ContextMenu = fileContextMenu;

            // SPR List grid (SPR List mode)
            _sprListGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            // 勾選欄位
            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = "",
                DataCell = new CheckBoxCell { Binding = Binding.Property<SprListItem, bool?>(r => r.IsChecked) },
                Width = 30,
                Editable = true
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.ID"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.Id.ToString()) },
                Width = 50
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Name"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.Name) },
                Width = 120
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.SpriteId"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.SpriteId.ToString()) },
                Width = 50
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.ImageCount"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.ImageCount.ToString()) },
                Width = 40
            });

            _sprListGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Type"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprListItem, string>(r => r.TypeName) },
                Width = 80
            });

            _sprListGrid.SelectionChanged += OnSprListSelected;

            // SPR List 右鍵選單 (不包含刪除功能)
            var sprListContextMenu = new ContextMenu();
            var sprListSelectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.SelectAll") };
            sprListSelectAllMenuItem.Click += OnSprListSelectAll;
            var sprListUnselectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.UnselectAll") };
            sprListUnselectAllMenuItem.Click += OnSprListUnselectAll;
            var sprListSaveMenuItem = new ButtonMenuItem { Text = I18n.T("Context.SaveAs") };
            sprListSaveMenuItem.Click += OnSprListSaveAs;

            sprListContextMenu.Items.Add(sprListSelectAllMenuItem);
            sprListContextMenu.Items.Add(sprListUnselectAllMenuItem);
            sprListContextMenu.Items.Add(new SeparatorMenuItem());
            sprListContextMenu.Items.Add(sprListSaveMenuItem);

            _sprListGrid.ContextMenu = sprListContextMenu;

            // SPR Group Grid (新增 - SPR 模式用)
            _sprGroupGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            // 勾選欄
            _sprGroupGrid.Columns.Add(new GridColumn
            {
                HeaderText = "",
                DataCell = new CheckBoxCell { Binding = Binding.Property<SprGroupItem, bool?>(r => r.IsChecked) },
                Width = 30,
                Editable = true
            });

            _sprGroupGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.ID"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprGroupItem, string>(r => r.Id.ToString()) },
                Width = 60
            });

            _sprGroupGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Parts"),
                DataCell = new TextBoxCell { Binding = Binding.Property<SprGroupItem, string>(r => r.Parts.ToString()) },
                Width = 50
            });

            _sprGroupGrid.SelectionChanged += OnSprGroupSelected;

            // SPR Group 右鍵選單
            var sprGroupContextMenu = new ContextMenu();
            var sprGroupSelectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.SelectAll") };
            sprGroupSelectAllMenuItem.Click += OnSprGroupSelectAll;
            var sprGroupUnselectAllMenuItem = new ButtonMenuItem { Text = I18n.T("Context.UnselectAll") };
            sprGroupUnselectAllMenuItem.Click += OnSprGroupUnselectAll;
            var sprGroupDeleteMenuItem = new ButtonMenuItem { Text = I18n.T("Context.Delete") };
            sprGroupDeleteMenuItem.Click += OnSprGroupDelete;
            var sprGroupExportMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelected") };
            sprGroupExportMenuItem.Click += OnSprGroupExport;
            var sprGroupExportToMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
            sprGroupExportToMenuItem.Click += OnSprGroupExportTo;

            sprGroupContextMenu.Items.Add(sprGroupExportMenuItem);
            sprGroupContextMenu.Items.Add(sprGroupExportToMenuItem);
            sprGroupContextMenu.Items.Add(new SeparatorMenuItem());
            sprGroupContextMenu.Items.Add(sprGroupSelectAllMenuItem);
            sprGroupContextMenu.Items.Add(sprGroupUnselectAllMenuItem);
            sprGroupContextMenu.Items.Add(new SeparatorMenuItem());
            sprGroupContextMenu.Items.Add(sprGroupDeleteMenuItem);

            _sprGroupGrid.ContextMenu = sprGroupContextMenu;

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
                        new TableCell(new Label { Text = I18n.T("Label.Folder"), Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Items = { _folderLabel, null, _openFolderBtn }
                        }, true)
                    ),
                    // IDX row
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.IDX"), Width = labelWidth }, false),
                        new TableCell(_idxDropDown, true)
                    ),
                    // 模式選擇 (一般/SPR/SPR List)
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Mode"), Width = labelWidth }, false),
                        new TableCell(_viewModeRadio, true)
                    ),
                    // list.spr 路徑
                    new TableRow(
                        new TableCell(new Label { Text = "list.spr:", Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Items = { _sprModeListSprCheck, _sprModeListSprLabel, null, sprModeListSprBtn }
                        }, true)
                    ),
                    // Ext row
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Ext"), Width = labelWidth }, false),
                        new TableCell(_extFilterDropDown, true)
                    ),
                    // Lang row
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Lang"), Width = labelWidth }, false),
                        new TableCell(_langFilterDropDown, true)
                    ),
                    // Type row (SPR List 模式專用)
                    new TableRow(
                        new TableCell(_sprTypeLabel = new Label { Text = I18n.T("Label.Type"), Width = labelWidth, Visible = false }, false),
                        new TableCell(_sprTypeFilterDropDown, true)
                    ),
                    // Search row
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Filter"), Width = labelWidth }, false),
                        new TableCell(_searchBox, true)
                    ),
                    // Content search row
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Search"), Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 2,
                            Items = { new StackLayoutItem(_contentSearchBox, true), _contentSearchBtn, _clearSearchBtn }
                        }, true)
                    )
                }
            };

            _leftListPanel = new Panel { Content = _fileGrid };

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
            // 模式切換工具列
            _previewModeRadio = new RadioButton { Text = I18n.T("RightPanel.Preview") };
            _galleryModeRadio = new RadioButton(_previewModeRadio) { Text = I18n.T("RightPanel.Gallery") };
            _previewModeRadio.Checked = true;

            _previewModeRadio.CheckedChanged += (s, e) =>
            {
                if (_previewModeRadio.Checked) SetRightPanelMode(false);
            };
            _galleryModeRadio.CheckedChanged += (s, e) =>
            {
                if (_galleryModeRadio.Checked) SetRightPanelMode(true);
            };

            _rightThumbnailSlider = new Slider
            {
                MinValue = 48,
                MaxValue = 200,
                Value = 80,
                Width = 100
            };
            _rightThumbnailSlider.ValueChanged += (s, e) =>
            {
                _cachedThumbnailSize = _rightThumbnailSlider.Value;  // 快取值供背景執行緒使用
                if (_rightGalleryPanel != null)
                    _rightGalleryPanel.ThumbnailSize = _rightThumbnailSlider.Value;
            };

            // 匯出動作矩陣按鈕 (只在 SPR List 模式顯示)
            _exportPivotTableBtn = new Button { Text = I18n.T("Button.ExportPivotTable"), Visible = false };
            _exportPivotTableBtn.Click += OnExportPivotTable;

            var modeToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    _previewModeRadio,
                    _galleryModeRadio,
                    new Label { Text = "|", VerticalAlignment = VerticalAlignment.Center },
                    new Label { Text = I18n.T("RightPanel.ThumbnailSize"), VerticalAlignment = VerticalAlignment.Center },
                    _rightThumbnailSlider,
                    _exportPivotTableBtn
                }
            };

            // Search toolbar for text viewer
            _textSearchBox = new TextBox { PlaceholderText = I18n.T("Placeholder.SearchInText"), Width = 200 };
            _textSearchBox.KeyDown += OnTextSearchKeyDown;

            _textSearchPrevBtn = new Button { Text = "◀", Width = 30 };
            _textSearchPrevBtn.Click += OnTextSearchPrev;

            _textSearchNextBtn = new Button { Text = "▶", Width = 30 };
            _textSearchNextBtn.Click += OnTextSearchNext;

            _textSearchResultLabel = new Label { Text = "" };

            _defaultSearchToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    new Label { Text = I18n.T("Label.Find") },
                    _textSearchBox,
                    _textSearchPrevBtn,
                    _textSearchNextBtn,
                    _textSearchResultLabel
                }
            };

            // 搜尋工具列容器 - 可動態替換為 viewer 自帶的搜尋工具列
            _searchToolbarContainer = new Panel { Content = _defaultSearchToolbar };

            _viewerPanel = new Panel { BackgroundColor = Colors.DarkGray };

            // 預覽模式的完整容器 (含搜尋工具列)
            var previewContainer = new TableLayout
            {
                Rows =
                {
                    new TableRow(_searchToolbarContainer),
                    new TableRow(_viewerPanel) { ScaleHeight = true }
                }
            };

            // 右側容器 (可切換預覽/相簿)
            _rightPanelContainer = new Panel { Content = previewContainer };

            // 整體佈局
            return new TableLayout
            {
                Rows =
                {
                    new TableRow(modeToolbar),
                    new TableRow(_rightPanelContainer) { ScaleHeight = true }
                }
            };
        }

        private void SetRightPanelMode(bool isGallery)
        {
            if (isGallery)
            {
                // 切換到相簿模式
                if (_rightGalleryPanel == null)
                {
                    _rightGalleryPanel = new Controls.GalleryPanel();
                    _rightGalleryPanel.ThumbnailSize = _rightThumbnailSlider.Value;
                    _rightGalleryPanel.ThumbnailLoader = LoadThumbnailForRightGallery;
                    _rightGalleryPanel.ItemSelected += OnRightGalleryItemSelected;
                    _rightGalleryPanel.ItemDoubleClicked += OnRightGalleryItemDoubleClicked;
                    _rightGalleryPanel.ItemRightClicked += OnRightGalleryItemRightClicked;
                }
                RefreshRightGallery();
                _rightPanelContainer.Content = _rightGalleryPanel;
            }
            else
            {
                // 切換回預覽模式 - 使用現有的 _searchToolbarContainer
                var previewContainer = new TableLayout
                {
                    Rows =
                    {
                        new TableRow(_searchToolbarContainer),
                        new TableRow(_viewerPanel) { ScaleHeight = true }
                    }
                };

                _rightPanelContainer.Content = previewContainer;
            }
        }

        private void RefreshRightGallery()
        {
            if (_rightGalleryPanel == null) return;

            var mode = _viewModeRadio?.SelectedIndex ?? MODE_NORMAL;

            switch (mode)
            {
                case MODE_SPR:
                    RefreshRightGalleryForSprMode();
                    break;
                case MODE_SPR_LIST:
                    RefreshRightGalleryForSprListMode();
                    break;
                default:
                    RefreshRightGalleryForNormalMode();
                    break;
            }
        }

        private void RefreshRightGalleryForNormalMode()
        {
            var imageExtensions = new HashSet<string> { ".spr", ".tbt", ".img", ".png", ".til", ".gif" };

            // 從 _fileGrid 的 DataStore 取得已過濾的項目 (包含正確的 SourcePak)
            var fileItems = _fileGrid.DataStore as IEnumerable<FileItem>;
            if (fileItems == null)
            {
                _rightGalleryItems = new List<Controls.GalleryItem>();
                _rightGalleryPanel.SetItems(_rightGalleryItems);
                return;
            }

            _rightGalleryItems = fileItems
                .Where(f =>
                {
                    var ext = Path.GetExtension(f.FileName).ToLower();
                    return imageExtensions.Contains(ext);
                })
                .Select(f => new Controls.GalleryItem
                {
                    Index = f.Index,
                    FileName = f.FileName,
                    FileSize = f.FileSize,
                    Tag = f
                })
                .ToList();

            _rightGalleryPanel.SetItems(_rightGalleryItems);
        }

        private void RefreshRightGalleryForSprMode()
        {
            // SPR 模式: 從 _sprGroupGrid 取得顯示的群組
            var groupItems = _sprGroupGrid?.DataStore as IEnumerable<SprGroupItem>;
            if (groupItems == null)
            {
                _rightGalleryItems = new List<Controls.GalleryItem>();
                _rightGalleryPanel.SetItems(_rightGalleryItems);
                return;
            }

            _rightGalleryItems = groupItems
                .Select(g => new Controls.GalleryItem
                {
                    Index = g.Id,
                    FileName = $"SPR {g.Id}",
                    FileSize = g.Frames,
                    Tag = g.Group  // Tag 存 SprGroup
                })
                .ToList();

            _rightGalleryPanel.SetItems(_rightGalleryItems);
        }

        private void RefreshRightGalleryForSprListMode()
        {
            // SPR List 模式: 從 _sprListGrid 取得顯示的項目
            var listItems = _sprListGrid?.DataStore as IEnumerable<SprListItem>;
            if (listItems == null)
            {
                _rightGalleryItems = new List<Controls.GalleryItem>();
                _rightGalleryPanel.SetItems(_rightGalleryItems);
                return;
            }

            _rightGalleryItems = listItems
                .Select(item => new Controls.GalleryItem
                {
                    Index = item.SpriteId,
                    FileName = !string.IsNullOrEmpty(item.Name) ? item.Name : $"SPR {item.SpriteId}",
                    FileSize = item.ImageCount,
                    Tag = item  // Tag 存 SprListItem
                })
                .ToList();

            _rightGalleryPanel.SetItems(_rightGalleryItems);
        }

        private Bitmap LoadThumbnailForRightGallery(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _rightGalleryItems.Count) return null;

            var item = _rightGalleryItems[itemIndex];
            var size = _cachedThumbnailSize;

            try
            {
                // 根據 Tag 類型決定載入方式
                if (item.Tag is FileItem fileItem)
                {
                    return LoadThumbnailForFileItem(fileItem, size);
                }
                else if (item.Tag is SprGroup sprGroup)
                {
                    return LoadThumbnailForSprGroup(sprGroup, size);
                }
                else if (item.Tag is SprListItem sprListItem)
                {
                    return LoadThumbnailForSprListItem(sprListItem, size);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Bitmap LoadThumbnailForFileItem(FileItem fileItem, int size)
        {
            // 使用 _currentProvider 或 PAK 模式
            if (_currentProvider != null)
            {
                return LoadThumbnailForFileItem(fileItem, size, _currentProvider);
            }
            else
            {
                var pak = fileItem.SourcePak ?? _currentPak;
                if (pak == null) return null;
                var data = pak.Extract(fileItem.Index);
                return LoadThumbnailFromData(data, fileItem.FileName, size);
            }
        }

        /// <summary>
        /// 從 Provider 載入檔案縮圖 (供新分頁使用)
        /// </summary>
        private Bitmap LoadThumbnailForFileItem(FileItem fileItem, int size, IFileProvider provider)
        {
            var data = provider.Extract(fileItem.Index);
            return LoadThumbnailFromData(data, fileItem.FileName, size);
        }

        /// <summary>
        /// 從原始資料產生縮圖
        /// </summary>
        private Bitmap LoadThumbnailFromData(byte[] data, string fileName, int size)
        {
            if (data == null || data.Length == 0) return null;

            var ext = Path.GetExtension(fileName).ToLower();

            SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image = null;

            try
            {
                switch (ext)
                {
                    case ".spr":
                        var frames = Lin.Helper.Core.Sprite.SprReader.Load(data);
                        if (frames?.Length > 0)
                        {
                            image = frames[0].Image;
                            // 釋放其他 frame 避免記憶體洩漏
                            for (int i = 1; i < frames.Length; i++)
                                frames[i].Image?.Dispose();
                        }
                        break;

                    case ".tbt":
                        // TBT 有 offset，需要先取得 offset 資訊，再用正確畫布大小載入
                        var tbtInfo = L1ImageConverter.LoadL1Image(data);
                        int canvasW = tbtInfo.XOffset + (tbtInfo.Image?.Width ?? 0);
                        int canvasH = tbtInfo.YOffset + (tbtInfo.Image?.Height ?? 0);
                        tbtInfo.Image?.Dispose();
                        if (canvasW > 0 && canvasH > 0)
                        {
                            var tbtResult = L1ImageConverter.LoadL1Image(data, canvasW, canvasH);
                            image = tbtResult.Image;
                        }
                        break;

                    case ".img":
                        image = L1ImageConverter.LoadImg(data);
                        break;

                    case ".png":
                    case ".gif":
                        // PNG/GIF 在 UI 執行緒載入並縮放 (WPF 需要)
                        Bitmap pngResult = null;
                        Application.Instance.Invoke(() =>
                        {
                            using (var ms = new MemoryStream(data))
                            {
                                using var originalBitmap = new Bitmap(ms);
                                int thumbWidth, thumbHeight;
                                if (originalBitmap.Width > originalBitmap.Height)
                                {
                                    thumbWidth = size;
                                    thumbHeight = (int)((double)originalBitmap.Height / originalBitmap.Width * size);
                                }
                                else
                                {
                                    thumbHeight = size;
                                    thumbWidth = (int)((double)originalBitmap.Width / originalBitmap.Height * size);
                                }
                                if (thumbWidth <= 0) thumbWidth = 1;
                                if (thumbHeight <= 0) thumbHeight = 1;

                                pngResult = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format32bppRgba);
                                using (var g = new Graphics(pngResult))
                                {
                                    g.DrawImage(originalBitmap, 0, 0, thumbWidth, thumbHeight);
                                }
                            }
                        });
                        return pngResult;

                    case ".til":
                        image = RenderTilThumbnail(data);
                        break;
                }
            }
            catch
            {
                // 載入失敗，返回 null
                return null;
            }

            if (image == null)
                return null;

            return CreateThumbnailBitmap(image, size);
        }

        private Bitmap LoadThumbnailForSprGroup(SprGroup sprGroup, int size)
        {
            // 取得第一個 part 的第一個 frame
            if (sprGroup?.Parts == null || sprGroup.Parts.Count == 0) return null;

            var firstPart = sprGroup.Parts[0];
            if (firstPart.SourcePak == null) return null;

            var data = firstPart.SourcePak.Extract(firstPart.FileIndex);
            var frames = Lin.Helper.Core.Sprite.SprReader.Load(data);
            if (frames?.Length > 0)
            {
                return CreateThumbnailBitmap(frames[0].Image, size);
            }

            return null;
        }

        private Bitmap LoadThumbnailForSprListItem(SprListItem sprListItem, int size)
        {
            // 從 _sprGroups 找到對應的 SprGroup
            if (_sprGroups == null || !_sprGroups.TryGetValue(sprListItem.SpriteId, out var sprGroup))
                return null;

            return LoadThumbnailForSprGroup(sprGroup, size);
        }

        private void OnRightGalleryItemSelected(object sender, Controls.GalleryItem item)
        {
            if (item?.Tag is FileItem fileItem)
            {
                _statusLabel.Text = $"Selected: {fileItem.FileName} ({fileItem.SizeText})";
            }
        }

        private void OnRightGalleryItemDoubleClicked(object sender, Controls.GalleryItem item)
        {
            if (item?.Tag is FileItem fileItem)
            {
                var pak = fileItem.SourcePak ?? _currentPak;
                if (pak == null) return;

                // 開啟詳細檢視視窗
                var dialog = new ViewerDialog(pak, _rightGalleryItems, item.Index);
                dialog.Show();
            }
        }

        private void OnRightGalleryItemRightClicked(object sender, Controls.GalleryItem item)
        {
            if (item?.Tag == null) return;

            var menu = new ContextMenu();

            // 根據 Tag 類型建立不同的選單
            if (item.Tag is FileItem fileItem)
            {
                BuildFileItemContextMenu(menu, fileItem, item.Index);
            }
            else if (item.Tag is SprGroup sprGroup)
            {
                BuildSprGroupContextMenu(menu, sprGroup);
            }
            else if (item.Tag is SprListItem sprListItem)
            {
                BuildSprListItemContextMenu(menu, sprListItem);
            }

            menu.Show(_rightGalleryPanel);
        }

        private void BuildFileItemContextMenu(ContextMenu menu, FileItem fileItem, int galleryIndex)
        {
            var openInTabMenuItem = new ButtonMenuItem { Text = I18n.T("Context.OpenInNewTab") };
            openInTabMenuItem.Click += (s, e) =>
            {
                var pak = fileItem.SourcePak ?? _currentPak;
                if (pak == null) return;
                var dialog = new ViewerDialog(pak, _rightGalleryItems, galleryIndex);
                dialog.Show();
            };
            menu.Items.Add(openInTabMenuItem);

            menu.Items.Add(new SeparatorMenuItem());

            var exportMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelected") };
            exportMenuItem.Click += (s, e) =>
            {
                var pak = fileItem.SourcePak ?? _currentPak;
                if (pak == null) return;
                try
                {
                    var data = pak.Extract(fileItem.Index);
                    var savePath = Path.Combine(_selectedFolder ?? ".", fileItem.FileName);
                    File.WriteAllBytes(savePath, data);
                    _statusLabel.Text = string.Format(I18n.T("Status.Exported"), 1);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
                }
            };
            menu.Items.Add(exportMenuItem);

            var exportToMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
            exportToMenuItem.Click += (s, e) =>
            {
                var pak = fileItem.SourcePak ?? _currentPak;
                if (pak == null) return;

                var dialog = new SaveFileDialog
                {
                    Title = I18n.T("Dialog.SaveFile"),
                    FileName = fileItem.FileName
                };

                if (dialog.ShowDialog(this) == DialogResult.Ok)
                {
                    try
                    {
                        var data = pak.Extract(fileItem.Index);
                        File.WriteAllBytes(dialog.FileName, data);
                        _statusLabel.Text = string.Format(I18n.T("Status.Exported"), 1);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
                    }
                }
            };
            menu.Items.Add(exportToMenuItem);

            menu.Items.Add(new SeparatorMenuItem());

            var copyFileNameMenuItem = new ButtonMenuItem { Text = I18n.T("Context.CopyFilename") };
            copyFileNameMenuItem.Click += (s, e) =>
            {
                new Clipboard().Text = fileItem.FileName;
            };
            menu.Items.Add(copyFileNameMenuItem);
        }

        private void BuildSprGroupContextMenu(ContextMenu menu, SprGroup sprGroup)
        {
            var exportMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelected") };
            exportMenuItem.Click += (s, e) => ExportSprGroup(sprGroup, false);
            menu.Items.Add(exportMenuItem);

            var exportToMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
            exportToMenuItem.Click += (s, e) => ExportSprGroup(sprGroup, true);
            menu.Items.Add(exportToMenuItem);

            menu.Items.Add(new SeparatorMenuItem());

            var copyIdMenuItem = new ButtonMenuItem { Text = I18n.T("Context.CopyFilename") };
            copyIdMenuItem.Click += (s, e) =>
            {
                new Clipboard().Text = $"SPR {sprGroup.SpriteId}";
            };
            menu.Items.Add(copyIdMenuItem);
        }

        private void BuildSprListItemContextMenu(ContextMenu menu, SprListItem sprListItem)
        {
            var exportMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelected") };
            exportMenuItem.Click += (s, e) =>
            {
                if (_sprGroups != null && _sprGroups.TryGetValue(sprListItem.SpriteId, out var group))
                    ExportSprGroup(group, false);
            };
            menu.Items.Add(exportMenuItem);

            var exportToMenuItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
            exportToMenuItem.Click += (s, e) =>
            {
                if (_sprGroups != null && _sprGroups.TryGetValue(sprListItem.SpriteId, out var group))
                    ExportSprGroup(group, true);
            };
            menu.Items.Add(exportToMenuItem);

            menu.Items.Add(new SeparatorMenuItem());

            var copyNameMenuItem = new ButtonMenuItem { Text = I18n.T("Context.CopyFilename") };
            copyNameMenuItem.Click += (s, e) =>
            {
                new Clipboard().Text = !string.IsNullOrEmpty(sprListItem.Name) ? sprListItem.Name : $"SPR {sprListItem.SpriteId}";
            };
            menu.Items.Add(copyNameMenuItem);
        }

        private void ExportSprGroup(SprGroup group, bool selectFolder)
        {
            if (group?.Parts == null || group.Parts.Count == 0) return;

            string exportPath = _selectedFolder;
            if (selectFolder)
            {
                using var dialog = new SelectFolderDialog { Title = I18n.T("Dialog.SelectExportFolder") };
                if (!string.IsNullOrEmpty(_selectedFolder))
                    dialog.Directory = _selectedFolder;
                if (dialog.ShowDialog(this) != DialogResult.Ok)
                    return;
                exportPath = dialog.Directory;
            }

            int exportedCount = 0;
            int errorCount = 0;

            foreach (var part in group.Parts)
            {
                try
                {
                    if (part.SourcePak == null) continue;

                    var data = part.SourcePak.Extract(part.FileIndex);
                    var outputPath = Path.Combine(exportPath, part.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exportedCount++;
                }
                catch
                {
                    errorCount++;
                }
            }

            _statusLabel.Text = errorCount > 0
                ? string.Format(I18n.T("Status.ExportedWithErrors"), exportedCount, errorCount)
                : string.Format(I18n.T("Status.Exported"), exportedCount);
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
            _currentViewer = Viewers.ViewerFactory.CreateViewerSmart(ext, data, fileName);
            _currentViewer.LoadData(data, fileName);

            // 訂閱儲存事件
            _currentViewer.SaveRequested += OnViewerSaveRequested;

            // 顯示 viewer 控件 (含編輯工具列)
            var viewerControl = _currentViewer.GetControl();
            var editToolbar = _currentViewer.CanEdit ? _currentViewer.GetEditToolbar() : null;

            if (editToolbar != null)
            {
                _viewerPanel.Content = new TableLayout
                {
                    Spacing = new Size(0, 5),
                    Rows =
                    {
                        new TableRow(editToolbar),
                        new TableRow(viewerControl) { ScaleHeight = true }
                    }
                };
            }
            else
            {
                _viewerPanel.Content = viewerControl;
            }

            // 如果 viewer 支援搜尋，使用 viewer 自帶的搜尋工具列
            if (_currentViewer.CanSearch)
            {
                var viewerSearchToolbar = _currentViewer.GetSearchToolbar();
                if (viewerSearchToolbar != null)
                {
                    _searchToolbarContainer.Content = viewerSearchToolbar;
                }
                else
                {
                    _searchToolbarContainer.Content = _defaultSearchToolbar;
                    _textSearchResultLabel.Text = "";
                }
            }
            else
            {
                _searchToolbarContainer.Content = _defaultSearchToolbar;
                _textSearchResultLabel.Text = "";
            }
        }

        /// <summary>
        /// 處理 Viewer 的儲存請求，將資料寫回 PAK
        /// </summary>
        private void OnViewerSaveRequested(object sender, Viewers.SaveRequestedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnViewerSaveRequested called: pak={_currentViewerPak?.IdxPath}, file={_currentViewerFileName}");

            if (_currentViewerPak == null || string.IsNullOrEmpty(_currentViewerFileName))
            {
                MessageBox.Show(this, I18n.T("Error.CannotSaveNoPak"), I18n.T("Dialog.Error"), MessageBoxType.Error);
                return;
            }

            try
            {
                // 替換 PAK 中的檔案內容
                _currentViewerPak.Replace(_currentViewerFileName, e.Data);
                _currentViewerPak.Save();

                _statusLabel.Text = I18n.T("Status.Saved") + ": " + _currentViewerFileName;
                ShowToast(I18n.T("Toast.SaveSuccess"), _currentViewerFileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, I18n.T("Error.SaveFailed") + ": " + ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
            }
        }

        /// <summary>
        /// 顯示 Toast 通知（右下角，自動消失）
        /// </summary>
        private void ShowToast(string title, string message, int durationMs = 3000)
        {
            var toast = new Form
            {
                Title = "",
                ShowInTaskbar = false,
                Minimizable = false,
                Maximizable = false,
                Resizable = false,
                WindowStyle = WindowStyle.None,
                BackgroundColor = Colors.DarkSlateGray,
                Size = new Size(280, 70)
            };

            var layout = new StackLayout
            {
                Padding = 10,
                Spacing = 5,
                Items =
                {
                    new Label { Text = title, TextColor = Colors.LightGreen, Font = new Font(SystemFont.Bold, 12) },
                    new Label { Text = message, TextColor = Colors.White, Font = new Font(SystemFont.Default, 10) }
                }
            };
            toast.Content = layout;

            // 定位到主視窗右下角
            var mainBounds = this.Bounds;
            toast.Location = new Point(
                mainBounds.Right - toast.Width - 20,
                mainBounds.Bottom - toast.Height - 40
            );

            toast.Show();

            // 自動關閉
            var timer = new UITimer { Interval = durationMs / 1000.0 };
            timer.Elapsed += (s, ev) =>
            {
                timer.Stop();
                toast.Close();
            };
            timer.Start();
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
                Title = I18n.T("Dialog.OpenClientFolder")
            };

            // Use last folder as starting point
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = _settings.LastFolder;
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    // 清除舊的 PAK 模式
                    _currentPak?.Dispose();
                    _currentPak = null;
                    _allPakFiles?.Values.ToList().ForEach(p => p?.Dispose());
                    _allPakFiles = null;
                    _isAllIdxMode = false;
                    _currentProvider?.Dispose();

                    // 建立 LinClientProvider
                    _currentProvider = new LinClientProvider(dialog.Directory);

                    _selectedFolder = dialog.Directory;
                    _folderLabel.Text = Path.GetFileName(_selectedFolder);

                    // Save to settings (這是有效的 client 資料夾)
                    _settings.LastFolder = _selectedFolder;
                    _settings.LastClientFolder = _selectedFolder;
                    _settings.Save();

                    // 顯示模式選擇器 (Lin Client 模式需要)
                    _viewModeRadio.Visible = true;

                    // 從 Provider 取得選項填入下拉選單
                    _idxDropDown.Items.Clear();
                    foreach (var option in _currentProvider.GetSourceOptions())
                    {
                        _idxDropDown.Items.Add(option);
                    }

                    // 選取 Provider 的預設選項
                    if (_idxDropDown.Items.Count > 0)
                    {
                        var currentOption = _currentProvider.CurrentSourceOption;
                        var matchItem = _idxDropDown.Items.FirstOrDefault(i => i.Text == currentOption);
                        _idxDropDown.SelectedIndex = matchItem != null ? _idxDropDown.Items.IndexOf(matchItem) : 0;
                    }

                    // 更新副檔名篩選和檔案列表
                    UpdateExtensionFilter();
                    RefreshFileList();

                    _statusLabel.Text = $"Loaded: {_currentProvider.Name} ({_currentProvider.Count} files)";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Error: {ex.Message}", "Error", MessageBoxType.Error);
                }
            }
        }

        private void OnOpenFileFolder(object sender, EventArgs e)
        {
            using var dialog = new SelectFolderDialog
            {
                Title = I18n.T("Dialog.SelectFileFolder")
            };

            // Use last folder as starting point
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = _settings.LastFolder;
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                OpenFolderInNewTab(dialog.Directory);
            }
        }

        private void OpenFolderInNewTab(string folderPath)
        {
            var tabKey = $"folder:{folderPath}";

            // Check if already open
            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var provider = new FolderFileProvider(folderPath);
                var folderName = Path.GetFileName(folderPath);

                // Create browser content for this folder
                var browserContent = CreateProviderBrowserContent(provider);

                var docPage = new TabPage
                {
                    Text = $"📁 {folderName}",
                    Content = browserContent
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                // Save to settings
                _settings.LastFolder = folderPath;
                _settings.Save();

                _statusLabel.Text = $"Opened folder: {folderName} ({provider.Count} files)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening folder: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private void OnOpenFile(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = I18n.T("Dialog.SelectFile"),
                Filters = {
                    new FileFilter("Image Files", ".tbt", ".spr", ".img", ".til", ".png", ".gif", ".jpg", ".bmp"),
                    new FileFilter("All Files", ".*")
                }
            };

            // Use last folder as starting point
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.Directory = new Uri(_settings.LastFolder);
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                OpenFileInNewTab(dialog.FileName);
            }
        }

        private void OpenFileInNewTab(string filePath)
        {
            var tabKey = $"file:{filePath}";

            // Check if already open
            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var provider = new SingleFileProvider(filePath);
                var fileName = Path.GetFileName(filePath);

                // Create browser content for this file
                var browserContent = CreateProviderBrowserContent(provider);

                var docPage = new TabPage
                {
                    Text = $"📄 {fileName}",
                    Content = browserContent
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                // Save to settings
                _settings.LastFolder = Path.GetDirectoryName(filePath);
                _settings.Save();

                _statusLabel.Text = $"Opened: {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening file: {ex.Message}", "Error", MessageBoxType.Error);
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
                var provider = new PakProvider(idxPath);
                var idxName = Path.GetFileName(idxPath);

                // Create browser content using shared provider browser
                var browserContent = CreateProviderBrowserContent(provider);

                var docPage = new TabPage
                {
                    Text = $"📦 {idxName}",
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

                _statusLabel.Text = $"Opened: {idxName} ({provider.Count} files)";
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
                HeaderText = I18n.T("Grid.No"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.FileName"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.FileName) },
                Width = 180
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Size"),
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
                        textViewer.Text = I18n.T("Status.BinaryFile");
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

        private Control CreateProviderBrowserContent(IFileProvider provider)
        {
            // === 狀態變數 ===
            List<FileItem> allItems = null;
            List<FileItem> filteredItems = null;
            string currentExtFilter = "All";
            string currentFilter = "";      // 篩選框 (即時，搜檔名)
            HashSet<int> contentSearchResults = null;  // 內容搜尋結果
            bool isGalleryMode = false;

            // === 建立 UI 元件 ===

            // 檔案列表 Grid
            var fileGrid = new GridView
            {
                AllowMultipleSelection = true,
                ShowHeader = true
            };

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.No"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.FileName"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.FileName) },
                Width = 180
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Size"),
                DataCell = new TextBoxCell { Binding = Binding.Property<FileItem, string>(r => r.SizeText) },
                Width = 80
            });

            // 來源下拉選單 (資料夾名稱)
            var sourceDropDown = new DropDown { Width = 200 };
            foreach (var option in provider.GetSourceOptions())
            {
                sourceDropDown.Items.Add(option);
            }
            if (sourceDropDown.Items.Count > 0)
                sourceDropDown.SelectedIndex = 0;

            // 副檔名下拉選單
            var extDropDown = new DropDown();
            extDropDown.Items.Add("All");

            // 篩選文字框
            var filterBox = new TextBox { PlaceholderText = I18n.T("Placeholder.Filter") };

            // 搜尋文字框和按鈕
            var searchBox = new TextBox { PlaceholderText = I18n.T("Placeholder.Search") };
            var searchBtn = new Button { Text = I18n.T("Button.Search") };
            var clearBtn = new Button { Text = I18n.T("Button.Clear") };

            // === 右側面板 ===
            var viewerPanel = new Panel { BackgroundColor = Colors.DarkGray };

            // 預覽模式元件 (for fallback text display)
            var textViewer = new RichTextArea { ReadOnly = true, Font = new Font("Menlo, Monaco, Consolas, monospace", 12) };

            // 相簿面板
            var galleryPanel = new Controls.GalleryPanel();

            // 目前預覽的 Viewer (用於統一處理各種格式)
            Viewers.IFileViewer currentViewer = null;

            // 預覽/相簿 模式切換
            var previewRadio = new RadioButton { Text = I18n.T("RightPanel.Preview") };
            var galleryRadio = new RadioButton(previewRadio) { Text = I18n.T("RightPanel.Gallery") };
            previewRadio.Checked = true;

            // 縮圖大小滑桿
            var thumbnailSlider = new Slider
            {
                MinValue = 48,
                MaxValue = 200,
                Value = 80,
                Width = 100
            };
            int cachedThumbSize = 80;  // 快取縮圖大小，避免在背景執行緒存取 UI

            // 搜尋工具列
            var textSearchBox = new TextBox { PlaceholderText = I18n.T("Placeholder.SearchInText"), Width = 200 };
            var textSearchPrevBtn = new Button { Text = "◀", Width = 30 };
            var textSearchNextBtn = new Button { Text = "▶", Width = 30 };

            // 右側面板容器
            var rightPanelContainer = new Panel { Content = viewerPanel };

            // === 輔助函式 ===
            Action refreshExtensions = () =>
            {
                var currentExt = extDropDown.SelectedValue?.ToString() ?? "All";
                extDropDown.Items.Clear();
                extDropDown.Items.Add("All");
                foreach (var ext in provider.GetExtensions())
                {
                    extDropDown.Items.Add(ext);
                }
                var match = extDropDown.Items.FirstOrDefault(i => i.Text == currentExt);
                extDropDown.SelectedIndex = match != null ? extDropDown.Items.IndexOf(match) : 0;
            };

            // 縮圖載入器 - 使用共用的 LoadThumbnailForFileItem 方法
            // 注意：此 callback 會在背景執行緒呼叫，不能存取 UI 控制項
            galleryPanel.ThumbnailLoader = (index) =>
            {
                try
                {
                    if (filteredItems == null || index < 0 || index >= filteredItems.Count)
                        return null;

                    var item = filteredItems[index];
                    return LoadThumbnailForFileItem(item, cachedThumbSize, provider);
                }
                catch
                {
                    return null;
                }
            };

            Action updateGalleryItems = () =>
            {
                if (filteredItems == null) return;
                var galleryItems = filteredItems.Select((f, i) => new Controls.GalleryItem
                {
                    Index = i,
                    FileName = f.FileName,
                    FileSize = f.FileSize,
                    Tag = f
                }).ToList();
                galleryPanel.SetItems(galleryItems);
            };

            Action refreshFileList = () =>
            {
                // 從 provider 重新載入
                allItems = provider.Files.Select(f => new FileItem
                {
                    Index = f.Index,
                    FileName = f.FileName,
                    FileSize = (int)f.FileSize,
                    Offset = f.Offset,
                    IdxName = f.SourceName
                }).ToList();

                // 套用篩選
                filteredItems = allItems.Where(item =>
                {
                    // 副檔名篩選
                    if (currentExtFilter != "All")
                    {
                        var ext = Path.GetExtension(item.FileName)?.ToLowerInvariant() ?? "";
                        if (ext != currentExtFilter) return false;
                    }
                    // 篩選框 (即時篩選檔名)
                    if (!string.IsNullOrEmpty(currentFilter))
                    {
                        if (!item.FileName.Contains(currentFilter, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    // 內容搜尋結果
                    if (contentSearchResults != null)
                    {
                        if (!contentSearchResults.Contains(item.Index))
                            return false;
                    }
                    return true;
                }).ToList();

                fileGrid.DataStore = filteredItems;

                // 更新相簿
                if (isGalleryMode)
                {
                    updateGalleryItems();
                }
            };

            Action<bool> setGalleryMode = (gallery) =>
            {
                isGalleryMode = gallery;
                if (gallery)
                {
                    updateGalleryItems();
                    rightPanelContainer.Content = galleryPanel;
                }
                else
                {
                    rightPanelContainer.Content = viewerPanel;
                }
            };

            // === 事件處理 ===

            sourceDropDown.SelectedIndexChanged += (s, e) =>
            {
                var selected = sourceDropDown.SelectedValue?.ToString();
                if (!string.IsNullOrEmpty(selected))
                {
                    provider.SetSourceOption(selected);
                    refreshExtensions();
                    refreshFileList();
                }
            };

            extDropDown.SelectedIndexChanged += (s, e) =>
            {
                currentExtFilter = extDropDown.SelectedValue?.ToString() ?? "All";
                refreshFileList();
            };

            filterBox.TextChanged += (s, e) =>
            {
                currentFilter = filterBox.Text ?? "";
                refreshFileList();
            };

            // 內容搜尋動作 - 搜尋檔案內容
            Action doContentSearch = () =>
            {
                var keyword = searchBox.Text?.Trim();
                if (string.IsNullOrEmpty(keyword))
                    return;

                // 先提取所有檔案資料
                var fileDataList = new List<(int Index, string FileName, string Ext, byte[] Data)>();
                foreach (var file in provider.Files)
                {
                    try
                    {
                        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
                        var data = provider.Extract(file.Index);
                        fileDataList.Add((file.Index, file.FileName, ext, data));
                    }
                    catch { }
                }

                // 平行搜尋內容
                var results = new System.Collections.Concurrent.ConcurrentBag<int>();
                System.Threading.Tasks.Parallel.ForEach(fileDataList, item =>
                {
                    try
                    {
                        using var viewer = Viewers.ViewerFactory.CreateViewer(item.Ext);
                        var text = viewer.GetTextContent(item.Data, item.FileName);
                        if (text != null && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(item.Index);
                        }
                    }
                    catch { }
                });

                contentSearchResults = new HashSet<int>(results);
                refreshFileList();
            };

            searchBtn.Click += (s, e) => doContentSearch();

            // Enter 鍵送出搜尋
            searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    doContentSearch();
                    e.Handled = true;
                }
            };

            clearBtn.Click += (s, e) =>
            {
                searchBox.Text = "";
                contentSearchResults = null;
                currentFilter = "";
                filterBox.Text = "";
                refreshFileList();
            };

            previewRadio.CheckedChanged += (s, e) =>
            {
                if (previewRadio.Checked) setGalleryMode(false);
            };

            galleryRadio.CheckedChanged += (s, e) =>
            {
                if (galleryRadio.Checked) setGalleryMode(true);
            };

            thumbnailSlider.ValueChanged += (s, e) =>
            {
                cachedThumbSize = thumbnailSlider.Value;
                galleryPanel.ThumbnailSize = thumbnailSlider.Value;
            };

            galleryPanel.ItemSelected += (s, galleryItem) =>
            {
                if (galleryItem == null) return;
                // 在 grid 中選取對應項目
                if (galleryItem.Index >= 0 && galleryItem.Index < filteredItems?.Count)
                {
                    fileGrid.SelectedRow = galleryItem.Index;
                }
            };

            galleryPanel.ItemDoubleClicked += (s, galleryItem) =>
            {
                if (galleryItem?.Tag is not FileItem fileItem) return;
                try
                {
                    var data = provider.Extract(fileItem.Index);
                    var ext = Path.GetExtension(fileItem.FileName).ToLowerInvariant();
                    var content = CreateTabContent(ext, data, fileItem.FileName);
                    if (content == null) return;

                    var tabKey = $"{provider.Name}:{fileItem.Index}";
                    if (_openTabs.ContainsKey(tabKey))
                    {
                        _mainTabControl.SelectedPage = _openTabs[tabKey];
                        return;
                    }

                    var docPage = new TabPage
                    {
                        Text = fileItem.FileName,
                        Content = content
                    };
                    docPage.Tag = tabKey;

                    _openTabs[tabKey] = docPage;
                    _mainTabControl.Pages.Add(docPage);
                    _mainTabControl.SelectedPage = docPage;
                }
                catch { }
            };

            fileGrid.SelectionChanged += (s, e) =>
            {
                if (isGalleryMode) return;  // 相簿模式由相簿處理

                var selected = fileGrid.SelectedItem as FileItem;
                if (selected == null) return;

                // 釋放舊的 viewer
                currentViewer?.Dispose();
                currentViewer = null;

                try
                {
                    var data = provider.Extract(selected.Index);
                    var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();

                    // 使用 ViewerFactory 建立適合的 viewer
                    currentViewer = Viewers.ViewerFactory.CreateViewerSmart(ext, data, selected.FileName);
                    currentViewer.LoadData(data, selected.FileName);

                    // 顯示 viewer 控件
                    viewerPanel.Content = currentViewer.GetControl();

                    _statusLabel.Text = $"Selected: {selected.FileName} ({selected.SizeText})";
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"Error: {ex.Message}";
                }
            };

            fileGrid.CellDoubleClick += (s, e) =>
            {
                var selected = fileGrid.SelectedItem as FileItem;
                if (selected == null) return;

                try
                {
                    var data = provider.Extract(selected.Index);
                    var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();
                    var content = CreateTabContent(ext, data, selected.FileName);
                    if (content == null) return;

                    var tabKey = $"{provider.Name}:{selected.Index}";
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

            // === 初始化 ===
            refreshExtensions();
            refreshFileList();

            // 自動選取第一個檔案
            if (filteredItems != null && filteredItems.Count > 0)
            {
                fileGrid.SelectedRow = 0;
            }

            // === 佈局 ===
            const int labelWidth = 50;

            var leftTopBar = new TableLayout
            {
                Padding = new Padding(5),
                Spacing = new Size(5, 5),
                Rows =
                {
                    // 來源
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.IDX"), Width = labelWidth }, false),
                        new TableCell(sourceDropDown, true)
                    ),
                    // 副檔名
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Ext"), Width = labelWidth }, false),
                        new TableCell(extDropDown, true)
                    ),
                    // 篩選
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Filter"), Width = labelWidth }, false),
                        new TableCell(filterBox, true)
                    ),
                    // 搜尋
                    new TableRow(
                        new TableCell(new Label { Text = I18n.T("Label.Search"), Width = labelWidth }, false),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 2,
                            Items = { new StackLayoutItem(searchBox, true), searchBtn, clearBtn }
                        }, true)
                    )
                }
            };

            var leftPanel = new TableLayout
            {
                Rows =
                {
                    new TableRow(leftTopBar),
                    new TableRow(fileGrid) { ScaleHeight = true }
                }
            };

            var rightModeToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    previewRadio,
                    galleryRadio,
                    new Label { Text = "|", VerticalAlignment = VerticalAlignment.Center },
                    new Label { Text = I18n.T("RightPanel.ThumbnailSize"), VerticalAlignment = VerticalAlignment.Center },
                    thumbnailSlider
                }
            };

            var rightSearchToolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    new Label { Text = I18n.T("Label.Find") },
                    textSearchBox,
                    textSearchPrevBtn,
                    textSearchNextBtn
                }
            };

            var rightPanel = new TableLayout
            {
                Rows =
                {
                    new TableRow(rightModeToolbar),
                    new TableRow(rightSearchToolbar),
                    new TableRow(rightPanelContainer) { ScaleHeight = true }
                }
            };

            var splitter = new Splitter
            {
                Orientation = Orientation.Horizontal,
                Position = 400,
                FixedPanel = SplitterFixedPanel.Panel1,
                Panel1 = leftPanel,
                Panel2 = rightPanel
            };

            return splitter;
        }

        private void OnIdxChanged(object sender, EventArgs e)
        {
            if (_idxDropDown.SelectedIndex < 0)
                return;

            // Provider 模式 - 讓 Provider 處理選項變更
            if (_currentProvider != null)
            {
                var selected = _idxDropDown.SelectedValue?.ToString();
                if (!string.IsNullOrEmpty(selected))
                {
                    _currentProvider.SetSourceOption(selected);
                    UpdateExtensionFilter();
                    RefreshFileList();
                }
                return;
            }

            if (string.IsNullOrEmpty(_selectedFolder))
                return;

            // 在 SPR 或 SPR List 模式時，IDX 是鎖定的，不處理
            if (_viewModeRadio.SelectedIndex != MODE_NORMAL)
                return;

            var selectedLegacy = _idxDropDown.SelectedValue.ToString();
            if (selectedLegacy == I18n.T("Filter.All"))
            {
                LoadAllIdxFiles();
            }
            else
            {
                _isAllIdxMode = false;
                _allPakFiles = null;
                var idxFile = Path.Combine(_selectedFolder, selectedLegacy);
                LoadIdxFile(idxFile);
            }
        }

        private void LoadAllIdxFiles()
        {
            if (string.IsNullOrEmpty(_selectedFolder)) return;

            _isAllIdxMode = true;
            _allPakFiles = new Dictionary<string, PakFile>();

            var idxFiles = Directory.GetFiles(_selectedFolder, "*.idx", SearchOption.TopDirectoryOnly);
            foreach (var idxFile in idxFiles.OrderBy(f => f))
            {
                try
                {
                    var idxName = Path.GetFileName(idxFile);
                    var pak = new PakFile(idxFile);
                    _allPakFiles[idxName] = pak;
                }
                catch { }
            }

            // Set first PAK as current (for fallback)
            _currentPak = _allPakFiles.Values.FirstOrDefault();
            UpdateExtensionFilter();
            RefreshFileList();

            // 如果在相簿模式，重新整理相簿
            if (_galleryModeRadio?.Checked == true)
            {
                RefreshRightGallery();
            }
        }

        private void OnOpenDatFile(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open Lineage M Icon/Image DAT File",
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
                var provider = new DatProvider(datPath);
                var datName = Path.GetFileName(datPath);

                // Create browser content using shared provider browser
                var browserContent = CreateProviderBrowserContent(provider);

                var docPage = new TabPage
                {
                    Text = $"📀 {datName}",
                    Content = browserContent
                };
                docPage.Tag = tabKey;

                _openTabs[tabKey] = docPage;
                _mainTabControl.Pages.Add(docPage);
                _mainTabControl.SelectedPage = docPage;

                _settings.LastFolder = Path.GetDirectoryName(datPath);
                _settings.Save();

                _statusLabel.Text = $"Opened DAT: {datName} ({provider.Count} files)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening DAT: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private Control CreateDatBrowserContent(DatTools.DatFile datFile)
        {
            // Filter toolbar
            var filterLabel = new Label { Text = I18n.T("Label.Filter"), VerticalAlignment = VerticalAlignment.Center };
            var filterBox = new TextBox { PlaceholderText = I18n.T("Placeholder.TypeToFilter"), Width = 200 };
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
                HeaderText = I18n.T("Grid.No"),
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.Index.ToString()) },
                Width = 60
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.FileName"),
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.FileName) },
                Width = 200
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Size"),
                DataCell = new TextBoxCell { Binding = Binding.Property<DatFileItem, string>(r => r.SizeText) },
                Width = 80
            });

            fileGrid.Columns.Add(new GridColumn
            {
                HeaderText = I18n.T("Grid.Offset"),
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
            var exportItem = new ButtonMenuItem { Text = I18n.T("Context.ExportSelectedTo") };
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

                // 如果在相簿模式，重新整理相簿
                if (_galleryModeRadio?.Checked == true)
                {
                    RefreshRightGallery();
                }

                _statusLabel.Text = $"Loaded: {Path.GetFileName(idxPath)} ({_currentPak.Count} files, {_currentPak.EncryptionType})";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading file: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }

        private void UpdateExtensionFilter()
        {
            IEnumerable<string> extensions;

            // Provider 模式
            if (_currentProvider != null)
            {
                extensions = _currentProvider.GetExtensions();
            }
            else if (_isAllIdxMode && _allPakFiles != null)
            {
                extensions = _allPakFiles.Values
                    .SelectMany(pak => pak.Files)
                    .Select(f => Path.GetExtension(f.FileName).ToLowerInvariant())
                    .Where(ext => !string.IsNullOrEmpty(ext))
                    .Distinct()
                    .OrderBy(ext => ext);
            }
            else if (_currentPak != null)
            {
                extensions = _currentPak.Files
                    .Select(f => Path.GetExtension(f.FileName).ToLowerInvariant())
                    .Where(ext => !string.IsNullOrEmpty(ext))
                    .Distinct()
                    .OrderBy(ext => ext);
            }
            else
            {
                extensions = Enumerable.Empty<string>();
            }

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

            // 使用 debounce 避免打字時卡頓
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer = new UITimer { Interval = 0.3 };
            _searchDebounceTimer.Elapsed += (s, ev) =>
            {
                _searchDebounceTimer.Stop();
                var mode = _viewModeRadio.SelectedIndex;
                if (mode == MODE_SPR)
                {
                    UpdateSprGroupDisplay();
                }
                else if (mode == MODE_SPR_LIST)
                {
                    UpdateSprListDisplay();
                }
                else
                {
                    RefreshFileList();
                }
            };
            _searchDebounceTimer.Start();
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
            if (string.IsNullOrEmpty(keyword))
                return;

            if (_isAllIdxMode)
            {
                MessageBox.Show(this, "Content search is not supported in 'All IDX' mode.\nPlease select a specific IDX file.", "Content Search", MessageBoxType.Information);
                return;
            }

            if (_currentPak == null)
                return;

            _contentSearchKeyword = keyword;
            _statusLabel.Text = I18n.T("Status.SearchingContent");

            // 先提取所有檔案資料（PAK 提取不一定 thread-safe）
            var fileDataList = new List<(int Index, string FileName, string Ext, byte[] Data)>();
            for (int i = 0; i < _currentPak.Count; i++)
            {
                try
                {
                    var file = _currentPak.Files[i];
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var data = _currentPak.Extract(i);
                    fileDataList.Add((i, file.FileName, ext, data));
                }
                catch { }
            }

            // 平行搜尋
            var results = new System.Collections.Concurrent.ConcurrentBag<int>();
            System.Threading.Tasks.Parallel.ForEach(fileDataList, item =>
            {
                try
                {
                    using var viewer = Viewers.ViewerFactory.CreateViewer(item.Ext);
                    var text = viewer.GetTextContent(item.Data, item.FileName);

                    if (text != null && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(item.Index);
                    }
                }
                catch { }
            });

            _contentSearchResults = new HashSet<int>(results);
            _statusLabel.Text = $"Content search: found {_contentSearchResults.Count} files containing '{keyword}'";
            RefreshFileList();
        }

        private void OnClearContentSearch(object sender, EventArgs e)
        {
            _contentSearchKeyword = "";
            _contentSearchResults = null;
            _contentSearchBox.Text = "";
            _statusLabel.Text = I18n.T("Status.ContentSearchCleared");
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            var items = new List<FileItem>();
            _filteredIndexes = new List<int>();
            int totalCount = 0;

            // Provider 模式 (資料夾/單一檔案)
            if (_currentProvider != null)
            {
                totalCount = _currentProvider.Count;

                for (int i = 0; i < _currentProvider.Count; i++)
                {
                    var file = _currentProvider.Files[i];
                    var fileName = file.FileName;
                    var lowerName = fileName.ToLowerInvariant();

                    // Apply extension filter
                    if (_currentExtFilter != "All")
                    {
                        var ext = Path.GetExtension(fileName).ToLowerInvariant();
                        if (ext != _currentExtFilter)
                            continue;
                    }

                    // Apply filename search filter
                    if (!string.IsNullOrEmpty(_currentFilter))
                    {
                        if (!fileName.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    _filteredIndexes.Add(i);
                    items.Add(new FileItem
                    {
                        Index = i,
                        FileName = fileName,
                        FileSize = (int)file.FileSize,
                        Offset = file.Offset,
                        IdxName = file.SourceName,
                        SourcePak = null  // Provider 模式不使用 SourcePak
                    });
                }
            }
            else
            {
                // PAK 模式
                // Get PAK files to iterate
                IEnumerable<KeyValuePair<string, PakFile>> pakSources;
                if (_isAllIdxMode && _allPakFiles != null)
                {
                    pakSources = _allPakFiles;
                }
                else if (_currentPak != null)
                {
                    var currentIdxName = _idxDropDown.SelectedValue?.ToString() ?? "";
                    pakSources = new[] { new KeyValuePair<string, PakFile>(currentIdxName, _currentPak) };
                }
                else
                {
                    return;
                }

                foreach (var pakEntry in pakSources)
                {
                    var idxName = pakEntry.Key;
                    var pak = pakEntry.Value;
                    totalCount += pak.Count;

                    for (int i = 0; i < pak.Count; i++)
                    {
                        var file = pak.Files[i];
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

                        // Apply content search filter (only for single PAK mode)
                        if (!_isAllIdxMode && _contentSearchResults != null)
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
                            Offset = file.Offset,
                            IdxName = _isAllIdxMode ? idxName : null,
                            SourcePak = pak
                        });
                    }
                }
            }

            // Apply sorting if any
            items = ApplySorting(items);

            _fileGrid.DataStore = items;
            _recordCountLabel.Text = $"Records: {items.Count} / {totalCount}";

            // 如果相簿模式開啟，同步更新相簿
            if (_galleryModeRadio?.Checked == true)
            {
                RefreshRightGallery();
            }
        }

        private List<FileItem> ApplySorting(List<FileItem> items)
        {
            if (_currentSortColumn == SortColumn.None || items == null || items.Count == 0)
                return items;

            IEnumerable<FileItem> sorted = _currentSortColumn switch
            {
                SortColumn.Index => _sortAscending
                    ? items.OrderBy(x => x.Index)
                    : items.OrderByDescending(x => x.Index),
                SortColumn.FileName => _sortAscending
                    ? items.OrderBy(x => x.FileName, NaturalStringComparer.Instance)
                    : items.OrderByDescending(x => x.FileName, NaturalStringComparer.Instance),
                SortColumn.Size => _sortAscending
                    ? items.OrderBy(x => x.FileSize)
                    : items.OrderByDescending(x => x.FileSize),
                SortColumn.IdxName => _sortAscending
                    ? items.OrderBy(x => x.IdxName ?? "", NaturalStringComparer.Instance)
                    : items.OrderByDescending(x => x.IdxName ?? "", NaturalStringComparer.Instance),
                _ => items
            };

            return sorted.ToList();
        }

        private void OnFileGridColumnHeaderClick(object sender, GridColumnEventArgs e)
        {
            var columnId = e.Column.ID;
            SortColumn newSortColumn = columnId switch
            {
                "Index" => SortColumn.Index,
                "FileName" => SortColumn.FileName,
                "Size" => SortColumn.Size,
                "IdxName" => SortColumn.IdxName,
                _ => SortColumn.None
            };

            if (newSortColumn == SortColumn.None)
                return;

            // Toggle direction if same column, otherwise default to ascending
            if (_currentSortColumn == newSortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _currentSortColumn = newSortColumn;
                _sortAscending = true;
            }

            // Update column header to show sort indicator
            UpdateSortIndicator(e.Column);

            // Refresh the list with new sorting
            RefreshFileList();
        }

        private void UpdateSortIndicator(GridColumn column)
        {
            // Reset previous sorted column header
            if (_sortedGridColumn != null && _sortedGridColumn != column)
            {
                var prevId = _sortedGridColumn.ID;
                _sortedGridColumn.HeaderText = prevId switch
                {
                    "Index" => I18n.T("Grid.No"),
                    "FileName" => I18n.T("Grid.FileName"),
                    "Size" => I18n.T("Grid.Size"),
                    "IdxName" => I18n.T("Grid.IDX"),
                    _ => _sortedGridColumn.HeaderText
                };
            }

            // Update current column header with sort indicator
            string baseHeader = column.ID switch
            {
                "Index" => I18n.T("Grid.No"),
                "FileName" => I18n.T("Grid.FileName"),
                "Size" => I18n.T("Grid.Size"),
                "IdxName" => I18n.T("Grid.IDX"),
                _ => column.HeaderText
            };

            string indicator = _sortAscending ? " ▲" : " ▼";
            column.HeaderText = baseHeader + indicator;

            _sortedGridColumn = column;
        }

        private void OnFileSelected(object sender, EventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected == null) return;

            try
            {
                byte[] data;

                // Provider 模式
                if (_currentProvider != null)
                {
                    data = _currentProvider.Extract(selected.Index);
                    _currentViewerPak = null;
                }
                else
                {
                    // PAK 模式
                    var pak = selected.SourcePak ?? _currentPak;
                    if (pak == null) return;

                    data = pak.Extract(selected.Index);
                    _currentViewerPak = pak;
                }

                var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();

                // 記錄當前預覽的檔案來源（用於儲存功能）
                _currentViewerIndex = selected.Index;
                _currentViewerFileName = selected.FileName;

                ShowFilePreview(ext, data, selected.FileName);
                var idxInfo = selected.IdxName != null ? $" [{selected.IdxName}]" : "";
                _statusLabel.Text = $"Selected: {selected.FileName} ({selected.SizeText}){idxInfo}";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
        }

        #region 縮圖工具函數

        private Bitmap CreateThumbnailBitmap(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> source, int size)
        {
            // 等比縮放
            int width, height;
            if (source.Width > source.Height)
            {
                width = size;
                height = (int)((double)source.Height / source.Width * size);
            }
            else
            {
                height = size;
                width = (int)((double)source.Width / source.Height * size);
            }

            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            // 縮放圖片
            source.Mutate(ctx => ctx.Resize(width, height));

            // 轉換為 byte array (可以在背景執行緒執行)
            using var ms = new MemoryStream();
            source.Save(ms, new PngEncoder());
            var pngBytes = ms.ToArray();

            // 在 UI 執行緒建立 Bitmap (WPF 需要)
            Bitmap result = null;
            Application.Instance.Invoke(() =>
            {
                result = new Bitmap(pngBytes);
            });
            return result;
        }

        private SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> RenderTilThumbnail(byte[] data)
        {
            try
            {
                var blocks = L1Til.Parse(data);
                if (blocks == null || blocks.Count == 0) return null;

                var tileSize = L1Til.GetTileSize(data);
                int cols = Math.Min(8, blocks.Count);
                int rows = Math.Min(4, (blocks.Count + cols - 1) / cols);
                int width = cols * tileSize;
                int height = rows * tileSize;

                var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);

                for (int i = 0; i < Math.Min(blocks.Count, cols * rows); i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int destX = col * tileSize;
                    int destY = row * tileSize;

                    var rgb555Canvas = new ushort[tileSize * tileSize];
                    L1Til.RenderBlock(blocks[i], 0, 0, rgb555Canvas, tileSize, tileSize);

                    for (int py = 0; py < tileSize; py++)
                    {
                        for (int px = 0; px < tileSize; px++)
                        {
                            ushort rgb555 = rgb555Canvas[py * tileSize + px];
                            if (rgb555 != 0)
                            {
                                int r5 = (rgb555 >> 10) & 0x1F;
                                int g5 = (rgb555 >> 5) & 0x1F;
                                int b5 = rgb555 & 0x1F;
                                byte r = (byte)((r5 << 3) | (r5 >> 2));
                                byte g = (byte)((g5 << 3) | (g5 >> 2));
                                byte b = (byte)((b5 << 3) | (b5 >> 2));
                                image[destX + px, destY + py] = new SixLabors.ImageSharp.PixelFormats.Rgba32(r, g, b, 255);
                            }
                        }
                    }
                }

                return image;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        private void OnExportSelected(object sender, EventArgs e)
        {
            if (_fileGrid.SelectedRows.Count() == 0)
            {
                MessageBox.Show(this, "No files selected", "Export", MessageBoxType.Information);
                return;
            }

            // Export to same folder as idx file
            var outputFolder = _selectedFolder;
            if (string.IsNullOrEmpty(outputFolder))
            {
                MessageBox.Show(this, "No folder selected. Please use 'Export To...' instead.", "Export", MessageBoxType.Information);
                return;
            }

            int exported = 0;
            int failed = 0;
            foreach (var row in _fileGrid.SelectedRows)
            {
                var item = (FileItem)_fileGrid.DataStore.ElementAt(row);
                var pak = item.SourcePak ?? _currentPak;
                if (pak == null) { failed++; continue; }
                try
                {
                    var data = pak.Extract(item.Index);
                    var outputPath = Path.Combine(outputFolder, item.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exported++;
                }
                catch (Exception ex)
                {
                    failed++;
                    System.Diagnostics.Debug.WriteLine($"Export failed: {item.FileName} - {ex.Message}");
                }
            }

            _statusLabel.Text = $"Exported {exported} files to {outputFolder}" + (failed > 0 ? $" ({failed} failed)" : "");
        }

        private void OnExportSelectedTo(object sender, EventArgs e)
        {
            if (_fileGrid.SelectedRows.Count() == 0)
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
                var pak = item.SourcePak ?? _currentPak;
                if (pak == null) continue;

                try
                {
                    var data = pak.Extract(item.Index);
                    var outputPath = Path.Combine(dialog.Directory, item.FileName);
                    File.WriteAllBytes(outputPath, data);
                    exported++;
                }
                catch { }
            }

            MessageBox.Show(this, $"Exported {exported} files", "Export Complete", MessageBoxType.Information);
        }

        private void OnDeleteSelected(object sender, EventArgs e)
        {
            if (_isAllIdxMode)
            {
                MessageBox.Show(this, "Delete is not supported in 'All IDX' mode.\nPlease select a specific IDX file.", "Delete", MessageBoxType.Information);
                return;
            }

            if (_currentPak == null || _fileGrid.SelectedRows.Count() == 0)
            {
                MessageBox.Show(this, "No files selected", "Delete", MessageBoxType.Information);
                return;
            }

            var count = _fileGrid.SelectedRows.Count();
            var result = MessageBox.Show(this,
                $"Are you sure you want to delete {count} file(s)?\n\nThis will modify the PAK file.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxType.Warning);

            if (result != DialogResult.Yes) return;

            try
            {
                // 收集要刪除的檔案 (從大到小排序，避免 index 變動問題)
                var itemsToDelete = _fileGrid.SelectedRows
                    .Select(row => (FileItem)_fileGrid.DataStore.ElementAt(row))
                    .OrderByDescending(item => item.Index)
                    .ToList();

                foreach (var item in itemsToDelete)
                {
                    _currentPak.Delete(item.Index);
                }

                // 儲存變更
                _currentPak.Save();

                // 重新載入檔案列表
                RefreshFileList();

                _statusLabel.Text = $"Deleted {count} file(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error deleting files: {ex.Message}", "Delete Error", MessageBoxType.Error);
            }
        }

        private void OnCopyFileName(object sender, EventArgs e)
        {
            var selected = _fileGrid.SelectedItem as FileItem;
            if (selected == null) return;

            // 格式: pakFileName#fileName (例如 tile.pak#SummonUi.xml)
            var pak = selected.SourcePak ?? _currentPak;
            var pakName = pak != null ? Path.GetFileName(pak.PakPath) : "";
            var copyText = string.IsNullOrEmpty(pakName)
                ? selected.FileName
                : $"{pakName}#{selected.FileName}";

            var clipboard = new Clipboard();
            clipboard.Text = copyText;
            _statusLabel.Text = $"Copied: {copyText}";
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
            var pak = item.SourcePak ?? _currentPak;
            if (pak == null) return;

            var tabKey = $"{pak.IdxPath}:{item.Index}";

            // Check if already open
            if (_openTabs.ContainsKey(tabKey))
            {
                _mainTabControl.SelectedPage = _openTabs[tabKey];
                return;
            }

            try
            {
                var data = pak.Extract(item.Index);
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
            var viewer = Viewers.ViewerFactory.CreateViewerSmart(ext, data, fileName);
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

            var searchBox = new TextBox { PlaceholderText = I18n.T("Placeholder.Search"), Width = 200 };
            var searchBtn = new Button { Text = I18n.T("Button.Find") };
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
                Items = { new Label { Text = I18n.T("Label.Find") }, searchBox, searchBtn }
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

        private void OnTabMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Buttons == MouseButtons.Alternate) // Right-click
            {
                // 只處理點擊在 tab 標籤區域（大約頂部 30 像素）
                // 避免與 file grid 的右鍵選單衝突
                if (e.Location.Y > 30)
                    return;

                var menu = new ContextMenu();

                var closeItem = new ButtonMenuItem { Text = I18n.T("Context.CloseTab") };
                closeItem.Click += (s, ev) =>
                {
                    if (_mainTabControl.SelectedPage != null && _mainTabControl.SelectedPage != _browserPage)
                        CloseTab(_mainTabControl.SelectedPage);
                };
                menu.Items.Add(closeItem);

                var closeOthersItem = new ButtonMenuItem { Text = I18n.T("Tab.CloseOthers") };
                closeOthersItem.Click += (s, ev) =>
                {
                    if (_mainTabControl.SelectedPage != null)
                        CloseOtherTabs(_mainTabControl.SelectedPage);
                };
                menu.Items.Add(closeOthersItem);

                var closeAllItem = new ButtonMenuItem { Text = I18n.T("Tab.CloseAll") };
                closeAllItem.Click += (s, ev) => CloseAllTabs();
                menu.Items.Add(closeAllItem);

                menu.Show(_mainTabControl);
            }
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
                _textSearchResultLabel.Text = I18n.T("Status.UseViewerSearch");
                e.Handled = true;
            }
        }

        private void OnTextSearchNext(object sender, EventArgs e)
        {
            _textSearchResultLabel.Text = I18n.T("Status.UseViewerSearch");
        }

        private void OnTextSearchPrev(object sender, EventArgs e)
        {
            _textSearchResultLabel.Text = I18n.T("Status.UseViewerSearch");
        }

        #endregion

        private void OnExportAll(object sender, EventArgs e)
        {
            IEnumerable<KeyValuePair<string, PakFile>> pakSources;
            if (_isAllIdxMode && _allPakFiles != null)
            {
                pakSources = _allPakFiles;
            }
            else if (_currentPak != null)
            {
                pakSources = new[] { new KeyValuePair<string, PakFile>(Path.GetFileName(_currentPak.IdxPath), _currentPak) };
            }
            else
            {
                MessageBox.Show(this, "No PAK file loaded", "Export", MessageBoxType.Information);
                return;
            }

            using var dialog = new SelectFolderDialog { Title = "Select Export Folder" };
            if (dialog.ShowDialog(this) != DialogResult.Ok) return;

            int exported = 0;
            int total = pakSources.Sum(kv => kv.Value.Count);
            int processed = 0;

            foreach (var kv in pakSources)
            {
                var pak = kv.Value;
                for (int i = 0; i < pak.Count; i++)
                {
                    try
                    {
                        var file = pak.Files[i];
                        var data = pak.Extract(i);
                        var outputPath = Path.Combine(dialog.Directory, file.FileName);
                        File.WriteAllBytes(outputPath, data);
                        exported++;
                    }
                    catch { }

                    processed++;
                    if (processed % 100 == 0)
                    {
                        _statusLabel.Text = $"Exporting... {processed}/{total}";
                    }
                }
            }

            _statusLabel.Text = $"Export complete: {exported} files";
            MessageBox.Show(this, $"Exported {exported} files", "Export Complete", MessageBoxType.Information);
        }

        #endregion

        #region SPR List Mode

        private async void OnOpenSprList(object sender, EventArgs e)
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

            var filePath = dialog.FileName;
            var fileName = Path.GetFileName(filePath);
            _statusLabel.Text = $"載入中: {fileName}...";

            try
            {
                // 非同步載入和解析
                var data = await Task.Run(() => File.ReadAllBytes(filePath));
                _sprListFile = await Task.Run(() => Lin.Helper.Core.Sprite.SprListParser.LoadFromBytes(data));
                _filteredSprListEntries = _sprListFile.Entries;
                _viewModeRadio.SelectedIndex = MODE_SPR_LIST;

                // 重置類型篩選器為「全部」(SPR List 模式不使用類型篩選)
                _sprTypeFilterDropDown.SelectedIndex = 0;
                _sprModeTypeFilter = null;
                _sprModeUnreferencedFilter = false;

                // Load sprite*.idx files for sprite data
                if (!string.IsNullOrEmpty(_selectedFolder))
                {
                    LoadSpriteIdxFiles(_selectedFolder);
                }

                // 建立 SprListViewer 並顯示
                ShowSprListViewer();
                UpdateModeDisplay();

                // 更新 list.spr 路徑設定和 UI（與 SPR 模式共用）
                _settings.SprModeListSprPath = filePath;
                _settings.Save();
                _sprModeListSprCheck.Checked = true;
                _sprModeListSprLabel.Text = fileName;
                _sprModeListSprLabel.TextColor = Eto.Drawing.Colors.Black;
                _sprModeListSprLabel.ToolTip = filePath;

                _statusLabel.Text = $"Loaded SPR List: {fileName} ({_sprListFile.Entries.Count} entries)";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = I18n.T("Status.LoadFailed");
                MessageBox.Show(this, I18n.T("Error.LoadSprList") + ": " + ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
            }
        }

        private void ShowSprListViewer()
        {
            // 釋放舊的 viewer
            _currentViewer?.Dispose();
            _currentViewer = null;

            // 建立簡化的 SprListViewer - 只顯示動作面板
            var viewer = new Viewers.SprListViewer();

            // 設定背景顏色
            viewer.SetBackgroundColorSettings(_settings.SprPreviewBgColor, color =>
            {
                _settings.SprPreviewBgColor = color;
                _settings.Save();
            });

            // 設定以 "spriteId-subId" 格式取得 SPR 的函數 (用於有向動畫)
            // 例如 "3225-20" 會載入 3225-20.spr
            viewer.SetSpriteByKeyProvider(spriteKey =>
            {
                // spriteKey 格式: "spriteId-subId" (例如 "3225-20")
                var sprFileName = $"{spriteKey}.spr";

                // 從所有已載入的 sprite pak 中尋找
                if (_spritePakFiles != null)
                {
                    foreach (var pak in _spritePakFiles.Values.Distinct())
                    {
                        var fileIndex = pak.Files.ToList().FindIndex(f =>
                            f.FileName.Equals(sprFileName, StringComparison.OrdinalIgnoreCase));
                        if (fileIndex >= 0)
                        {
                            return pak.Extract(fileIndex);
                        }
                    }
                }

                return null;
            });

            // 設定 SprListFile (用於產生 Pivot Table)
            viewer.SetSprListFile(_sprListFile);

            _currentViewer = viewer;
            _viewerPanel.Content = viewer.GetControl();
        }

        private void OnViewModeChanged(object sender, EventArgs e)
        {
            var newMode = _viewModeRadio.SelectedIndex;

            switch (newMode)
            {
                case MODE_NORMAL:
                    // 恢復 IDX 下拉選單
                    RestoreIdxDropDown();
                    _leftListPanel.Content = _fileGrid;
                    // 啟用相簿模式
                    if (_galleryModeRadio != null)
                        _galleryModeRadio.Enabled = true;
                    RefreshFileList();
                    break;

                case MODE_SPR:
                    // 檢查資料夾
                    if (string.IsNullOrEmpty(_selectedFolder))
                    {
                        MessageBox.Show(I18n.T("Error.SelectFolderFirst"), I18n.T("Dialog.Info"));
                        _viewModeRadio.SelectedIndex = _previousModeIndex;
                        return;
                    }
                    SetSpriteIdxMode(true);
                    LoadSprGroups(_selectedFolder);
                    _leftListPanel.Content = _sprGroupGrid;
                    // 啟用相簿模式
                    if (_galleryModeRadio != null)
                        _galleryModeRadio.Enabled = true;
                    // 重新整理相簿
                    if (_galleryModeRadio?.Checked == true)
                        RefreshRightGallery();
                    break;

                case MODE_SPR_LIST:
                    // 檢查 list.spr
                    if (string.IsNullOrEmpty(_settings.SprModeListSprPath) || !File.Exists(_settings.SprModeListSprPath))
                    {
                        OnSprModeListSprBrowse(sender, e);
                        if (string.IsNullOrEmpty(_settings.SprModeListSprPath))
                        {
                            // 使用者取消，恢復之前模式
                            _viewModeRadio.SelectedIndex = _previousModeIndex;
                            return;
                        }
                    }
                    SetSpriteIdxMode(true);
                    // 載入 SPR Groups (用於 sprite 資料查找)
                    if (_sprGroups == null || _sprGroups.Count == 0)
                    {
                        LoadSprGroups(_selectedFolder);
                    }
                    LoadListSprFile();
                    ShowSprListViewer();
                    _leftListPanel.Content = _sprListGrid;
                    // SPR List 模式不支援相簿，強制切回預覽模式
                    if (_galleryModeRadio != null)
                    {
                        _galleryModeRadio.Enabled = false;
                        if (_galleryModeRadio.Checked)
                            _previewModeRadio.Checked = true;
                    }
                    break;
            }

            _previousModeIndex = newMode;
            UpdateModeDisplay();
        }

        private void SetSpriteIdxMode(bool enabled)
        {
            if (enabled)
            {
                // 儲存原本選項
                if (_idxDropDown.SelectedIndex >= 0)
                    _originalIdxText = _idxDropDown.SelectedValue?.ToString();

                // 切換為 sprite 模式
                _idxDropDown.Items.Clear();
                _idxDropDown.Items.Add(I18n.T("SprMode.AllSprite"));
                _idxDropDown.SelectedIndex = 0;
                _idxDropDown.Enabled = false;

                // 載入 sprite*.idx
                if (!string.IsNullOrEmpty(_selectedFolder))
                    LoadSpriteIdxFiles(_selectedFolder);
            }
            else
            {
                RestoreIdxDropDown();
            }
        }

        private void LoadListSprFile()
        {
            if (string.IsNullOrEmpty(_settings.SprModeListSprPath)) return;

            try
            {
                var data = File.ReadAllBytes(_settings.SprModeListSprPath);
                _sprListFile = Lin.Helper.Core.Sprite.SprListParser.LoadFromBytes(data);
                _statusLabel.Text = $"已載入 list.spr: {_sprListFile.Entries.Count} 條目";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"載入 list.spr 失敗: {ex.Message}";
                _sprListFile = null;
            }
        }

        private void OnSprModeListSprBrowse(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = I18n.T("Dialog.OpenFile"),
                Filters = { new FileFilter("SPR List (*.txt)", ".txt", ".spr"), new FileFilter("All Files", ".*") }
            };

            if (!string.IsNullOrEmpty(_settings.LastFolder))
                dialog.Directory = new Uri(_settings.LastFolder);
            else if (!string.IsNullOrEmpty(_selectedFolder))
                dialog.Directory = new Uri(_selectedFolder);

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                var filePath = dialog.FileName;
                _settings.SprModeListSprPath = filePath;
                _settings.Save();

                _sprModeListSprCheck.Checked = true;
                _sprModeListSprLabel.Text = Path.GetFileName(filePath);
                _sprModeListSprLabel.TextColor = Eto.Drawing.Colors.Black;
                _sprModeListSprLabel.ToolTip = filePath;

                // 載入 list.spr 檔案
                try
                {
                    var data = File.ReadAllBytes(filePath);
                    _sprListFile = Lin.Helper.Core.Sprite.SprListParser.LoadFromBytes(data);
                    _statusLabel.Text = $"已載入 list.spr: {_sprListFile.Entries.Count} 條目";

                    // 如果在 SPR List 模式，刷新顯示
                    if (_viewModeRadio.SelectedIndex == MODE_SPR_LIST)
                    {
                        SetSpriteIdxMode(true);
                        ShowSprListViewer();
                        _leftListPanel.Content = _sprListGrid;
                        UpdateModeDisplay();
                    }
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"載入失敗: {ex.Message}";
                    _sprListFile = null;
                }
            }
        }

        private void RestoreIdxDropDown()
        {
            if (string.IsNullOrEmpty(_selectedFolder)) return;

            _idxDropDown.Items.Clear();
            var idxFiles = Directory.GetFiles(_selectedFolder, "*.idx", SearchOption.TopDirectoryOnly);
            if (idxFiles.Length > 1)
                _idxDropDown.Items.Add(I18n.T("Filter.All"));
            foreach (var file in idxFiles.OrderBy(f => f))
            {
                _idxDropDown.Items.Add(Path.GetFileName(file));
            }

            if (_idxDropDown.Items.Count > 0)
            {
                // 嘗試恢復原本選項
                int selectIndex = -1;
                if (!string.IsNullOrEmpty(_originalIdxText))
                {
                    selectIndex = _idxDropDown.Items.ToList().FindIndex(i => i.Text == _originalIdxText);
                }
                _idxDropDown.SelectedIndex = selectIndex >= 0 ? selectIndex : 0;
            }

            _idxDropDown.Enabled = true;
        }

        private void OnSprGroupSelected(object sender, EventArgs e)
        {
            var selected = _sprGroupGrid.SelectedItem as SprGroupItem;
            if (selected == null) return;

            // 顯示 SprGroupViewer
            ShowSprGroupViewer(selected.Group);
        }

        private void LoadSprGroups(string folder)
        {
            _sprGroups = new Dictionary<int, SprGroup>();
            _statusLabel.Text = "正在掃描 SPR 檔案...";

            // 在背景執行緒載入
            System.Threading.Tasks.Task.Run(() =>
            {
                var groups = new Dictionary<int, SprGroup>();
                var spriteIdxFiles = Directory.GetFiles(folder, "sprite*.idx", SearchOption.TopDirectoryOnly);

                foreach (var idxFile in spriteIdxFiles)
                {
                    try
                    {
                        var pak = new PakFile(idxFile);
                        for (int fileIndex = 0; fileIndex < pak.Files.Count; fileIndex++)
                        {
                            var file = pak.Files[fileIndex];
                            var name = Path.GetFileNameWithoutExtension(file.FileName);
                            if (string.IsNullOrEmpty(name)) continue;

                            // 只處理 .spr 檔案
                            if (!file.FileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                                continue;

                            int spriteId;
                            int partIndex = 0;

                            // 解析檔名格式: "123.spr" 或 "123-0.spr"
                            if (name.Contains('-'))
                            {
                                var parts = name.Split('-');
                                if (parts.Length >= 2 &&
                                    int.TryParse(parts[0], out spriteId) &&
                                    int.TryParse(parts[1], out partIndex))
                                {
                                    // 格式: xxx-xxx.spr
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else if (int.TryParse(name, out spriteId))
                            {
                                // 格式: xxx.spr
                                partIndex = 0;
                            }
                            else
                            {
                                continue;
                            }

                            // 取得或建立群組
                            if (!groups.TryGetValue(spriteId, out var group))
                            {
                                group = new SprGroup { SpriteId = spriteId };
                                groups[spriteId] = group;
                            }

                            // 檢查是否已有此 part
                            if (!group.Parts.Any(p => p.PartIndex == partIndex))
                            {
                                // 不在這裡載入 frame 數量，延遲到需要時再載入
                                group.Parts.Add(new SprPart
                                {
                                    FileName = file.FileName,
                                    PartIndex = partIndex,
                                    FrameCount = 0,  // 延遲載入
                                    SourcePak = pak,
                                    FileIndex = fileIndex
                                });
                            }
                        }
                    }
                    catch { }
                }

                // 排序 Parts
                foreach (var group in groups.Values)
                {
                    group.Parts.Sort((a, b) => a.PartIndex.CompareTo(b.PartIndex));
                }

                // 回到 UI 執行緒更新
                Application.Instance.Invoke(() =>
                {
                    _sprGroups = groups;
                    _statusLabel.Text = $"載入 {_sprGroups.Count} 個 SPR 群組";
                    UpdateSprGroupDisplay();
                });
            });
        }

        private void UpdateSprGroupDisplay()
        {
            if (_sprGroups == null) return;

            var filter = _currentFilter?.ToLowerInvariant() ?? "";
            var groups = _sprGroups.Values.AsEnumerable();

            // 如果有 list.spr，可以做類型篩選
            if (_sprListFile != null)
            {
                if (_sprModeUnreferencedFilter)
                {
                    // 篩選未被 list.spr 引用的 SPR
                    var referencedIds = _sprListFile.Entries
                        .Select(e => e.SpriteId)
                        .ToHashSet();
                    groups = groups.Where(g => !referencedIds.Contains(g.SpriteId));
                }
                else if (_sprModeTypeFilter.HasValue)
                {
                    // 篩選特定類型
                    var typeIds = _sprListFile.Entries
                        .Where(e => e.TypeId == _sprModeTypeFilter.Value)
                        .Select(e => e.SpriteId)
                        .ToHashSet();
                    groups = groups.Where(g => typeIds.Contains(g.SpriteId));
                }
            }

            // 文字搜尋篩選
            if (!string.IsNullOrEmpty(filter))
            {
                groups = groups.Where(g => g.SpriteId.ToString().Contains(filter));
            }

            var items = groups
                .OrderBy(g => g.SpriteId)
                .Select(g => new SprGroupItem
                {
                    Id = g.SpriteId,
                    Parts = g.PartsCount,
                    Frames = g.TotalFrames,
                    Group = g
                })
                .ToList();

            _sprGroupGrid.DataStore = items;
            _recordCountLabel.Text = $"{items.Count} / {_sprGroups.Count} 筆";

            // 重新整理相簿
            if (_galleryModeRadio?.Checked == true)
                RefreshRightGallery();
        }

        private void ShowSprGroupViewer(SprGroup group)
        {
            _currentViewer?.Dispose();

            var viewer = new Viewers.SprGroupViewer();
            viewer.SetBackgroundColorSettings(_settings.SprPreviewBgColor, color =>
            {
                _settings.SprPreviewBgColor = color;
                _settings.Save();
            });
            viewer.LoadGroup(group);
            _currentViewer = viewer;
            _viewerPanel.Content = viewer.GetControl();
        }

        private void ShowSprGalleryViewer()
        {
            _currentViewer?.Dispose();

            if (_sprGroups == null || _sprGroups.Count == 0) return;

            var viewer = new Viewers.SprGalleryViewer();
            viewer.LoadGroups(_sprGroups.Values.OrderBy(g => g.SpriteId).ToList());
            _currentViewer = viewer;
            _viewerPanel.Content = viewer.GetControl();
        }

        private void OnSprTypeFilterChanged(object sender, EventArgs e)
        {
            var selected = _sprTypeFilterDropDown.SelectedValue?.ToString() ?? "All Types";
            _sprModeUnreferencedFilter = false;
            _sprModeTypeFilter = null;

            if (selected == I18n.T("Filter.AllTypes"))
            {
                // No filter
            }
            else if (selected == I18n.T("Filter.Unreferenced"))
            {
                _sprModeUnreferencedFilter = true;
            }
            else
            {
                // Parse type ID from string like "10 - 怪物"
                var parts = selected.Split('-');
                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int typeId))
                {
                    _sprModeTypeFilter = typeId;
                }
            }

            UpdateSprGroupDisplay();
        }

        private void UpdateModeDisplay()
        {
            var mode = _viewModeRadio.SelectedIndex;

            // Toggle visibility of mode-specific filter controls
            // 只在 SPR 模式且有載入 list.spr 時顯示類型篩選器
            var showTypeFilter = (mode == MODE_SPR && _sprListFile != null);
            _sprTypeFilterDropDown.Visible = showTypeFilter;
            _sprTypeLabel.Visible = showTypeFilter;

            // 匯出動作矩陣按鈕: 只在有載入 SPR List 且有資料時顯示
            if (_exportPivotTableBtn != null)
            {
                _exportPivotTableBtn.Visible = _sprListFile != null && _sprListFile.Entries.Count > 0;
            }

            // 根據模式更新顯示
            switch (mode)
            {
                case MODE_SPR_LIST:
                    UpdateSprListDisplay();
                    break;
                case MODE_SPR:
                    UpdateSprGroupDisplay();
                    break;
                default:
                    RefreshFileList();
                    break;
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
                // Apply search filter only (no type filter in SPR List mode)
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

            // 預設選擇第一個條目
            if (items.Count > 0 && _sprListGrid.SelectedRow < 0)
            {
                _sprListGrid.SelectedRow = 0;
            }

            // 重新整理相簿
            if (_galleryModeRadio?.Checked == true)
                RefreshRightGallery();
        }

        private void OnSprListSelected(object sender, EventArgs e)
        {
            var selected = _sprListGrid.SelectedItem as SprListItem;
            if (selected == null) return;

            var entry = selected.Entry;

            // 使用 SprListViewer 顯示選中條目的動作
            if (_currentViewer is Viewers.SprListViewer sprListViewer)
            {
                sprListViewer.ShowEntry(entry);
            }
            else
            {
                // 如果沒有 SprListViewer，建立一個
                ShowSprListViewer();
                if (_currentViewer is Viewers.SprListViewer newViewer)
                {
                    newViewer.ShowEntry(entry);
                }
            }

            // Show entry info in status
            _statusLabel.Text = $"#{entry.Id} {entry.Name} - {entry.TypeName} ({entry.Actions.Count} actions, {entry.ImageCount} images)";
        }

        private void OnSprListSelectAll(object sender, EventArgs e)
        {
            var items = _sprListGrid.DataStore as IEnumerable<SprListItem>;
            if (items == null) return;

            foreach (var item in items)
            {
                item.IsChecked = true;
            }
            _sprListGrid.Invalidate();

            int count = items.Count(i => i.IsChecked == true);
            _statusLabel.Text = $"{I18n.T("Status.Selected")}: {count}";
        }

        private void OnSprListUnselectAll(object sender, EventArgs e)
        {
            var items = _sprListGrid.DataStore as IEnumerable<SprListItem>;
            if (items == null) return;

            foreach (var item in items)
            {
                item.IsChecked = false;
            }
            _sprListGrid.Invalidate();
            _statusLabel.Text = I18n.T("Status.SelectionCleared");
        }

        private void OnSprListDelete(object sender, EventArgs e)
        {
            var items = _sprListGrid.DataStore as IEnumerable<SprListItem>;
            if (items == null) return;

            var checkedItems = items.Where(i => i.IsChecked == true).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(I18n.T("Error.NoItemSelected"), I18n.T("Dialog.Info"));
                return;
            }

            // 確認刪除
            var result = MessageBox.Show(
                string.Format(I18n.T("Confirm.DeleteItems"), checkedItems.Count),
                I18n.T("Dialog.Confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);

            if (result != DialogResult.Yes) return;

            // 從 _sprListFile.Entries 中移除
            foreach (var item in checkedItems)
            {
                _sprListFile.Entries.Remove(item.Entry);
            }

            // 更新顯示
            UpdateSprListDisplay();
            _statusLabel.Text = string.Format(I18n.T("Status.ItemsDeleted"), checkedItems.Count);
        }

        private void OnSprListSaveAs(object sender, EventArgs e)
        {
            if (_sprListFile == null)
            {
                MessageBox.Show(I18n.T("Error.NoFileLoaded"), I18n.T("Dialog.Error"));
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = I18n.T("Dialog.SaveAs"),
                Filters = { new FileFilter(I18n.T("Filter.SprList"), ".spr") }
            };

            if (dlg.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    // 使用 SprListWriter 儲存
                    Lin.Helper.Core.Sprite.SprListWriter.SaveToFile(_sprListFile, dlg.FileName);

                    _statusLabel.Text = $"{I18n.T("Status.Saved")}: {dlg.FileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{I18n.T("Error.SaveFailed")}: {ex.Message}", I18n.T("Dialog.Error"));
                }
            }
        }

        // 攻擊動作定義 (ActionId -> 名稱) - 用於 Pivot Table 匯出
        private static readonly Dictionary<int, string> AttackActions = new Dictionary<int, string>
        {
            { 1, "空手攻擊" },
            { 5, "持劍攻擊" },
            { 12, "持斧攻擊" },
            { 21, "持弓攻擊" },
            { 25, "持矛攻擊" },
            { 41, "持杖攻擊" },
            { 47, "持匕首攻擊" },
            { 51, "持雙手劍攻擊" },
            { 55, "雙刀攻擊" },
            { 59, "持爪攻擊" },
            { 63, "持飛鏢攻擊" },
            { 84, "鎖鍊攻擊" },
            { 89, "雙斧攻擊" }
        };

        // 攻擊動作對應的武器類型 Bit 值 (ActionId -> BitValue)
        private static readonly Dictionary<int, int> AttackActionBits = new Dictionary<int, int>
        {
            { 47, 1 },     // 匕首
            { 5, 2 },      // 劍
            { 51, 4 },     // 雙手劍
            { 12, 8 },     // 斧
            { 25, 16 },    // 矛
            { 41, 32 },    // 魔杖
            { 55, 64 },    // 雙刀
            { 59, 128 },   // 鋼爪
            { 21, 256 },   // 弓
            { 84, 1024 },  // 鎖鍊劍
        };

        /// <summary>
        /// 匯出 Pivot Table (ID x 攻擊動作矩陣)
        /// </summary>
        private void OnExportPivotTable(object sender, EventArgs e)
        {
            if (_sprListFile == null || _sprListFile.Entries.Count == 0)
            {
                MessageBox.Show(I18n.T("Error.NoSprListLoaded"), I18n.T("Dialog.Error"), MessageBoxType.Error);
                return;
            }

            // 選擇儲存位置
            var dialog = new SaveFileDialog
            {
                Title = I18n.T("Dialog.SavePivotTable"),
                Filters = { new FileFilter("CSV Files", ".csv"), new FileFilter("All Files", ".*") },
                FileName = "spr_attack_pivot.csv"
            };

            if (dialog.ShowDialog(this) != DialogResult.Ok)
                return;

            try
            {
                // 只匯出有攻擊動作的條目
                var entriesWithAttacks = _sprListFile.Entries
                    .Where(entry => entry.Actions.Any(a => AttackActions.ContainsKey(a.ActionId)))
                    .OrderBy(entry => entry.Id)
                    .ToList();

                using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
                {
                    // 寫入標題列: ID, Name, SpriteId, Type, WeaponBits, 1(空手攻擊), 5(持劍攻擊), ...
                    var headerParts = new List<string> { "ID", "Name", "SpriteId", "Type", "WeaponBits" };
                    headerParts.AddRange(AttackActions.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}({kv.Value})"));
                    writer.WriteLine(string.Join(",", headerParts.Select(EscapeCsv)));

                    // 寫入每個 Entry
                    foreach (var entry in entriesWithAttacks)
                    {
                        // 計算武器類型 Bit OR 值
                        int weaponBits = 0;
                        foreach (var action in entry.Actions)
                        {
                            if (AttackActionBits.TryGetValue(action.ActionId, out int bitValue))
                            {
                                weaponBits |= bitValue;
                            }
                        }

                        var rowParts = new List<string>
                        {
                            entry.Id.ToString(),
                            entry.Name ?? "",
                            entry.SpriteId.ToString(),
                            entry.TypeName ?? "",
                            weaponBits.ToString()
                        };

                        // 每個攻擊動作欄位
                        foreach (var actionId in AttackActions.Keys.OrderBy(id => id))
                        {
                            var action = entry.Actions.FirstOrDefault(a => a.ActionId == actionId);
                            if (action != null)
                            {
                                // 有此動作: 寫 1
                                rowParts.Add("1");
                            }
                            else
                            {
                                // 無此動作: 空白
                                rowParts.Add("");
                            }
                        }

                        writer.WriteLine(string.Join(",", rowParts.Select(EscapeCsv)));
                    }
                }

                MessageBox.Show(
                    I18n.T("Status.PivotTableExported", entriesWithAttacks.Count, AttackActions.Count),
                    I18n.T("Dialog.Info"),
                    MessageBoxType.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
            }
        }

        /// <summary>
        /// CSV 欄位轉義
        /// </summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // 如果包含逗號、引號或換行，需要用引號包住並轉義內部引號
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private void OnSprGroupSelectAll(object sender, EventArgs e)
        {
            var items = _sprGroupGrid.DataStore as IEnumerable<SprGroupItem>;
            if (items == null) return;

            foreach (var item in items)
            {
                item.IsChecked = true;
            }
            _sprGroupGrid.Invalidate();

            int count = items.Count(i => i.IsChecked == true);
            _statusLabel.Text = $"{I18n.T("Status.Selected")}: {count}";
        }

        private void OnSprGroupUnselectAll(object sender, EventArgs e)
        {
            var items = _sprGroupGrid.DataStore as IEnumerable<SprGroupItem>;
            if (items == null) return;

            foreach (var item in items)
            {
                item.IsChecked = false;
            }
            _sprGroupGrid.Invalidate();
            _statusLabel.Text = I18n.T("Status.SelectionCleared");
        }

        private void OnSprGroupExport(object sender, EventArgs e)
        {
            ExportSprGroups(false);
        }

        private void OnSprGroupExportTo(object sender, EventArgs e)
        {
            ExportSprGroups(true);
        }

        private void ExportSprGroups(bool selectFolder)
        {
            // 取得要匯出的群組 (優先使用勾選的，否則使用選取的)
            var items = _sprGroupGrid.DataStore as IEnumerable<SprGroupItem>;
            if (items == null) return;

            var checkedItems = items.Where(i => i.IsChecked == true).ToList();
            if (checkedItems.Count == 0)
            {
                // 使用目前選取的項目
                var selected = _sprGroupGrid.SelectedItem as SprGroupItem;
                if (selected != null)
                    checkedItems = new List<SprGroupItem> { selected };
            }

            if (checkedItems.Count == 0)
            {
                MessageBox.Show(I18n.T("Error.NoItemSelected"), I18n.T("Dialog.Info"));
                return;
            }

            // 決定匯出目錄
            string exportPath = _selectedFolder;
            if (selectFolder)
            {
                using var dialog = new SelectFolderDialog { Title = I18n.T("Dialog.SelectExportFolder") };
                if (!string.IsNullOrEmpty(_selectedFolder))
                    dialog.Directory = _selectedFolder;
                if (dialog.ShowDialog(this) != DialogResult.Ok)
                    return;
                exportPath = dialog.Directory;
            }

            // 匯出所有選取群組的 SPR 檔案
            int exportedCount = 0;
            int errorCount = 0;

            foreach (var groupItem in checkedItems)
            {
                var group = groupItem.Group;
                if (group?.Parts == null) continue;

                foreach (var part in group.Parts)
                {
                    try
                    {
                        if (part.SourcePak == null) continue;

                        var data = part.SourcePak.Extract(part.FileIndex);
                        var outputPath = Path.Combine(exportPath, part.FileName);

                        File.WriteAllBytes(outputPath, data);
                        exportedCount++;
                    }
                    catch
                    {
                        errorCount++;
                    }
                }
            }

            _statusLabel.Text = errorCount > 0
                ? string.Format(I18n.T("Status.ExportedWithErrors"), exportedCount, errorCount)
                : string.Format(I18n.T("Status.Exported"), exportedCount);
        }

        private void OnSprGroupDelete(object sender, EventArgs e)
        {
            var items = _sprGroupGrid.DataStore as IEnumerable<SprGroupItem>;
            if (items == null) return;

            var checkedItems = items.Where(i => i.IsChecked == true).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(I18n.T("Error.NoItemSelected"), I18n.T("Dialog.Info"));
                return;
            }

            // 建立要刪除的 SPR 檔案清單
            var sprFileNames = checkedItems
                .SelectMany(g => Enumerable.Range(0, g.Group.PartsCount)
                    .Select(p => $"{g.Id}-{p}.spr"))
                .ToList();

            var message = string.Format(I18n.T("Confirm.DeleteSprFiles"),
                string.Join("\n", sprFileNames.Take(10)) +
                (sprFileNames.Count > 10 ? $"\n... {I18n.T("Status.AndMore", sprFileNames.Count - 10)}" : ""));

            // 確認刪除
            var result = MessageBox.Show(
                message,
                I18n.T("Dialog.Confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxType.Warning);

            if (result != DialogResult.Yes) return;

            // TODO: 實際刪除 SPR 檔案從 sprite*.idx
            // 目前只從顯示列表中移除
            foreach (var item in checkedItems)
            {
                _sprGroups.Remove(item.Id);
            }

            // 更新顯示
            UpdateSprGroupDisplay();
            _statusLabel.Text = string.Format(I18n.T("Status.ItemsDeleted"), checkedItems.Count);
        }

        #endregion
    }
}
