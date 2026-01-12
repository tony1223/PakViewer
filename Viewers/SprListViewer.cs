using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        private ImageView _previewImage;
        private Label _previewInfoLabel;
        private Label _entryTitleLabel;
        private Button _stopButton;
        private UITimer _animTimer;
        private Button[] _directionButtons;  // 方向選擇按鈕

        // 動畫資料
        private SprFrame[] _currentSprFrames;
        private Func<int, byte[]> _getSpriteDataFunc;

        public SprListViewer()
        {
            BuildUI();
        }

        /// <summary>
        /// 設定取得 SPR 資料的函數
        /// </summary>
        public void SetSpriteDataProvider(Func<int, byte[]> getSpriteDataFunc)
        {
            _getSpriteDataFunc = getSpriteDataFunc;
        }

        /// <summary>
        /// 顯示指定條目的動作列表
        /// </summary>
        public void ShowEntry(SprListEntry entry)
        {
            _selectedEntry = entry;
            _animTimer?.Stop();
            _playingAction = null;

            if (entry == null)
            {
                _entryTitleLabel.Text = I18n.T("Viewer.SelectEntry");
                _actionsPanel.Content = null;
                _previewImage.Image = null;
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

            // 預覽區
            _previewImage = new ImageView { Size = new Eto.Drawing.Size(200, 200) };
            _previewInfoLabel = new Label { Text = "", TextColor = Colors.White };
            _stopButton = new Button { Text = I18n.T("Button.Stop"), Width = 80 };
            _stopButton.Click += OnStopClick;

            var previewPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = 10,
                Spacing = 5,
                BackgroundColor = Eto.Drawing.Color.FromArgb(80, 80, 80),
                Items =
                {
                    new Label { Text = I18n.T("Viewer.AnimPreview"), TextColor = Colors.White, Font = new Font(SystemFont.Bold) },
                    _previewInfoLabel,
                    _stopButton,
                    _previewImage
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

            if (_getSpriteDataFunc != null)
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
            playBtn.Click += (s, e) => PlayAction(entry, action);

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

            // 方向選擇器 (只對有向動作顯示)
            if (action.Directional == 1)
            {
                var dirSelector = CreateDirectionSelector();
                contentPanel.Items.Add(dirSelector);
            }

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

            // 如果正在播放，更新當前幀
            if (_playingAction != null)
            {
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

        private void PlayAction(SprListEntry entry, SprAction action)
        {
            _animTimer.Stop();
            _playingAction = action;
            _playingFrameIndex = 0;

            if (action.Frames.Count == 0)
            {
                _previewInfoLabel.Text = I18n.T("Viewer.NoFrames");
                return;
            }

            // 確保有 sprite 資料
            if (_currentSprFrames == null || _currentSprFrames.Length == 0)
            {
                LoadSpriteData(entry);
            }

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

            // 設定下一幀間隔 (舊版公式: Duration * 30ms, 最小 50ms)
            _animTimer.Interval = Math.Max(0.05, frame.Duration * 0.03);

            // 顯示圖片
            if (_currentSprFrames != null && _currentSprFrames.Length > 0)
            {
                // 計算實際幀索引 - 舊版公式: ImageId + FrameIndex
                int actualIndex = frame.ImageId + frame.FrameIndex;

                // 如果超出範圍，嘗試只用 ImageId
                if (actualIndex < 0 || actualIndex >= _currentSprFrames.Length)
                {
                    if (frame.ImageId >= 0 && frame.ImageId < _currentSprFrames.Length)
                    {
                        actualIndex = frame.ImageId;
                    }
                    else
                    {
                        actualIndex = actualIndex % _currentSprFrames.Length;
                    }
                }

                if (actualIndex >= 0 && actualIndex < _currentSprFrames.Length)
                {
                    var sprFrame = _currentSprFrames[actualIndex];
                    if (sprFrame.Image != null)
                    {
                        _previewImage.Image = ConvertToEtoBitmap(sprFrame.Image);
                    }
                }

                // 顯示 SPR 檔名、格數和方向資訊
                var sprFileName = $"{_selectedEntry?.SpriteId}.spr";
                var directionText = _playingAction.IsDirectional ? $"#Dir:{_currentDirection}" : "";
                _previewInfoLabel.Text = $"{I18n.T("Viewer.File")}: {sprFileName}\n" +
                                         $"{I18n.T("Viewer.Frame")}: {_playingFrameIndex + 1}/{_playingAction.Frames.Count} {directionText}\n" +
                                         $"{I18n.T("Viewer.SprFrame")}: {actualIndex}/{_currentSprFrames.Length}";
            }
            else
            {
                _previewInfoLabel.Text = I18n.T("Viewer.Playing", _playingAction.DisplayName, _playingFrameIndex + 1, _playingAction.Frames.Count) + "\n" +
                                         I18n.T("Viewer.NoImageData");
            }
        }

        private Bitmap ConvertToEtoBitmap(Image<Rgba32> image)
        {
            using var ms = new System.IO.MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            _animTimer.Stop();
            _playingAction = null;
            _previewInfoLabel.Text = I18n.T("Viewer.Stopped");
        }

        public override void Dispose()
        {
            _animTimer?.Stop();
            _animTimer?.Dispose();
            base.Dispose();
        }
    }
}
