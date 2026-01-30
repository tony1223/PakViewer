using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eto.Forms;
using Eto.Drawing;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// SPR List 條目動作檢視器 - 顯示選中條目的動作列表與動畫播放
    /// 列表和篩選使用主程式左側面板，此 Viewer 只負責顯示動作和播放動畫
    /// </summary>
    public class SprListViewer : BaseViewer
    {
        public override string[] SupportedExtensions => new[] { ".sprlist" };
        public override bool CanEdit => false;

        private SprListEntry _selectedEntry;
        private SprAction _playingAction;
        private int _playingFrameIndex;
        private int _currentDirection = 0;  // 當前選擇的方向 (0-7)

        // UI
        private Scrollable _actionsPanel;
        private Drawable _previewDrawable;
        private Bitmap _currentBitmap;
        private Label _previewInfoLabel;
        private Label _entryTitleLabel;
        private Button _stopButton;
        private UITimer _animTimer;
        private Button[] _directionButtons;  // 方向選擇按鈕 (右側)

        // 縮放與背景
        private int _zoomLevel = 2;  // 預設 2x
        private int _bgColorIndex = 0;  // 0=黑, 1=紅, 2=透明, 3=白
        private DropDown _zoomDropDown;
        private DropDown _bgColorDropDown;
        private CheckBox _compositeToggle;      // 是否顯示附加/疊圖（套圖模式）
        private bool _enableCompositePreview = true;
        private Action<int> _onBgColorChanged;  // 背景色變更回呼
        private List<string> _compositeLayerOrder = new List<string>();  // 圖層繪製順序（key: "main" / "105:id" / "overlay:id"）
        private GridView _layerOrderGrid;       // 圖層順序列表
        private Panel _layerOrderPanel;         // 圖層區塊（可隱藏）
        private Button _layerMoveUpBtn;
        private Button _layerMoveDownBtn;

        // 播放速度控制
        private RadioButtonList _speedRadio;
        private NumericStepper _customSpeedInput;
        private const int SPEED_STANDARD = 0;
        private const int SPEED_SLOW = 1;
        private const int SPEED_CUSTOM = 2;

        private Slider _overlayOpacitySlider;
        private Label _overlayOpacityLabel;
        private Slider _cutoutStrengthSlider;
        private Label _cutoutStrengthLabel;

        // 動畫資料
        private SprFrame[] _currentSprFrames;
        private Func<int, byte[]> _getSpriteDataFunc;
        private Func<int, SprFrame[]> _getSpriteFramesFunc;  // 直接取得合併後的 frames
        private Func<string, byte[]> _getSpriteByKeyFunc;    // 以 "spriteId-subId" 格式取得 SPR
        private string _loadedSpriteKey;  // 目前載入的 sprite key
        private StackLayout _playingActionPanel;  // 目前播放中的動作面板

        // Pivot Table 功能 (按鈕已移至主程式工具列)
        private SprListFile _sprListFile;  // 完整的 SPR List 資料

        public SprListViewer()
        {
            BuildUI();
        }

        /// <summary>
        /// 設定背景顏色初始值和變更回呼
        /// </summary>
        public void SetBackgroundColorSettings(int initialColor, Action<int> onChanged)
        {
            _bgColorIndex = initialColor;
            _onBgColorChanged = onChanged;
            if (_bgColorDropDown != null)
            {
                _bgColorDropDown.SelectedIndex = Math.Min(_bgColorIndex, 3);
            }
            UpdatePreviewBackground();
        }

        /// <summary>
        /// 設定取得 SPR 資料的函數 (單一 part)
        /// </summary>
        public void SetSpriteDataProvider(Func<int, byte[]> getSpriteDataFunc)
        {
            _getSpriteDataFunc = getSpriteDataFunc;
        }

        /// <summary>
        /// 設定取得合併後 SprFrame[] 的函數 (所有 parts)
        /// </summary>
        public void SetSpriteFramesProvider(Func<int, SprFrame[]> getFramesFunc)
        {
            _getSpriteFramesFunc = getFramesFunc;
        }

        /// <summary>
        /// 設定以 "spriteId-subId" 格式取得 SPR 的函數 (用於有向動畫)
        /// </summary>
        public void SetSpriteByKeyProvider(Func<string, byte[]> getByKeyFunc)
        {
            _getSpriteByKeyFunc = getByKeyFunc;
        }

        /// <summary>
        /// 設定 SPR List 資料 (保留供未來使用)
        /// </summary>
        public void SetSprListFile(SprListFile sprListFile)
        {
            _sprListFile = sprListFile;
        }

        /// <summary>
        /// 顯示指定條目的動作列表
        /// </summary>
        public void ShowEntry(SprListEntry entry)
        {
            _selectedEntry = entry;
            _animTimer?.Stop();
            _playingAction = null;
            _compositeLayerOrder = new List<string>();
            BindLayerGrid();

            if (entry == null)
            {
                _entryTitleLabel.Text = I18n.T("Viewer.SelectEntry");
                _actionsPanel.Content = null;
                _currentBitmap?.Dispose();
                _currentBitmap = null;
                _previewDrawable?.Invalidate();
                _previewInfoLabel.Text = "";
                return;
            }

            // 顯示標題
            _entryTitleLabel.Text = $"#{entry.Id} {entry.Name} ({I18n.T("Grid.SpriteId")}:{entry.SpriteId}, {entry.ImageCount}{I18n.T("Viewer.Images")}, {entry.Actions.Count}{I18n.T("Grid.Actions")})";

            // 載入 SPR 資料
            LoadSpriteData(entry);

            // 顯示動作列表
            ShowActions(entry);
        }

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;
            // 此 viewer 不直接載入資料，由 ShowEntry 顯示
        }

        private void BuildUI()
        {
            // 條目標題
            _entryTitleLabel = new Label
            {
                Text = I18n.T("Viewer.SelectEntry"),
                Font = new Font(SystemFont.Bold, 12),
                TextColor = Colors.DarkBlue
            };

            // 動作面板
            _actionsPanel = new Scrollable
            {
                BackgroundColor = Eto.Drawing.Color.FromArgb(250, 250, 252)
            };

            // 預覽區元件
            _previewDrawable = new Drawable
            {
                Size = new Eto.Drawing.Size(200, 200),
                BackgroundColor = Colors.Black
            };
            _previewDrawable.Paint += OnPreviewPaint;

            _previewInfoLabel = new Label { Text = "", TextColor = Colors.White };
            _stopButton = new Button { Text = I18n.T("Button.Stop"), Width = 80 };
            _stopButton.Click += OnStopClick;

            // 縮放選擇
            _zoomDropDown = new DropDown();
            _zoomDropDown.Items.Add("1x");
            _zoomDropDown.Items.Add("2x");
            _zoomDropDown.SelectedIndex = _zoomLevel - 1;
            _zoomDropDown.SelectedIndexChanged += (s, e) =>
            {
                _zoomLevel = _zoomDropDown.SelectedIndex + 1;
                _previewDrawable?.Invalidate();
            };

            // 背景顏色選擇
            _bgColorDropDown = new DropDown();
            _bgColorDropDown.Items.Add(I18n.T("Color.Black"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Red"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Transparent"));
            _bgColorDropDown.Items.Add(I18n.T("Color.White"));
            _bgColorDropDown.SelectedIndex = Math.Min(_bgColorIndex, 3);
            _bgColorDropDown.SelectedIndexChanged += (s, e) =>
            {
                _bgColorIndex = _bgColorDropDown.SelectedIndex;
                UpdatePreviewBackground();
                _onBgColorChanged?.Invoke(_bgColorIndex);
            };

            var zoomBgPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items =
                {
                    new Label { Text = I18n.T("Label.Zoom"), TextColor = Colors.White, VerticalAlignment = VerticalAlignment.Center },
                    _zoomDropDown,
                    new Label { Text = I18n.T("Label.Background"), TextColor = Colors.White, VerticalAlignment = VerticalAlignment.Center },
                    _bgColorDropDown
                }
            };

            // 套圖模式：含附加/疊圖
            _compositeToggle = new CheckBox
            {
                Text = "含附加/疊圖",
                Checked = true,
                TextColor = Colors.White
            };
            _enableCompositePreview = true;
            _compositeToggle.CheckedChanged += (s, e) =>
            {
                _enableCompositePreview = _compositeToggle.Checked == true;
                if (_layerOrderPanel != null) _layerOrderPanel.Visible = _enableCompositePreview;
                _previewDrawable?.Invalidate();
            };

            // 圖層順序（可調整上下關係）+ 疊圖不透明度
            _layerOrderGrid = new GridView
            {
                Size = new Eto.Drawing.Size(180, 80),
                ShowHeader = false,
                DataStore = new List<object>()
            };
            _layerOrderGrid.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<LayerRow, string>(r => r.DisplayName) },
                AutoSize = true
            });
            _layerMoveUpBtn = new Button { Text = "▲ 上移", Width = 70 };
            _layerMoveUpBtn.Click += (s, e) => { MoveLayerOrder(-1); };
            _layerMoveDownBtn = new Button { Text = "▼ 下移", Width = 70 };
            _layerMoveDownBtn.Click += (s, e) => { MoveLayerOrder(1); };
            _overlayOpacitySlider = new Slider
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 75,
                Width = 100
            };
            _overlayOpacityLabel = new Label { Text = "75%", TextColor = Colors.White, Width = 28, TextAlignment = TextAlignment.Right };
            _overlayOpacitySlider.ValueChanged += (s, e) =>
            {
                var v = _overlayOpacitySlider.Value;
                _overlayOpacityLabel.Text = v + "%";
                UpdateAnimationFrame();
            };

            _cutoutStrengthSlider = new Slider
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 50,
                Width = 100
            };
            _cutoutStrengthLabel = new Label { Text = "50", TextColor = Colors.White, Width = 28, TextAlignment = TextAlignment.Right };
            _cutoutStrengthSlider.ValueChanged += (s, e) =>
            {
                var v = _cutoutStrengthSlider.Value;
                _cutoutStrengthLabel.Text = v.ToString();
                UpdateAnimationFrame();
            };

            _layerOrderPanel = new Panel
            {
                Content = new StackLayout
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 4,
                    Items =
                    {
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Items =
                            {
                                new Label { Text = "疊圖不透明度", TextColor = Colors.White, Font = new Font(SystemFont.Default, 8), Width = 84 },
                                _overlayOpacitySlider,
                                _overlayOpacityLabel
                            }
                        },
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Items =
                            {
                                new Label { Text = "去背強度", TextColor = Colors.White, Font = new Font(SystemFont.Default, 8), Width = 84 },
                                _cutoutStrengthSlider,
                                _cutoutStrengthLabel
                            }
                        },
                        new Label { Text = "圖層順序（下→上）", TextColor = Colors.White, Font = new Font(SystemFont.Default, 8) },
                        _layerOrderGrid,
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Items = { _layerMoveUpBtn, _layerMoveDownBtn }
                        }
                    }
                }
            };
            var layerOrderPanel = _layerOrderPanel;

            var compositeAndLayerSection = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 4,
                Items = { _compositeToggle, layerOrderPanel }
            };

            // 方向選擇器 (右側)
            var directionSelector = CreateDirectionSelector();
            var directionPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    new Label { Text = I18n.T("Viewer.Direction") + ":", TextColor = Colors.White },
                    directionSelector
                }
            };

            // 播放速度選擇 (垂直排列避免截斷)
            _speedRadio = new RadioButtonList
            {
                Orientation = Orientation.Vertical,
                Spacing = new Eto.Drawing.Size(0, 2),
                TextColor = Colors.White
            };
            _speedRadio.Items.Add(I18n.T("Speed.Standard"));
            _speedRadio.Items.Add(I18n.T("Speed.Slow"));
            _speedRadio.Items.Add(I18n.T("Speed.Custom"));
            _speedRadio.SelectedIndex = SPEED_STANDARD;

            _customSpeedInput = new NumericStepper
            {
                MinValue = 1,
                MaxValue = 1000,
                Value = 100,
                Width = 60,
                Enabled = false
            };
            _speedRadio.SelectedIndexChanged += (s, e) =>
            {
                _customSpeedInput.Enabled = (_speedRadio.SelectedIndex == SPEED_CUSTOM);
            };

            var customSpeedRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = { _customSpeedInput, new Label { Text = "ms", TextColor = Colors.White } }
            };

            var speedPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 3,
                Items =
                {
                    new Label { Text = I18n.T("Viewer.Speed") + ":", TextColor = Colors.White },
                    _speedRadio,
                    customSpeedRow
                }
            };

            // 右側預覽區
            var previewPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = 10,
                Spacing = 8,
                BackgroundColor = Eto.Drawing.Color.FromArgb(80, 80, 80),
                Items =
                {
                    new Label { Text = I18n.T("Viewer.AnimPreview"), TextColor = Colors.White, Font = new Font(SystemFont.Bold) },
                    zoomBgPanel,
                    directionPanel,
                    speedPanel,
                    _previewDrawable,
                    _previewInfoLabel,
                    _stopButton,
                    compositeAndLayerSection
                }
            };

            // 主佈局 - 左側動作列表，右側預覽
            var splitter = new Splitter
            {
                Panel1 = new StackLayout
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 5,
                    Padding = 5,
                    Items =
                    {
                        _entryTitleLabel,
                        new StackLayoutItem(_actionsPanel, true)
                    }
                },
                Panel2 = previewPanel,
                Position = 500,
                Orientation = Orientation.Horizontal
            };

            _control = splitter;

            // 動畫計時器
            _animTimer = new UITimer { Interval = 0.1 };
            _animTimer.Elapsed += OnAnimationTick;
        }

        private void LoadSpriteData(SprListEntry entry)
        {
            _currentSprFrames = null;

            // 優先使用 frames provider (載入所有 parts)
            if (_getSpriteFramesFunc != null)
            {
                try
                {
                    _currentSprFrames = _getSpriteFramesFunc(entry.SpriteId);
                }
                catch { }
            }

            // 備用: 使用 data provider (單一 part)
            if (_currentSprFrames == null && _getSpriteDataFunc != null)
            {
                try
                {
                    var sprData = _getSpriteDataFunc(entry.SpriteId);
                    if (sprData != null)
                    {
                        _currentSprFrames = SprReader.Load(sprData);
                    }
                }
                catch { }
            }
        }

        private void ShowActions(SprListEntry entry)
        {
            if (entry.Actions.Count == 0)
            {
                _actionsPanel.Content = new Label
                {
                    Text = I18n.T("Viewer.NoActions"),
                    TextColor = Colors.Gray,
                    TextAlignment = TextAlignment.Center
                };
                return;
            }

            var layout = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                Padding = 10
            };

            foreach (var action in entry.Actions)
            {
                var actionPanel = CreateActionPanel(entry, action);
                layout.Items.Add(actionPanel);
            }

            _actionsPanel.Content = layout;
        }

        private Panel CreateActionPanel(SprListEntry entry, SprAction action)
        {
            // 動作標題
            var titleLabel = new Label
            {
                Text = $"{action.DisplayName}",
                Font = new Font(SystemFont.Bold)
            };

            // 播放按鈕
            var playBtn = new Button { Text = I18n.T("Button.Play"), Width = 70 };
            StackLayout panelRef = null;  // 稍後設定
            playBtn.Click += (s, e) => PlayAction(entry, action, panelRef);

            // 動作資訊
            var directionText = action.Directional == 1 ? I18n.T("Viewer.Directional") : I18n.T("Viewer.NonDirectional");
            int totalDuration = action.Frames.Sum(f => f.Duration);
            var infoLabel = new Label
            {
                Text = $"{directionText} | {action.Frames.Count}{I18n.T("Viewer.Frames")} | {I18n.T("Viewer.TotalDurationValue")}:{totalDuration}",
                TextColor = Colors.Gray
            };

            // 主內容面板
            var contentPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 3
            };

            // 幀格預覽
            var framesPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };

            foreach (var frame in action.Frames.Take(10))  // 只顯示前 10 幀
            {
                var frameBox = CreateFrameBox(frame);
                framesPanel.Items.Add(frameBox);
            }

            if (action.Frames.Count > 10)
            {
                framesPanel.Items.Add(new Label { Text = $"...+{action.Frames.Count - 10}", TextColor = Colors.Gray });
            }

            contentPanel.Items.Add(framesPanel);

            // 組合
            var panel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 3,
                Padding = 5,
                BackgroundColor = Colors.White,
                Items =
                {
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items = { titleLabel, playBtn }
                    },
                    infoLabel,
                    contentPanel
                }
            };

            // 設定 panelRef 供按鈕點擊時使用
            panelRef = panel;

            return panel;
        }

        /// <summary>
        /// 建立方向選擇器 (3x3 格子)
        /// 方向配置:
        ///   0  1  2
        ///   7     3
        ///   6  5  4
        /// </summary>
        private Control CreateDirectionSelector()
        {
            _directionButtons = new Button[8];
            var btnSize = new Eto.Drawing.Size(24, 24);

            // 建立 3x3 表格
            var grid = new TableLayout
            {
                Spacing = new Eto.Drawing.Size(1, 1),
                Padding = 2
            };

            // 方向對應: 行列 -> 方向編號
            // (0,0)=0, (0,1)=1, (0,2)=2
            // (1,0)=7, (1,1)=X, (1,2)=3
            // (2,0)=6, (2,1)=5, (2,2)=4
            int[,] dirMap = {
                { 0, 1, 2 },
                { 7, -1, 3 },
                { 6, 5, 4 }
            };

            for (int row = 0; row < 3; row++)
            {
                var tableRow = new TableRow();
                for (int col = 0; col < 3; col++)
                {
                    int dir = dirMap[row, col];
                    if (dir >= 0)
                    {
                        var btn = new Button
                        {
                            Text = dir.ToString(),
                            Size = btnSize,
                            BackgroundColor = (dir == _currentDirection) ? Colors.LightBlue : Colors.White
                        };
                        int capturedDir = dir;
                        btn.Click += (s, e) => OnDirectionSelected(capturedDir);
                        _directionButtons[dir] = btn;
                        tableRow.Cells.Add(new TableCell(btn));
                    }
                    else
                    {
                        // 中心空格
                        tableRow.Cells.Add(new TableCell(new Panel { Size = btnSize }));
                    }
                }
                grid.Rows.Add(tableRow);
            }

            return grid;
        }

        private void OnDirectionSelected(int direction)
        {
            _currentDirection = direction;

            // 更新按鈕顏色
            if (_directionButtons != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (_directionButtons[i] != null)
                    {
                        _directionButtons[i].BackgroundColor = (i == _currentDirection) ? Colors.LightBlue : Colors.White;
                    }
                }
            }

            // 如果正在播放，立即更新當前幀 (不等待 timer)
            if (_playingAction != null && _playingAction.Frames.Count > 0)
            {
                // 重新計算並顯示當前幀
                _playingFrameIndex = 0;  // 重置到第一幀
                UpdateAnimationFrame();
            }
        }

        private Panel CreateFrameBox(SprActionFrame frame)
        {
            var bgColor = GetFrameColor(frame);

            // 內容面板
            var content = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = 2,
                BackgroundColor = bgColor,
                Size = new Eto.Drawing.Size(50, 36)
            };

            // 第一行: ImageId.FrameIndex + 修飾符 (舊版格式)
            var modifiers = GetShortModifiers(frame);
            var topLine = $"{frame.ImageId}.{frame.FrameIndex}";
            if (!string.IsNullOrEmpty(modifiers))
            {
                topLine += modifiers;
            }

            content.Items.Add(new Label
            {
                Text = topLine,
                Font = new Font(SystemFont.Default, 7),
                TextColor = string.IsNullOrEmpty(modifiers) ? Colors.Black : Colors.DarkRed
            });

            // 第二行: :Duration (舊版格式)
            content.Items.Add(new Label
            {
                Text = $":{frame.Duration}",
                Font = new Font(SystemFont.Default, 7),
                TextColor = Colors.DarkGray
            });

            // 外框 (模擬 BorderStyle.FixedSingle)
            var border = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = 1,
                BackgroundColor = Colors.Gray,
                Items = { content }
            };

            return border;
        }

        private Eto.Drawing.Color GetFrameColor(SprActionFrame frame)
        {
            if (frame.TriggerHit)
                return Eto.Drawing.Color.FromArgb(255, 200, 200);  // 淡紅色 - 命中
            if (frame.SoundIds.Count > 0)
                return Eto.Drawing.Color.FromArgb(200, 255, 200);  // 淡綠色 - 聲音
            if (frame.OverlayIds.Count > 0)
                return Eto.Drawing.Color.FromArgb(200, 200, 255);  // 淡藍色 - 疊圖
            if (frame.EffectIds.Count > 0)
                return Eto.Drawing.Color.FromArgb(255, 255, 200);  // 淡黃色 - 特效
            return Eto.Drawing.Color.FromArgb(245, 245, 245);     // 淡灰色 - 一般
        }

        private string GetShortModifiers(SprActionFrame frame)
        {
            var parts = new List<string>();
            if (frame.TriggerHit) parts.Add("!");
            // 顯示實際 ID (舊版格式)
            if (frame.SoundIds.Count > 0)
                parts.Add($"[{string.Join(",", frame.SoundIds)}");   // 聲音
            if (frame.OverlayIds.Count > 0)
                parts.Add($"]{string.Join(",", frame.OverlayIds)}"); // 疊圖
            if (frame.EffectIds.Count > 0)
                parts.Add($"<{string.Join(",", frame.EffectIds)}");  // 特效
            return string.Join("", parts);  // 不加空格，緊湊顯示
        }

        private void PlayAction(SprListEntry entry, SprAction action, StackLayout actionPanel = null)
        {
            _animTimer.Stop();
            _playingAction = action;
            _playingFrameIndex = 0;
            _loadedSpriteKey = null;  // 重置，讓 UpdateAnimationFrame 重新載入

            // 更新動作面板高亮
            if (_playingActionPanel != null)
            {
                _playingActionPanel.BackgroundColor = Colors.White;
            }
            _playingActionPanel = actionPanel;
            if (_playingActionPanel != null)
            {
                _playingActionPanel.BackgroundColor = Eto.Drawing.Color.FromArgb(200, 230, 255);  // 淡藍色
            }

            if (action.Frames.Count == 0)
            {
                _previewInfoLabel.Text = I18n.T("Viewer.NoFrames");
                return;
            }

            RefreshLayerOrderGrid(entry, action);
            UpdateAnimationFrame();
            _animTimer.Start();
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            if (_playingAction == null || _playingAction.Frames.Count == 0)
            {
                _animTimer.Stop();
                return;
            }

            _playingFrameIndex++;
            if (_playingFrameIndex >= _playingAction.Frames.Count)
                _playingFrameIndex = 0;

            UpdateAnimationFrame();
        }

        private void UpdateAnimationFrame()
        {
            if (_playingAction == null || _playingAction.Frames.Count == 0)
                return;

            var frame = _playingAction.Frames[_playingFrameIndex];

            // 設定下一幀間隔
            double interval = GetFrameInterval(frame.Duration);
            _animTimer.Interval = Math.Max(0.02, interval);

            // 計算 subId: ImageId + direction (有向) 或 ImageId (無向)
            // 舊版格式: {SpriteId}-{subId}.spr 每個方向是獨立的 SPR 檔案
            int direction = _playingAction.IsDirectional ? _currentDirection : 0;
            int subId = frame.ImageId + direction;
            string spriteKey = $"{_selectedEntry?.SpriteId}-{subId}";

            // 如果 sprite key 改變了，重新載入
            if (_loadedSpriteKey != spriteKey)
            {
                LoadSpriteByKey(spriteKey);
            }

            // 實際幀索引就是 FrameIndex
            int actualIndex = frame.FrameIndex;

            // 顯示圖片（套圖模式：有 105 或 OverlayIds 時可合成）
            if (_currentSprFrames != null && _currentSprFrames.Length > 0)
            {
                if (actualIndex >= 0 && actualIndex < _currentSprFrames.Length)
                {
                    var sprFrame = _currentSprFrames[actualIndex];
                    if (sprFrame.Image != null)
                    {
                        _currentBitmap?.Dispose();
                        Bitmap nextBitmap = null;
                        if (_enableCompositePreview)
                        {
                            var attachIds = GetAttachSpriteIds(_selectedEntry);
                            bool hasOverlay = frame.OverlayIds != null && frame.OverlayIds.Count > 0;
                            Image<Rgba32> composite = (attachIds.Count > 0 || hasOverlay)
                                ? BuildCompositeFrame(sprFrame, subId, actualIndex, frame)
                                : null;
                            if (composite != null)
                            {
                                try { nextBitmap = ConvertToEtoBitmap(composite); }
                                finally { composite?.Dispose(); }
                            }
                            else
                                nextBitmap = ConvertToEtoBitmap(sprFrame.Image);
                        }
                        else
                            nextBitmap = ConvertToEtoBitmap(sprFrame.Image);
                        _currentBitmap = nextBitmap;
                        _previewDrawable?.Invalidate();
                    }
                }

                // 顯示格式: SpriteId-subId #{FrameIndex}
                var sprRef = $"{_selectedEntry?.SpriteId}-{subId} #{actualIndex}";
                var directionText = _playingAction.IsDirectional ? $" Dir:{_currentDirection}" : "";
                _previewInfoLabel.Text = $"{sprRef}\n" +
                                         $"{I18n.T("Viewer.Frame", _playingFrameIndex + 1, _playingAction.Frames.Count)}{directionText}\n" +
                                         $"SPR: {_currentSprFrames.Length}{I18n.T("Viewer.Frames")}";
            }
            else
            {
                // 沒有圖片資料
                var sprRef = $"{_selectedEntry?.SpriteId}-{subId} #{actualIndex}";
                var directionText = _playingAction.IsDirectional ? $" Dir:{_currentDirection}" : "";
                _previewInfoLabel.Text = $"{sprRef}\n" +
                                         $"{I18n.T("Viewer.Frame", _playingFrameIndex + 1, _playingAction.Frames.Count)}{directionText}\n" +
                                         $"({I18n.T("Viewer.NoImageData")})";
            }
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            if (_currentBitmap == null) return;

            var g = e.Graphics;
            var drawableSize = _previewDrawable.Size;

            float scale = Math.Min((float)(drawableSize.Width - 10) / _currentBitmap.Width, (float)(drawableSize.Height - 10) / _currentBitmap.Height);
            scale = Math.Min(scale, _zoomLevel);

            float w = _currentBitmap.Width * scale;
            float h = _currentBitmap.Height * scale;
            float x = (drawableSize.Width - w) / 2;
            float y = (drawableSize.Height - h) / 2;

            g.DrawImage(_currentBitmap, x, y, w, h);
        }

        private void UpdatePreviewBackground()
        {
            if (_previewDrawable == null) return;
            _previewDrawable.BackgroundColor = _bgColorIndex switch
            {
                1 => Colors.Red,
                2 => Colors.Transparent,
                3 => Colors.White,
                _ => Colors.Black
            };
            _previewDrawable.Invalidate();
        }

        /// <summary>
        /// 以 spriteKey 格式載入 SPR (例如 "3225-20")
        /// </summary>
        private void LoadSpriteByKey(string spriteKey)
        {
            _currentSprFrames = null;
            _loadedSpriteKey = null;

            if (_getSpriteByKeyFunc != null)
            {
                try
                {
                    var sprData = _getSpriteByKeyFunc(spriteKey);
                    if (sprData != null)
                    {
                        _currentSprFrames = SprReader.Load(sprData);
                        _loadedSpriteKey = spriteKey;
                    }
                }
                catch { }
            }
        }

        private Bitmap ConvertToEtoBitmap(Image<Rgba32> image)
        {
            using var ms = new System.IO.MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }

        /// <summary>依 spriteKey 載入 SPR 的幀陣列（用於套圖）</summary>
        private SprFrame[] LoadSpriteFramesByKey(string spriteKey)
        {
            if (_getSpriteByKeyFunc == null) return null;
            try
            {
                var sprData = _getSpriteByKeyFunc(spriteKey);
                if (sprData != null)
                    return SprReader.Load(sprData);
            }
            catch { }
            return null;
        }

        /// <summary>取得條目的 105 (attach) 附加圖 sprite ID 列表</summary>
        private static List<int> GetAttachSpriteIds(SprListEntry entry)
        {
            var list = new List<int>();
            if (entry?.Attributes == null) return list;
            foreach (var attr in entry.Attributes)
            {
                if (attr.AttributeId != 105 || attr.Parameters == null) continue;
                for (int i = 1; i < attr.Parameters.Count; i++)
                {
                    if (int.TryParse(attr.Parameters[i], out int sid) && sid > 0)
                        list.Add(sid);
                }
            }
            return list;
        }

        /// <summary>依目前圖層順序取得當前幀的圖層 key 列表</summary>
        private List<string> GetOrderedLayerKeysForFrame(SprActionFrame listFrame)
        {
            var currentKeys = new List<string> { "main" };
            foreach (var sid in GetAttachSpriteIds(_selectedEntry))
                currentKeys.Add($"105:{sid}");
            if (listFrame?.OverlayIds != null)
                foreach (var oid in listFrame.OverlayIds)
                    currentKeys.Add($"overlay:{oid}");
            var ordered = new List<string>();
            foreach (var k in _compositeLayerOrder)
                if (currentKeys.Contains(k)) ordered.Add(k);
            foreach (var k in currentKeys)
                if (!ordered.Contains(k)) ordered.Add(k);
            return ordered;
        }

        /// <summary>將單一圖層 key 解析為 SprFrame</summary>
        private SprFrame? ResolveLayerKey(string layerKey, SprFrame mainFrame, int subId, int actualIndex)
        {
            if (layerKey == "main") return mainFrame;
            if (layerKey.StartsWith("105:") && int.TryParse(layerKey.Substring(4), out int spriteId))
            {
                var frames = LoadSpriteFramesByKey($"{spriteId}-{subId}");
                if (frames != null && actualIndex >= 0 && actualIndex < frames.Length)
                    return frames[actualIndex];
            }
            if (layerKey.StartsWith("overlay:") && int.TryParse(layerKey.Substring(8), out spriteId))
            {
                var frames = LoadSpriteFramesByKey($"{spriteId}-{subId}");
                if (frames != null && actualIndex >= 0 && actualIndex < frames.Length)
                    return frames[actualIndex];
            }
            return null;
        }

        /// <summary>將 105 附加圖 + 疊圖 + 主體（底圖最上）依圖層順序合成為一張圖</summary>
        private Image<Rgba32> BuildCompositeFrame(SprFrame mainFrame, int subId, int actualIndex, SprActionFrame listFrame)
        {
            var orderedKeys = GetOrderedLayerKeysForFrame(listFrame);
            var layers = new List<(SprFrame frame, string key)>();
            foreach (var key in orderedKeys)
            {
                var f = ResolveLayerKey(key, mainFrame, subId, actualIndex);
                if (f.HasValue && f.Value.Image != null)
                    layers.Add((f.Value, key));
            }
            if (layers.Count <= 1) return null;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (f, _) in layers)
            {
                if (f.Image == null) continue;
                minX = Math.Min(minX, f.XOffset);
                minY = Math.Min(minY, f.YOffset);
                maxX = Math.Max(maxX, f.XOffset + f.Width);
                maxY = Math.Max(maxY, f.YOffset + f.Height);
            }
            int canvasW = Math.Max(1, maxX - minX);
            int canvasH = Math.Max(1, maxY - minY);
            Rgba32 bgFill = GetBackgroundRgba32();
            var canvas = new Image<Rgba32>(canvasW, canvasH, bgFill);
            float overlayOpacity = _overlayOpacitySlider != null ? (float)_overlayOpacitySlider.Value / 100f : 0.75f;
            int cutoutStrength = _cutoutStrengthSlider != null ? _cutoutStrengthSlider.Value : 50;
            var transparent = new Rgba32(0, 0, 0, 0);
            canvas.Mutate(ctx =>
            {
                foreach (var (f, key) in layers)
                {
                    if (f.Image == null) continue;
                    int dx = f.XOffset - minX;
                    int dy = f.YOffset - minY;
                    if (key == "main")
                    {
                        ctx.DrawImage(f.Image, new SixLabors.ImageSharp.Point(dx, dy), 1f);
                    }
                    else
                    {
                        using (var layerImg = f.Image.Clone())
                        {
                            MakeNearBlackTransparent(layerImg, transparent, cutoutStrength);
                            MakeNearWhiteTransparent(layerImg, transparent, cutoutStrength);
                            ScaleOverlayAlpha(layerImg, overlayOpacity);
                            ctx.DrawImage(layerImg, new SixLabors.ImageSharp.Point(dx, dy), 1f);
                        }
                    }
                }
            });
            return canvas;
        }

        /// <summary>僅縮放疊圖 alpha，不碰 RGB，避免整體顏色被稀釋。</summary>
        private static void ScaleOverlayAlpha(Image<Rgba32> image, float opacity)
        {
            byte op = (byte)Math.Max(0, Math.Min(255, (int)(opacity * 255f)));
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        ref var p = ref row[x];
                        p.A = (byte)((p.A * op) / 255);
                    }
                }
            });
        }

        private Rgba32 GetBackgroundRgba32()
        {
            return _bgColorIndex switch
            {
                1 => new Rgba32(255, 0, 0, 255),
                2 => new Rgba32(0, 0, 0, 0),
                3 => new Rgba32(255, 255, 255, 255),
                _ => new Rgba32(0, 0, 0, 255)
            };
        }

        /// <summary>近黑視為透明。strength 0~100，愈大去背愈強。</summary>
        private static void MakeNearBlackTransparent(Image<Rgba32> image, Rgba32 backgroundFill, int strength = 50)
        {
            int darkThreshold = 25 + (strength * 85 / 100);
            int grayTolerance = 5 + (strength * 45 / 100);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        ref var p = ref row[x];
                        int max = Math.Max(p.R, Math.Max(p.G, p.B));
                        int min = Math.Min(p.R, Math.Min(p.G, p.B));
                        if (max <= darkThreshold && (max - min) <= grayTolerance)
                            p = backgroundFill;
                    }
                }
            });
        }

        /// <summary>近白／淺灰視為透明。strength 0~100，愈大去背愈強。</summary>
        private static void MakeNearWhiteTransparent(Image<Rgba32> image, Rgba32 backgroundFill, int strength = 50)
        {
            int lightThreshold = 255 - (strength * 55 / 100);
            int grayTolerance = 5 + (strength * 25 / 100);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        ref var p = ref row[x];
                        if (p.A == 0) continue;
                        int min = Math.Min(p.R, Math.Min(p.G, p.B));
                        int max = Math.Max(p.R, Math.Max(p.G, p.B));
                        if (min >= lightThreshold && (max - min) <= grayTolerance)
                            p = backgroundFill;
                    }
                }
            });
        }

        private List<string> GetLayerKeysForAction(SprListEntry entry, SprAction action)
        {
            var keys = new List<string> { "main" };
            foreach (var sid in GetAttachSpriteIds(entry))
                keys.Add($"105:{sid}");
            if (action?.Frames != null)
            {
                foreach (var f in action.Frames)
                {
                    if (f.OverlayIds == null) continue;
                    foreach (var oid in f.OverlayIds)
                        if (!keys.Contains($"overlay:{oid}"))
                            keys.Add($"overlay:{oid}");
                }
            }
            return keys;
        }

        private static string GetLayerDisplayName(string key)
        {
            if (key == "main") return "主體";
            if (key.StartsWith("105:")) return "105: " + key.Substring(4);
            if (key.StartsWith("overlay:")) return "疊圖: " + key.Substring(8);
            return key;
        }

        private void RefreshLayerOrderGrid(SprListEntry entry, SprAction action)
        {
            var keys = GetLayerKeysForAction(entry, action);
            if (keys.Count <= 1) { _compositeLayerOrder = new List<string>(keys); BindLayerGrid(); return; }
            var defaultOrder = new List<string>();
            foreach (var k in keys)
                if (k != "main") defaultOrder.Add(k);
            defaultOrder.Add("main");
            bool useDefault = _compositeLayerOrder.Count == 0 || keys.Count != _compositeLayerOrder.Count;
            if (!useDefault)
            {
                foreach (var k in keys)
                    if (!_compositeLayerOrder.Contains(k)) { useDefault = true; break; }
            }
            if (useDefault)
                _compositeLayerOrder = new List<string>(defaultOrder);
            else
            {
                var merged = new List<string>();
                foreach (var k in _compositeLayerOrder)
                    if (keys.Contains(k)) merged.Add(k);
                foreach (var k in keys)
                    if (!merged.Contains(k)) merged.Add(k);
                _compositeLayerOrder = merged;
            }
            BindLayerGrid();
        }

        private void BindLayerGrid()
        {
            if (_layerOrderGrid == null) return;
            var rows = _compositeLayerOrder.Select(k => new LayerRow { Key = k, DisplayName = GetLayerDisplayName(k) }).ToList();
            _layerOrderGrid.DataStore = rows;
        }

        private void MoveLayerOrder(int delta)
        {
            if (_compositeLayerOrder.Count <= 1) return;
            int idx = _layerOrderGrid.SelectedRow;
            if (idx < 0) return;
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= _compositeLayerOrder.Count) return;
            var tmp = _compositeLayerOrder[idx];
            _compositeLayerOrder[idx] = _compositeLayerOrder[newIdx];
            _compositeLayerOrder[newIdx] = tmp;
            BindLayerGrid();
            _layerOrderGrid.SelectedRow = newIdx;
            UpdateAnimationFrame();
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            _animTimer.Stop();
            _playingAction = null;
            _previewInfoLabel.Text = I18n.T("Viewer.Stopped");

            // 清除動作面板高亮
            if (_playingActionPanel != null)
            {
                _playingActionPanel.BackgroundColor = Colors.White;
                _playingActionPanel = null;
            }
        }

        /// <summary>
        /// 根據播放速度設定計算幀間隔
        /// </summary>
        private double GetFrameInterval(int duration)
        {
            switch (_speedRadio.SelectedIndex)
            {
                case SPEED_STANDARD:
                    // 1/24 秒 ≈ 41.67ms，乘以 duration
                    return duration * (1.0 / 24.0);
                case SPEED_SLOW:
                    // 10/24 秒 ≈ 416.67ms，乘以 duration
                    return duration * (10.0 / 24.0);
                case SPEED_CUSTOM:
                    // 自訂毫秒值
                    return _customSpeedInput.Value / 1000.0;
                default:
                    return duration * (1.0 / 24.0);
            }
        }

        public override void Dispose()
        {
            _animTimer?.Stop();
            _animTimer?.Dispose();
            _currentBitmap?.Dispose();
            _currentBitmap = null;
            base.Dispose();
        }
    }

    /// <summary>圖層順序列表的一列（供 GridView 綁定）</summary>
    internal class LayerRow
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
    }
}
