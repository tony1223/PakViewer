using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;

namespace PakViewer.Controls
{
    /// <summary>
    /// 相簿項目
    /// </summary>
    public class GalleryItem
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public int FileSize { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// 通用相簿面板 - 支援虛擬化滾動和延遲縮圖載入
    /// </summary>
    public class GalleryPanel : Scrollable
    {
        private const int PADDING = 8;
        private const int LABEL_HEIGHT = 20;
        private const int DEFAULT_THUMBNAIL_SIZE = 80;

        private Drawable _drawable;
        private List<GalleryItem> _items = new();
        private ConcurrentDictionary<int, Bitmap> _thumbnailCache = new();
        private int _thumbnailSize = DEFAULT_THUMBNAIL_SIZE;
        private int _selectedIndex = -1;
        private int _hoverIndex = -1;

        // 延遲載入
        private UITimer _loadTimer;
        private CancellationTokenSource _loadCts;

        // 縮圖載入回調
        public Func<int, Bitmap> ThumbnailLoader { get; set; }

        // 事件
        public event EventHandler<GalleryItem> ItemSelected;
        public event EventHandler<GalleryItem> ItemDoubleClicked;
        public event EventHandler<GalleryItem> ItemRightClicked;

        public int ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                if (_thumbnailSize != value)
                {
                    _thumbnailSize = value;
                    ClearCache();
                    UpdateContentSize();
                    _drawable?.Invalidate();
                }
            }
        }

        public GalleryPanel()
        {
            _drawable = new Drawable();
            _drawable.Paint += OnPaint;
            _drawable.MouseDown += OnMouseDown;
            _drawable.MouseDoubleClick += OnMouseDoubleClick;
            _drawable.MouseMove += OnMouseMove;
            _drawable.MouseLeave += OnMouseLeave;

            Content = _drawable;
            ScrollPosition = Point.Empty;

            Scroll += (s, e) => OnScroll();
        }

        public void SetItems(List<GalleryItem> items)
        {
            _items = items ?? new List<GalleryItem>();
            _selectedIndex = -1;
            _hoverIndex = -1;
            ClearCache();
            UpdateContentSize();
            ScrollPosition = Point.Empty;
            _drawable.Invalidate();

            // 載入初始可見縮圖
            ScheduleThumbnailLoad();
        }

        public void ClearCache()
        {
            _loadCts?.Cancel();
            foreach (var bmp in _thumbnailCache.Values)
                bmp?.Dispose();
            _thumbnailCache.Clear();
        }

        private int GetColumns()
        {
            int itemWidth = _thumbnailSize + PADDING * 2;
            int cols = Math.Max(1, (int)(Width / itemWidth));
            return cols;
        }

        private void UpdateContentSize()
        {
            if (_items.Count == 0)
            {
                _drawable.Size = new Size((int)Width, (int)Height);
                return;
            }

            int itemWidth = _thumbnailSize + PADDING * 2;
            int itemHeight = _thumbnailSize + LABEL_HEIGHT + PADDING * 2;
            int columns = GetColumns();
            int rows = (int)Math.Ceiling((double)_items.Count / columns);
            int totalHeight = rows * itemHeight;

            _drawable.Size = new Size((int)Width, Math.Max(totalHeight, (int)Height));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateContentSize();
            _drawable?.Invalidate();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(45, 45, 48));

            if (_items.Count == 0) return;

            int itemWidth = _thumbnailSize + PADDING * 2;
            int itemHeight = _thumbnailSize + LABEL_HEIGHT + PADDING * 2;
            int columns = GetColumns();

            // 計算可見範圍
            int scrollY = ScrollPosition.Y;
            int startRow = scrollY / itemHeight;
            int visibleRows = (int)Math.Ceiling((double)Height / itemHeight) + 1;
            int startIndex = startRow * columns;
            int endIndex = Math.Min(_items.Count, (startRow + visibleRows) * columns);

            // 繪製可見項目
            for (int i = startIndex; i < endIndex; i++)
            {
                int col = i % columns;
                int row = i / columns;
                int x = col * itemWidth;
                int y = row * itemHeight;

                DrawItem(g, i, x, y, itemWidth, itemHeight);
            }
        }

        private void DrawItem(Graphics g, int index, int x, int y, int width, int height)
        {
            var item = _items[index];
            var bgColor = Color.FromArgb(60, 60, 65);

            // 選取或 hover 狀態
            if (index == _selectedIndex)
                bgColor = Color.FromArgb(0, 122, 204);
            else if (index == _hoverIndex)
                bgColor = Color.FromArgb(80, 80, 85);

            // 背景
            g.FillRectangle(bgColor, x + PADDING / 2, y + PADDING / 2,
                width - PADDING, height - PADDING);

            // 縮圖
            int thumbX = x + PADDING;
            int thumbY = y + PADDING;
            int thumbSize = _thumbnailSize;

            if (_thumbnailCache.TryGetValue(index, out var thumbnail) && thumbnail != null)
            {
                // 計算縮放後的尺寸（確保圖片適應格子）
                int drawWidth = thumbnail.Width;
                int drawHeight = thumbnail.Height;

                if (drawWidth > thumbSize || drawHeight > thumbSize)
                {
                    // 需要縮放
                    float scale = Math.Min((float)thumbSize / drawWidth, (float)thumbSize / drawHeight);
                    drawWidth = (int)(drawWidth * scale);
                    drawHeight = (int)(drawHeight * scale);
                }

                // 置中繪製縮圖
                int imgX = thumbX + (thumbSize - drawWidth) / 2;
                int imgY = thumbY + (thumbSize - drawHeight) / 2;
                g.DrawImage(thumbnail, imgX, imgY, drawWidth, drawHeight);
            }
            else
            {
                // 佔位符
                g.FillRectangle(Color.FromArgb(40, 40, 42), thumbX, thumbY, thumbSize, thumbSize);
                g.DrawText(Fonts.Sans(8), Colors.Gray, thumbX + thumbSize / 2 - 10, thumbY + thumbSize / 2 - 5, "...");
            }

            // 檔名標籤
            int labelY = thumbY + thumbSize + 2;
            var displayName = GetDisplayName(item.FileName, width - PADDING * 2);
            g.DrawText(Fonts.Sans(9), Colors.White, thumbX, labelY, displayName);
        }

        private string GetDisplayName(string fileName, int maxWidth)
        {
            // 簡單截斷
            if (fileName.Length > 12)
                return fileName.Substring(0, 10) + "..";
            return fileName;
        }

        private int GetItemIndexAt(PointF point)
        {
            int itemWidth = _thumbnailSize + PADDING * 2;
            int itemHeight = _thumbnailSize + LABEL_HEIGHT + PADDING * 2;
            int columns = GetColumns();

            int col = (int)(point.X / itemWidth);
            int row = (int)(point.Y / itemHeight);
            int index = row * columns + col;

            if (col >= columns || index < 0 || index >= _items.Count)
                return -1;

            return index;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            int index = GetItemIndexAt(e.Location);
            if (index >= 0 && index < _items.Count)
            {
                _selectedIndex = index;
                _drawable.Invalidate();

                if (e.Buttons == MouseButtons.Alternate)  // 右鍵
                {
                    ItemRightClicked?.Invoke(this, _items[index]);
                }
                else
                {
                    ItemSelected?.Invoke(this, _items[index]);
                }
            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = GetItemIndexAt(e.Location);
            if (index >= 0 && index < _items.Count)
            {
                ItemDoubleClicked?.Invoke(this, _items[index]);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            int index = GetItemIndexAt(e.Location);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                _drawable.Invalidate();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _drawable.Invalidate();
            }
        }

        private void OnScroll()
        {
            _drawable.Invalidate();
            ScheduleThumbnailLoad();
        }

        private void ScheduleThumbnailLoad()
        {
            _loadTimer?.Stop();
            _loadTimer = new UITimer { Interval = 0.15 };
            _loadTimer.Elapsed += (s, e) =>
            {
                _loadTimer.Stop();
                LoadVisibleThumbnailsAsync();
            };
            _loadTimer.Start();
        }

        private async void LoadVisibleThumbnailsAsync()
        {
            if (ThumbnailLoader == null || _items.Count == 0) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            // 計算可見範圍
            int itemHeight = _thumbnailSize + LABEL_HEIGHT + PADDING * 2;
            int columns = GetColumns();
            int scrollY = ScrollPosition.Y;
            int startRow = scrollY / itemHeight;
            int visibleRows = (int)Math.Ceiling((double)Height / itemHeight) + 2;
            int startIndex = Math.Max(0, startRow * columns);
            int endIndex = Math.Min(_items.Count, (startRow + visibleRows) * columns);

            await Task.Run(() =>
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    if (token.IsCancellationRequested) break;
                    if (_thumbnailCache.ContainsKey(i)) continue;

                    try
                    {
                        var thumbnail = ThumbnailLoader(i);
                        if (thumbnail != null && !token.IsCancellationRequested)
                        {
                            _thumbnailCache[i] = thumbnail;
                            Application.Instance.Invoke(() =>
                            {
                                if (!token.IsCancellationRequested)
                                    _drawable.Invalidate();
                            });
                        }
                    }
                    catch
                    {
                        // 忽略載入失敗
                    }
                }
            }, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadCts?.Cancel();
                _loadTimer?.Stop();
                ClearCache();
            }
            base.Dispose(disposing);
        }
    }
}
