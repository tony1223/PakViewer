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
    /// SPR 相簿預覽器 - 網格顯示所有 SPR 的縮圖
    /// </summary>
    public class SprGalleryViewer : BaseViewer
    {
        private List<SprGroup> _groups;
        private Scrollable _scrollable;
        private Drawable _drawable;
        private Label _infoLabel;
        private TextBox _searchBox;

        // 縮圖快取
        private ConcurrentDictionary<int, Bitmap> _thumbnails = new();
        private volatile bool _isLoading = false;

        // 選擇
        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;

        // 常數
        private const int COLUMNS = 8;
        private const int CELL_SIZE = 72;
        private const int SPACING = 4;

        // 事件
        public event Action<SprGroup> OnGroupSelected;

        public override string[] SupportedExtensions => Array.Empty<string>();
        public override bool CanEdit => false;

        public override void LoadData(byte[] data, string fileName)
        {
            // 此 Viewer 不從 byte[] 載入，使用 LoadGroups 方法
        }

        public void LoadGroups(List<SprGroup> groups)
        {
            _groups = groups;
            _thumbnails.Clear();
            BuildUI();
            StartThumbnailLoading();
        }

        private void BuildUI()
        {
            var layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 5), Padding = 5 };

            // 工具列
            var toolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    new Label { Text = "搜尋:", VerticalAlignment = VerticalAlignment.Center }
                }
            };

            _searchBox = new TextBox { Width = 100, PlaceholderText = "ID..." };
            _searchBox.TextChanged += OnSearchChanged;
            toolbar.Items.Add(_searchBox);

            _infoLabel = new Label
            {
                Text = $"共 {_groups.Count} 個 SPR",
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Items.Add(new StackLayoutItem(null, true));
            toolbar.Items.Add(_infoLabel);

            layout.AddRow(toolbar);

            // 畫布
            int rows = (_groups.Count + COLUMNS - 1) / COLUMNS;
            int width = COLUMNS * (CELL_SIZE + SPACING) + SPACING;
            int height = rows * (CELL_SIZE + SPACING) + SPACING;

            _drawable = new Drawable
            {
                Size = new Eto.Drawing.Size(width, Math.Max(height, 400)),
                BackgroundColor = Colors.DarkGray
            };
            _drawable.Paint += OnPaint;
            _drawable.MouseDown += OnMouseDown;
            _drawable.MouseMove += OnMouseMove;
            _drawable.MouseDoubleClick += OnMouseDoubleClick;

            _scrollable = new Scrollable
            {
                Content = _drawable,
                ExpandContentWidth = false,
                ExpandContentHeight = false
            };

            layout.AddRow(new TableLayout
            {
                Rows = { new TableRow(_scrollable) { ScaleHeight = true } }
            });

            _control = layout;
        }

        private void OnSearchChanged(object sender, EventArgs e)
        {
            // 搜尋時重繪
            _drawable?.Invalidate();
        }

        private void StartThumbnailLoading()
        {
            if (_isLoading) return;
            _isLoading = true;

            Task.Run(() =>
            {
                Parallel.ForEach(
                    _groups,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    group =>
                    {
                        if (_thumbnails.ContainsKey(group.SpriteId)) return;

                        // 取得第一個 part 的第一個 frame
                        var firstPart = group.Parts.FirstOrDefault();
                        if (firstPart == null) return;

                        try
                        {
                            var data = firstPart.SourcePak.Extract(firstPart.FileName);
                            var frames = SprReader.Load(data);
                            if (frames == null || frames.Length == 0) return;

                            var firstFrame = frames[0];
                            if (firstFrame.Image == null) return;

                            var bmp = ConvertToBitmap(firstFrame.Image);
                            if (bmp != null)
                            {
                                _thumbnails[group.SpriteId] = bmp;
                            }
                        }
                        catch { }
                    });

                _isLoading = false;
                Application.Instance.Invoke(() => _drawable?.Invalidate());
            });
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (_groups == null) return;

            var g = e.Graphics;
            var clip = e.ClipRectangle;
            var filter = _searchBox?.Text?.Trim() ?? "";

            var filteredGroups = string.IsNullOrEmpty(filter)
                ? _groups
                : _groups.Where(gr => gr.SpriteId.ToString().Contains(filter)).ToList();

            // 更新大小
            int rows = (filteredGroups.Count + COLUMNS - 1) / COLUMNS;
            int height = rows * (CELL_SIZE + SPACING) + SPACING;
            if (_drawable.Size.Height != Math.Max(height, 400))
            {
                _drawable.Size = new Eto.Drawing.Size(_drawable.Size.Width, Math.Max(height, 400));
            }

            for (int i = 0; i < filteredGroups.Count; i++)
            {
                var group = filteredGroups[i];
                int col = i % COLUMNS;
                int row = i / COLUMNS;
                int x = col * (CELL_SIZE + SPACING) + SPACING;
                int y = row * (CELL_SIZE + SPACING) + SPACING;

                // 只繪製可見區域
                if (y + CELL_SIZE < clip.Top || y > clip.Bottom) continue;

                // 背景
                var bgColor = i == _selectedIndex ? Eto.Drawing.Color.FromArgb(80, 120, 180)
                            : i == _hoveredIndex ? Eto.Drawing.Color.FromArgb(70, 70, 80)
                            : Eto.Drawing.Color.FromArgb(50, 50, 50);
                g.FillRectangle(bgColor, x, y, CELL_SIZE, CELL_SIZE);

                // 縮圖
                if (_thumbnails.TryGetValue(group.SpriteId, out var bmp))
                {
                    float scale = Math.Min((float)(CELL_SIZE - 16) / bmp.Width, (float)(CELL_SIZE - 16) / bmp.Height);
                    float w = bmp.Width * scale;
                    float h = bmp.Height * scale;
                    float imgX = x + (CELL_SIZE - w) / 2;
                    float imgY = y + 4 + (CELL_SIZE - 16 - h) / 2;
                    g.DrawImage(bmp, imgX, imgY, w, h);
                }

                // ID
                var idText = group.SpriteId.ToString();
                g.DrawText(new Font(SystemFont.Default, 8), Colors.White, x + 4, y + CELL_SIZE - 14, idText);

                // Parts/Frames 資訊
                var infoText = $"{group.PartsCount}p";
                g.DrawText(new Font(SystemFont.Default, 7), Colors.Gray, x + CELL_SIZE - 20, y + CELL_SIZE - 12, infoText);
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            var index = GetIndexAtPoint(e.Location);
            if (index >= 0)
            {
                _selectedIndex = index;
                _drawable.Invalidate();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var index = GetIndexAtPoint(e.Location);
            if (index != _hoveredIndex)
            {
                _hoveredIndex = index;
                _drawable.Invalidate();
            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            var index = GetIndexAtPoint(e.Location);
            if (index >= 0 && index < GetFilteredGroups().Count)
            {
                var group = GetFilteredGroups()[index];
                OnGroupSelected?.Invoke(group);
            }
        }

        private int GetIndexAtPoint(Eto.Drawing.PointF point)
        {
            int col = (int)(point.X - SPACING) / (CELL_SIZE + SPACING);
            int row = (int)(point.Y - SPACING) / (CELL_SIZE + SPACING);

            if (col < 0 || col >= COLUMNS) return -1;

            int index = row * COLUMNS + col;
            var filtered = GetFilteredGroups();
            return index >= 0 && index < filtered.Count ? index : -1;
        }

        private List<SprGroup> GetFilteredGroups()
        {
            var filter = _searchBox?.Text?.Trim() ?? "";
            return string.IsNullOrEmpty(filter)
                ? _groups
                : _groups.Where(gr => gr.SpriteId.ToString().Contains(filter)).ToList();
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
            foreach (var bmp in _thumbnails.Values)
                bmp?.Dispose();
            _thumbnails.Clear();
            _groups = null;
            base.Dispose();
        }
    }
}
