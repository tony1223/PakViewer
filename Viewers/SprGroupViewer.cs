using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// SPR 群組預覽器 - 顯示 SPR 的所有 Parts 和 Frames
    /// </summary>
    public class SprGroupViewer : BaseViewer
    {
        private SprGroup _group;
        private Scrollable _scrollable;
        private DynamicLayout _layout;
        private Drawable _previewDrawable;
        private Label _infoLabel;

        // Frame 快取
        private ConcurrentDictionary<string, Bitmap> _frameBitmaps = new();
        private ConcurrentDictionary<string, SprFrame[]> _partFramesCache = new();
        private Lin.Helper.Core.Sprite.SprFrame[] _currentFrames;
        private int _selectedFrameIndex = -1;
        private SprPart _selectedPart;

        // 動畫
        private UITimer _animTimer;
        private int _animFrameIndex = 0;
        private bool _isPlaying = false;
        private Button _playBtn;

        // 縮放
        private int _zoomLevel = 2;  // 預設 2x
        private DropDown _zoomDropDown;

        // 背景顏色
        private int _bgColorIndex = 0;  // 0=黑, 1=紅, 2=透明, 3=白
        private DropDown _bgColorDropDown;
        private Action<int> _onBgColorChanged;  // 背景色變更回呼

        private const int THUMB_SIZE = 48;
        private const int THUMB_SPACING = 4;

        public override string[] SupportedExtensions => Array.Empty<string>();
        public override bool CanEdit => false;

        /// <summary>
        /// 設定背景顏色初始值和變更回呼
        /// </summary>
        public void SetBackgroundColorSettings(int initialColor, Action<int> onChanged)
        {
            _bgColorIndex = initialColor;
            _onBgColorChanged = onChanged;
        }

        public override void LoadData(byte[] data, string fileName)
        {
            // 此 Viewer 不從 byte[] 載入，使用 LoadGroup 方法
        }

        public void LoadGroup(SprGroup group)
        {
            _group = group;
            _frameBitmaps.Clear();
            _partFramesCache.Clear();
            BuildUI();

            // 自動載入並播放第一個 part
            if (_group.Parts.Count > 0)
            {
                AutoPlayFirstPart();
            }
        }

        private void AutoPlayFirstPart()
        {
            var firstPart = _group.Parts[0];
            var frames = LoadPartFramesInternal(firstPart);
            if (frames != null && frames.Length > 0)
            {
                _partFramesCache[firstPart.FileName] = frames;
                LoadFrameBitmaps(firstPart, frames);

                _selectedPart = firstPart;
                _currentFrames = frames;
                _selectedFrameIndex = 0;
                _animFrameIndex = 0;
                _isPlaying = true;
                _animTimer.Start();
                if (_playBtn != null) _playBtn.Text = I18n.T("Button.Pause");
                _previewDrawable?.Invalidate();
            }
        }

        private void BuildUI()
        {
            // 標題
            _infoLabel = new Label
            {
                Text = $"Sprite #{_group.SpriteId} ({_group.PartsCount} parts)",
                Font = new Font(SystemFont.Bold, 12)
            };

            // Parts 和 Frames 列表
            _layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 10) };

            foreach (var part in _group.Parts)
            {
                AddPartSection(part);
            }

            _scrollable = new Scrollable
            {
                Content = _layout,
                ExpandContentWidth = true,
                ExpandContentHeight = false
            };

            // 預覽區域
            _previewDrawable = new Drawable
            {
                Size = new Eto.Drawing.Size(200, 200),
                BackgroundColor = Colors.Black
            };
            _previewDrawable.Paint += OnPreviewPaint;

            _playBtn = new Button { Text = I18n.T("Button.Play") };
            _playBtn.Click += (s, e) => ToggleAnimation();

            // 縮放選擇
            _zoomDropDown = new DropDown();
            _zoomDropDown.Items.Add("1x");
            _zoomDropDown.Items.Add("2x");
            _zoomDropDown.SelectedIndex = _zoomLevel - 1;  // 預設 2x
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

            var controlsPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items = {
                    _playBtn,
                    new Label { Text = I18n.T("Label.Zoom"), VerticalAlignment = VerticalAlignment.Center },
                    _zoomDropDown,
                    new Label { Text = I18n.T("Label.Background"), VerticalAlignment = VerticalAlignment.Center },
                    _bgColorDropDown
                }
            };

            var previewPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Spacing = 5,
                Padding = 5,
                Items = { _previewDrawable, controlsPanel }
            };

            // 使用 Splitter 分割上下區域
            var splitter = new Splitter
            {
                Orientation = Orientation.Vertical,
                Position = 400,
                Panel1 = _scrollable,
                Panel2 = previewPanel
            };

            var mainLayout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 5), Padding = 5 };
            mainLayout.AddRow(_infoLabel);
            mainLayout.AddRow(new TableLayout
            {
                Rows = { new TableRow(splitter) { ScaleHeight = true } }
            });

            _control = mainLayout;

            // 動畫計時器
            _animTimer = new UITimer { Interval = 0.15 };
            _animTimer.Elapsed += OnAnimTick;
        }

        private void AddPartSection(SprPart part)
        {
            // 延遲載入 - 不在這裡載入 frames
            var partLabel = new LinkButton
            {
                Text = $"▶ {part.FileName}",
                Font = new Font(SystemFont.Bold, 10)
            };

            // 縮圖面板 (初始隱藏，點擊後展開)
            var framesPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = THUMB_SPACING,
                Visible = false
            };

            bool isExpanded = false;

            partLabel.Click += (s, e) =>
            {
                if (!isExpanded)
                {
                    // 第一次展開：載入 frames 並建立縮圖
                    var frames = LoadPartFramesInternal(part);
                    if (frames != null)
                    {
                        _partFramesCache[part.FileName] = frames;

                        // 更新標題顯示 frame 數量
                        partLabel.Text = $"▼ {part.FileName} ({frames.Length} frames)";

                        // 載入縮圖
                        LoadFrameBitmaps(part, frames);

                        // 建立縮圖按鈕
                        for (int i = 0; i < frames.Length; i++)
                        {
                            var frameIndex = i;
                            var frameKey = $"{part.FileName}_{i}";

                            var frameBtn = new Drawable
                            {
                                Size = new Eto.Drawing.Size(THUMB_SIZE, THUMB_SIZE),
                                BackgroundColor = Colors.DarkGray
                            };

                            frameBtn.Paint += (sender, pe) =>
                            {
                                if (_frameBitmaps.TryGetValue(frameKey, out var bmp))
                                {
                                    float scale = Math.Min((float)THUMB_SIZE / bmp.Width, (float)THUMB_SIZE / bmp.Height);
                                    float w = bmp.Width * scale;
                                    float h = bmp.Height * scale;
                                    float x = (THUMB_SIZE - w) / 2;
                                    float y = (THUMB_SIZE - h) / 2;
                                    pe.Graphics.DrawImage(bmp, x, y, w, h);
                                }
                                pe.Graphics.DrawText(new Font(SystemFont.Default, 7), Colors.White, 2, THUMB_SIZE - 12, frameIndex.ToString());
                            };

                            frameBtn.MouseDown += (sender, me) =>
                            {
                                // 停止目前動畫
                                _animTimer.Stop();
                                _isPlaying = false;
                                if (_playBtn != null) _playBtn.Text = I18n.T("Button.Play");

                                _selectedPart = part;
                                _selectedFrameIndex = frameIndex;
                                _currentFrames = frames;

                                _previewDrawable.Invalidate();
                            };

                            framesPanel.Items.Add(frameBtn);
                        }

                        // 設定當前選擇並開始播放
                        _selectedPart = part;
                        _currentFrames = frames;
                        _selectedFrameIndex = 0;
                        _animFrameIndex = 0;
                        _isPlaying = true;
                        _animTimer.Start();
                        if (_playBtn != null) _playBtn.Text = I18n.T("Button.Pause");
                        _previewDrawable.Invalidate();
                    }

                    framesPanel.Visible = true;
                    isExpanded = true;
                }
                else
                {
                    // 切換展開/收合
                    framesPanel.Visible = !framesPanel.Visible;
                    partLabel.Text = framesPanel.Visible
                        ? $"▼ {part.FileName} ({_partFramesCache.GetValueOrDefault(part.FileName)?.Length ?? 0} frames)"
                        : $"▶ {part.FileName} ({_partFramesCache.GetValueOrDefault(part.FileName)?.Length ?? 0} frames)";

                    // 如果展開且有 frames，播放動畫
                    if (framesPanel.Visible && _partFramesCache.TryGetValue(part.FileName, out var frames))
                    {
                        _selectedPart = part;
                        _currentFrames = frames;
                        _selectedFrameIndex = 0;
                        _animFrameIndex = 0;
                        _isPlaying = true;
                        _animTimer.Start();
                        if (_playBtn != null) _playBtn.Text = I18n.T("Button.Pause");
                        _previewDrawable.Invalidate();
                    }
                }
            };

            _layout.AddRow(partLabel);
            _layout.AddRow(framesPanel);
        }

        private SprFrame[] LoadPartFramesInternal(SprPart part)
        {
            try
            {
                var data = part.SourcePak.Extract(part.FileName);
                return SprReader.Load(data);
            }
            catch
            {
                return null;
            }
        }

        private void LoadFrameBitmaps(SprPart part, SprFrame[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frameKey = $"{part.FileName}_{i}";
                if (!_frameBitmaps.ContainsKey(frameKey) && frames[i].Image != null)
                {
                    var bmp = ConvertToBitmap(frames[i].Image);
                    if (bmp != null)
                        _frameBitmaps[frameKey] = bmp;
                }
            }
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            if (_currentFrames == null || _selectedFrameIndex < 0 || _selectedFrameIndex >= _currentFrames.Length)
                return;

            var frame = _currentFrames[_selectedFrameIndex];
            if (frame.Image == null) return;

            var frameKey = $"{_selectedPart?.FileName}_{_selectedFrameIndex}";
            if (!_frameBitmaps.TryGetValue(frameKey, out var bmp))
            {
                bmp = ConvertToBitmap(frame.Image);
                if (bmp != null)
                    _frameBitmaps[frameKey] = bmp;
            }

            if (bmp == null) return;

            var g = e.Graphics;
            var drawableSize = _previewDrawable.Size;

            // 依選擇的縮放倍率繪製
            float scale = Math.Min((float)(drawableSize.Width - 20) / bmp.Width, (float)(drawableSize.Height - 20) / bmp.Height);
            scale = Math.Min(scale, _zoomLevel);

            float w = bmp.Width * scale;
            float h = bmp.Height * scale;
            float x = (drawableSize.Width - w) / 2;
            float y = (drawableSize.Height - h) / 2;

            g.DrawImage(bmp, x, y, w, h);

            // 顯示資訊
            var info = $"Frame {_selectedFrameIndex} ({frame.Width}x{frame.Height})";
            g.DrawText(new Font(SystemFont.Default, 9), Colors.White, 5, 5, info);
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

        private void ToggleAnimation()
        {
            if (_currentFrames == null || _currentFrames.Length == 0) return;

            _isPlaying = !_isPlaying;
            if (_isPlaying)
            {
                _animFrameIndex = 0;
                _animTimer.Start();
                if (_playBtn != null) _playBtn.Text = I18n.T("Button.Pause");
            }
            else
            {
                _animTimer.Stop();
                if (_playBtn != null) _playBtn.Text = I18n.T("Button.Play");
            }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (_currentFrames == null || _currentFrames.Length == 0)
            {
                _animTimer.Stop();
                _isPlaying = false;
                return;
            }

            _selectedFrameIndex = _animFrameIndex;
            _animFrameIndex = (_animFrameIndex + 1) % _currentFrames.Length;
            _previewDrawable.Invalidate();
        }

        private Bitmap ConvertToBitmap(Image<Rgba32> image)
        {
            try
            {
                using var ms = new MemoryStream();
                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        public override void Dispose()
        {
            _animTimer?.Stop();
            _animTimer?.Dispose();

            foreach (var bmp in _frameBitmaps.Values)
                bmp?.Dispose();
            _frameBitmaps.Clear();
            _partFramesCache.Clear();

            _currentFrames = null;
            base.Dispose();
        }
    }
}
