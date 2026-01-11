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
        private Lin.Helper.Core.Sprite.SprFrame[] _currentFrames;
        private int _selectedFrameIndex = -1;
        private SprPart _selectedPart;

        // 動畫
        private UITimer _animTimer;
        private int _animFrameIndex = 0;
        private bool _isPlaying = false;

        private const int THUMB_SIZE = 48;
        private const int THUMB_SPACING = 4;

        public override string[] SupportedExtensions => Array.Empty<string>();
        public override bool CanEdit => false;

        public override void LoadData(byte[] data, string fileName)
        {
            // 此 Viewer 不從 byte[] 載入，使用 LoadGroup 方法
        }

        public void LoadGroup(SprGroup group)
        {
            _group = group;
            _frameBitmaps.Clear();
            BuildUI();
            // LoadFrameThumbnails 已不需要，因為 AddPartSection 會直接載入
        }

        private void BuildUI()
        {
            var mainLayout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 5), Padding = 5 };

            // 標題
            _infoLabel = new Label
            {
                Text = $"Sprite #{_group.SpriteId} ({_group.PartsCount} parts)",
                Font = new Font(SystemFont.Bold, 12)
            };
            mainLayout.AddRow(_infoLabel);

            // Parts 和 Frames 列表
            _layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 10) };

            foreach (var part in _group.Parts)
            {
                AddPartSection(part);
            }

            _scrollable = new Scrollable
            {
                Content = _layout,
                ExpandContentWidth = true
            };

            mainLayout.AddRow(new TableLayout
            {
                Rows = { new TableRow(_scrollable) { ScaleHeight = true } }
            });

            // 預覽區域
            var previewPanel = new GroupBox { Text = "預覽" };
            _previewDrawable = new Drawable
            {
                Size = new Eto.Drawing.Size(200, 200),
                BackgroundColor = Colors.Black
            };
            _previewDrawable.Paint += OnPreviewPaint;

            var playBtn = new Button { Text = "▶ 播放" };
            playBtn.Click += (s, e) => ToggleAnimation();

            previewPanel.Content = new StackLayout
            {
                Orientation = Orientation.Vertical,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Spacing = 5,
                Items = { _previewDrawable, playBtn }
            };

            mainLayout.AddRow(previewPanel);

            _control = mainLayout;

            // 動畫計時器
            _animTimer = new UITimer { Interval = 0.15 };
            _animTimer.Elapsed += OnAnimTick;
        }

        private void AddPartSection(SprPart part)
        {
            // 直接載入這個 part 的 frames
            SprFrame[] frames = null;
            try
            {
                var data = part.SourcePak.Extract(part.FileName);
                frames = SprReader.Load(data);
            }
            catch { }

            int frameCount = frames?.Length ?? 0;

            var partLabel = new Label
            {
                Text = $"▼ {part.FileName} ({frameCount} frames)",
                Font = new Font(SystemFont.Bold, 10)
            };

            // 使用 StackLayout 模擬 WrapPanel
            var framesPanel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = THUMB_SPACING
            };

            // 為每個 frame 建立縮圖按鈕
            for (int i = 0; i < frameCount; i++)
            {
                var frameIndex = i;
                var frameKey = $"{part.FileName}_{i}";
                var frame = frames[i];

                // 預先轉換 bitmap
                if (frame.Image != null && !_frameBitmaps.ContainsKey(frameKey))
                {
                    var bmp = ConvertToBitmap(frame.Image);
                    if (bmp != null)
                        _frameBitmaps[frameKey] = bmp;
                }

                var frameBtn = new Drawable
                {
                    Size = new Eto.Drawing.Size(THUMB_SIZE, THUMB_SIZE),
                    BackgroundColor = Colors.DarkGray
                };

                frameBtn.Paint += (s, e) =>
                {
                    if (_frameBitmaps.TryGetValue(frameKey, out var bmp))
                    {
                        // 縮放繪製
                        float scale = Math.Min((float)THUMB_SIZE / bmp.Width, (float)THUMB_SIZE / bmp.Height);
                        float w = bmp.Width * scale;
                        float h = bmp.Height * scale;
                        float x = (THUMB_SIZE - w) / 2;
                        float y = (THUMB_SIZE - h) / 2;
                        e.Graphics.DrawImage(bmp, x, y, w, h);
                    }

                    // Frame 編號
                    e.Graphics.DrawText(new Font(SystemFont.Default, 7), Colors.White, 2, THUMB_SIZE - 12, frameIndex.ToString());
                };

                frameBtn.MouseDown += (s, e) =>
                {
                    _selectedPart = part;
                    _selectedFrameIndex = frameIndex;
                    _currentFrames = frames;  // 使用已載入的 frames
                    _previewDrawable.Invalidate();
                };

                framesPanel.Items.Add(frameBtn);
            }

            _layout.AddRow(partLabel);
            _layout.AddRow(framesPanel);
        }

        private void LoadFrameThumbnails()
        {
            Task.Run(() =>
            {
                foreach (var part in _group.Parts)
                {
                    try
                    {
                        var data = part.SourcePak.Extract(part.FileName);
                        var frames = SprReader.Load(data);
                        if (frames == null) continue;

                        for (int i = 0; i < frames.Length; i++)
                        {
                            var frame = frames[i];
                            if (frame.Image == null) continue;

                            var frameKey = $"{part.FileName}_{i}";
                            var bmp = ConvertToBitmap(frame.Image);
                            if (bmp != null)
                            {
                                _frameBitmaps[frameKey] = bmp;
                            }
                        }

                        Application.Instance.Invoke(() => _layout?.Invalidate());
                    }
                    catch { }
                }

                Application.Instance.Invoke(() => _scrollable?.Invalidate());
            });
        }

        private void LoadPartFrames(SprPart part)
        {
            try
            {
                var data = part.SourcePak.Extract(part.FileName);
                _currentFrames = SprReader.Load(data);
            }
            catch
            {
                _currentFrames = null;
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

            // 2x 縮放
            float scale = Math.Min((float)(drawableSize.Width - 20) / bmp.Width, (float)(drawableSize.Height - 20) / bmp.Height);
            scale = Math.Min(scale, 2);

            float w = bmp.Width * scale;
            float h = bmp.Height * scale;
            float x = (drawableSize.Width - w) / 2;
            float y = (drawableSize.Height - h) / 2;

            g.DrawImage(bmp, x, y, w, h);

            // 顯示資訊
            var info = $"Frame {_selectedFrameIndex} ({frame.Width}x{frame.Height})";
            g.DrawText(new Font(SystemFont.Default, 9), Colors.White, 5, 5, info);
        }

        private void ToggleAnimation()
        {
            if (_currentFrames == null || _currentFrames.Length == 0) return;

            _isPlaying = !_isPlaying;
            if (_isPlaying)
            {
                _animFrameIndex = 0;
                _animTimer.Start();
            }
            else
            {
                _animTimer.Stop();
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

            _currentFrames = null;
            base.Dispose();
        }
    }
}
