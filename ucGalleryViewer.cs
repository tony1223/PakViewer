using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PakViewer.Utility;

namespace PakViewer
{
    /// <summary>
    /// 相簿模式檢視器 - 用縮圖瀏覽圖片類檔案
    /// </summary>
    public class ucGalleryViewer : UserControl
    {
        private FlowLayoutPanel flowPanel;
        private TrackBar tbThumbnailSize;
        private Label lblSize;
        private Panel panelToolbar;
        private VScrollBar vScrollBar;
        private Panel panelContent;

        private int _thumbnailSize = 80;
        private const int MIN_THUMBNAIL_SIZE = 48;
        private const int MAX_THUMBNAIL_SIZE = 200;
        private const int ITEM_PADDING = 8;

        // 資料來源
        private List<GalleryItem> _items = new List<GalleryItem>();
        private ConcurrentDictionary<int, Image> _thumbnailCache = new ConcurrentDictionary<int, Image>();
        private CancellationTokenSource _loadCts;

        // 虛擬化相關
        private int _visibleStartIndex = 0;
        private int _visibleCount = 0;
        private List<GalleryTile> _tiles = new List<GalleryTile>();

        // 選取
        private int _selectedIndex = -1;
        private GalleryTile _selectedTile = null;

        // 延遲載入
        private System.Windows.Forms.Timer _loadDelayTimer;
        private int _pendingLoadStart = -1;
        private int _pendingLoadEnd = -1;

        // 事件
        public event EventHandler<GalleryItemSelectedEventArgs> ItemSelected;
        public event EventHandler<GalleryItemSelectedEventArgs> ItemDoubleClicked;

        // 縮圖載入委派
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<int, Image> ThumbnailLoader { get; set; }

        public ucGalleryViewer()
        {
            InitializeComponent();

            // 初始化延遲載入 Timer
            _loadDelayTimer = new System.Windows.Forms.Timer();
            _loadDelayTimer.Interval = 150; // 150ms 延遲
            _loadDelayTimer.Tick += LoadDelayTimer_Tick;
        }

        private void LoadDelayTimer_Tick(object sender, EventArgs e)
        {
            _loadDelayTimer.Stop();
            if (_pendingLoadStart >= 0 && _pendingLoadEnd > _pendingLoadStart)
            {
                // 重新建立 CancellationTokenSource
                _loadCts?.Dispose();
                _loadCts = new CancellationTokenSource();
                LoadThumbnailsAsync(_pendingLoadStart, _pendingLoadEnd);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 工具列面板
            this.panelToolbar = new Panel();
            this.panelToolbar.Dock = DockStyle.Top;
            this.panelToolbar.Height = 32;
            this.panelToolbar.Padding = new Padding(4);

            // 縮圖大小標籤
            this.lblSize = new Label();
            this.lblSize.Text = "縮圖大小:";
            this.lblSize.AutoSize = true;
            this.lblSize.Location = new Point(8, 8);
            this.panelToolbar.Controls.Add(this.lblSize);

            // 縮圖大小滑桿
            this.tbThumbnailSize = new TrackBar();
            this.tbThumbnailSize.Minimum = MIN_THUMBNAIL_SIZE;
            this.tbThumbnailSize.Maximum = MAX_THUMBNAIL_SIZE;
            this.tbThumbnailSize.Value = _thumbnailSize;
            this.tbThumbnailSize.TickFrequency = 16;
            this.tbThumbnailSize.SmallChange = 8;
            this.tbThumbnailSize.LargeChange = 32;
            this.tbThumbnailSize.Location = new Point(70, 2);
            this.tbThumbnailSize.Size = new Size(200, 28);
            this.tbThumbnailSize.Scroll += TbThumbnailSize_Scroll;
            this.panelToolbar.Controls.Add(this.tbThumbnailSize);

            // 內容面板（包含 FlowLayoutPanel 和捲軸）
            this.panelContent = new Panel();
            this.panelContent.Dock = DockStyle.Fill;

            // 捲軸
            this.vScrollBar = new VScrollBar();
            this.vScrollBar.Dock = DockStyle.Right;
            this.vScrollBar.Scroll += VScrollBar_Scroll;
            this.panelContent.Controls.Add(this.vScrollBar);

            // FlowLayoutPanel
            this.flowPanel = new FlowLayoutPanel();
            this.flowPanel.Dock = DockStyle.Fill;
            this.flowPanel.AutoScroll = false;
            this.flowPanel.WrapContents = true;
            this.flowPanel.FlowDirection = FlowDirection.LeftToRight;
            this.flowPanel.BackColor = Color.FromArgb(45, 45, 48);
            this.flowPanel.Padding = new Padding(ITEM_PADDING);
            this.flowPanel.Resize += FlowPanel_Resize;
            this.flowPanel.MouseWheel += FlowPanel_MouseWheel;
            this.panelContent.Controls.Add(this.flowPanel);

            // 加入控件
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelToolbar);

            this.Size = new Size(600, 400);
            this.ResumeLayout(false);

            // 設定 DoubleBuffered
            typeof(FlowLayoutPanel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(this.flowPanel, true, null);
        }

        private void TbThumbnailSize_Scroll(object sender, EventArgs e)
        {
            _thumbnailSize = tbThumbnailSize.Value;
            RefreshLayout();
        }

        private void VScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            UpdateVisibleItems();
        }

        private void FlowPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            // 增加滾輪步進：每次滾動 3 行高度
            int itemHeight = _thumbnailSize + 24 + ITEM_PADDING * 2;
            int scrollAmount = itemHeight * 3;
            int delta = e.Delta > 0 ? -scrollAmount : scrollAmount;
            int newValue = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum - vScrollBar.LargeChange + 1, vScrollBar.Value + delta));
            if (newValue != vScrollBar.Value)
            {
                vScrollBar.Value = newValue;
                UpdateVisibleItems();
            }
        }

        private void FlowPanel_Resize(object sender, EventArgs e)
        {
            UpdateScrollBar();
            UpdateVisibleItems();
        }

        /// <summary>
        /// 設定要顯示的項目
        /// </summary>
        public void SetItems(List<GalleryItem> items)
        {
            // 取消之前的載入
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();

            _items = items ?? new List<GalleryItem>();
            _thumbnailCache.Clear();
            _selectedIndex = -1;
            _selectedTile = null;

            UpdateScrollBar();
            UpdateVisibleItems();
        }

        /// <summary>
        /// 清除所有項目
        /// </summary>
        public void Clear()
        {
            _loadCts?.Cancel();
            _items.Clear();
            _thumbnailCache.Clear();
            _selectedIndex = -1;
            _selectedTile = null;

            flowPanel.Controls.Clear();
            _tiles.Clear();

            vScrollBar.Value = 0;
            vScrollBar.Maximum = 0;
        }

        /// <summary>
        /// 選取指定索引的項目
        /// </summary>
        public void SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count)
                return;

            _selectedIndex = index;

            // 更新視覺選取狀態
            foreach (var tile in _tiles)
            {
                tile.IsSelected = (tile.ItemIndex == index);
                if (tile.IsSelected)
                    _selectedTile = tile;
            }

            // 確保選取項目可見
            EnsureVisible(index);
        }

        public int SelectedIndex => _selectedIndex;

        public GalleryItem SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count
            ? _items[_selectedIndex]
            : null;

        private void UpdateScrollBar()
        {
            if (_items.Count == 0)
            {
                vScrollBar.Maximum = 0;
                vScrollBar.Value = 0;
                return;
            }

            int itemWidth = _thumbnailSize + ITEM_PADDING * 2;
            int itemHeight = _thumbnailSize + 24 + ITEM_PADDING * 2; // 24 for label height

            int panelWidth = flowPanel.ClientSize.Width - vScrollBar.Width;
            int itemsPerRow = Math.Max(1, panelWidth / itemWidth);
            int rowCount = (int)Math.Ceiling((double)_items.Count / itemsPerRow);
            int totalHeight = rowCount * itemHeight;

            int visibleHeight = flowPanel.ClientSize.Height;

            if (totalHeight <= visibleHeight)
            {
                vScrollBar.Maximum = 0;
                vScrollBar.Value = 0;
                vScrollBar.Enabled = false;
            }
            else
            {
                vScrollBar.Enabled = true;
                vScrollBar.Maximum = totalHeight;
                vScrollBar.LargeChange = Math.Max(1, visibleHeight);
                vScrollBar.SmallChange = itemHeight;

                if (vScrollBar.Value > vScrollBar.Maximum - vScrollBar.LargeChange + 1)
                    vScrollBar.Value = Math.Max(0, vScrollBar.Maximum - vScrollBar.LargeChange + 1);
            }
        }

        private void UpdateVisibleItems()
        {
            if (_items.Count == 0)
            {
                flowPanel.Controls.Clear();
                _tiles.Clear();
                return;
            }

            int itemWidth = _thumbnailSize + ITEM_PADDING * 2;
            int itemHeight = _thumbnailSize + 24 + ITEM_PADDING * 2;

            int panelWidth = flowPanel.ClientSize.Width - vScrollBar.Width;
            int panelHeight = flowPanel.ClientSize.Height;
            int itemsPerRow = Math.Max(1, panelWidth / itemWidth);

            int scrollOffset = vScrollBar.Value;
            int startRow = scrollOffset / itemHeight;
            int visibleRows = (int)Math.Ceiling((double)panelHeight / itemHeight) + 1;

            int startIndex = startRow * itemsPerRow;
            int endIndex = Math.Min(_items.Count, (startRow + visibleRows) * itemsPerRow);
            int neededTiles = endIndex - startIndex;

            // 計算 Y 偏移
            int yOffsetBase = -(scrollOffset % itemHeight);

            // 如果可見範圍沒變，只更新位置
            if (startIndex == _visibleStartIndex && neededTiles == _visibleCount && _tiles.Count > 0)
            {
                for (int i = 0; i < _tiles.Count; i++)
                {
                    int row = i / itemsPerRow;
                    int col = i % itemsPerRow;
                    _tiles[i].Location = new Point(col * itemWidth + ITEM_PADDING, row * itemHeight + ITEM_PADDING + yOffsetBase);
                }
                return;
            }

            // 暫停佈局和重繪
            flowPanel.SuspendLayout();

            // 確保有足夠的 tiles（只增加，不刪除）
            while (_tiles.Count < neededTiles)
            {
                var tile = new GalleryTile();
                tile.ThumbnailSize = _thumbnailSize;
                tile.Click += Tile_Click;
                tile.DoubleClick += Tile_DoubleClick;
                _tiles.Add(tile);
                flowPanel.Controls.Add(tile);
            }

            // 隱藏多餘的 tiles（而不是刪除）
            for (int i = neededTiles; i < _tiles.Count; i++)
            {
                _tiles[i].Visible = false;
            }

            // 更新可見 tiles 的內容和位置
            for (int i = 0; i < neededTiles; i++)
            {
                int itemIndex = startIndex + i;
                var tile = _tiles[i];
                var item = _items[itemIndex];

                // 只有當 tile 對應的項目改變時才更新內容
                if (tile.ItemIndex != itemIndex)
                {
                    tile.ItemIndex = itemIndex;
                    tile.SetItem(item.FileName, GetThumbnail(itemIndex));
                }

                tile.ThumbnailSize = _thumbnailSize;
                tile.IsSelected = (itemIndex == _selectedIndex);
                tile.Visible = true;

                if (tile.IsSelected)
                    _selectedTile = tile;

                int row = i / itemsPerRow;
                int col = i % itemsPerRow;
                tile.Location = new Point(col * itemWidth + ITEM_PADDING, row * itemHeight + ITEM_PADDING + yOffsetBase);
            }

            _visibleStartIndex = startIndex;
            _visibleCount = neededTiles;

            flowPanel.ResumeLayout(false);

            // 延遲載入縮圖（避免捲動時閃爍）
            _loadCts?.Cancel();
            _pendingLoadStart = startIndex;
            _pendingLoadEnd = endIndex;
            _loadDelayTimer.Stop();
            _loadDelayTimer.Start();
        }

        private Image GetThumbnail(int index)
        {
            if (_thumbnailCache.TryGetValue(index, out var cached))
                return cached;
            return null;
        }

        private async void LoadThumbnailsAsync(int startIndex, int endIndex)
        {
            if (ThumbnailLoader == null)
                return;

            var cts = _loadCts;
            if (cts == null)
                return;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (cts.IsCancellationRequested)
                    return;

                if (_thumbnailCache.ContainsKey(i))
                    continue;

                int index = i; // 捕獲
                try
                {
                    var image = await Task.Run(() => ThumbnailLoader(index), cts.Token);

                    if (cts.IsCancellationRequested)
                    {
                        image?.Dispose();
                        return;
                    }

                    if (image != null)
                    {
                        // 建立縮圖
                        var thumbnail = CreateThumbnail(image, _thumbnailSize);
                        image.Dispose();

                        _thumbnailCache[index] = thumbnail;

                        // 更新對應的 tile
                        if (index >= _visibleStartIndex && index < _visibleStartIndex + _visibleCount)
                        {
                            int tileIndex = index - _visibleStartIndex;
                            if (tileIndex < _tiles.Count)
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    if (tileIndex < _tiles.Count && _tiles[tileIndex].ItemIndex == index)
                                    {
                                        _tiles[tileIndex].SetThumbnail(thumbnail);
                                    }
                                }));
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // 忽略單一縮圖載入錯誤
                }
            }
        }

        private Image CreateThumbnail(Image source, int size)
        {
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

            width = Math.Max(1, width);
            height = Math.Max(1, height);

            var thumbnail = new Bitmap(size, size);
            using (var g = Graphics.FromImage(thumbnail))
            {
                g.Clear(Color.FromArgb(60, 60, 65));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                int x = (size - width) / 2;
                int y = (size - height) / 2;
                g.DrawImage(source, x, y, width, height);
            }

            return thumbnail;
        }

        private void RefreshLayout()
        {
            // 縮圖大小改變時，清除快取並重新載入
            foreach (var img in _thumbnailCache.Values)
            {
                img?.Dispose();
            }
            _thumbnailCache.Clear();

            // 更新每個 tile 的大小
            foreach (var tile in _tiles)
            {
                tile.ThumbnailSize = _thumbnailSize;
            }

            UpdateScrollBar();
            UpdateVisibleItems();
        }

        private void EnsureVisible(int index)
        {
            int itemHeight = _thumbnailSize + 24 + ITEM_PADDING * 2;
            int panelWidth = flowPanel.ClientSize.Width - vScrollBar.Width;
            int itemWidth = _thumbnailSize + ITEM_PADDING * 2;
            int itemsPerRow = Math.Max(1, panelWidth / itemWidth);

            int row = index / itemsPerRow;
            int itemTop = row * itemHeight;
            int itemBottom = itemTop + itemHeight;

            int visibleTop = vScrollBar.Value;
            int visibleBottom = visibleTop + flowPanel.ClientSize.Height;

            if (itemTop < visibleTop)
            {
                vScrollBar.Value = Math.Max(0, itemTop);
                UpdateVisibleItems();
            }
            else if (itemBottom > visibleBottom)
            {
                vScrollBar.Value = Math.Min(vScrollBar.Maximum - vScrollBar.LargeChange + 1, itemBottom - flowPanel.ClientSize.Height);
                UpdateVisibleItems();
            }
        }

        private void Tile_Click(object sender, EventArgs e)
        {
            if (sender is GalleryTile tile)
            {
                // 更新選取狀態
                if (_selectedTile != null && _selectedTile != tile)
                    _selectedTile.IsSelected = false;

                tile.IsSelected = true;
                _selectedTile = tile;
                _selectedIndex = tile.ItemIndex;

                ItemSelected?.Invoke(this, new GalleryItemSelectedEventArgs(tile.ItemIndex, _items[tile.ItemIndex]));
            }
        }

        private void Tile_DoubleClick(object sender, EventArgs e)
        {
            if (sender is GalleryTile tile)
            {
                ItemDoubleClicked?.Invoke(this, new GalleryItemSelectedEventArgs(tile.ItemIndex, _items[tile.ItemIndex]));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadDelayTimer?.Stop();
                _loadDelayTimer?.Dispose();

                _loadCts?.Cancel();
                _loadCts?.Dispose();

                foreach (var img in _thumbnailCache.Values)
                {
                    img?.Dispose();
                }
                _thumbnailCache.Clear();

                foreach (var tile in _tiles)
                {
                    tile.Dispose();
                }
                _tiles.Clear();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 相簿項目
    /// </summary>
    public class GalleryItem
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public int FileSize { get; set; }
        public int Offset { get; set; }
        public string SourcePak { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// 相簿項目選取事件參數
    /// </summary>
    public class GalleryItemSelectedEventArgs : EventArgs
    {
        public int Index { get; }
        public GalleryItem Item { get; }

        public GalleryItemSelectedEventArgs(int index, GalleryItem item)
        {
            Index = index;
            Item = item;
        }
    }

    /// <summary>
    /// 單一縮圖 tile
    /// </summary>
    internal class GalleryTile : Control
    {
        private PictureBox pictureBox;
        private Label label;
        private int _thumbnailSize = 80;
        private bool _isSelected;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ItemIndex { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                if (_thumbnailSize != value)
                {
                    _thumbnailSize = value;
                    UpdateLayout();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    this.BackColor = _isSelected
                        ? Color.FromArgb(0, 122, 204)
                        : Color.FromArgb(45, 45, 48);
                }
            }
        }

        public GalleryTile()
        {
            this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            pictureBox = new PictureBox();
            pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
            pictureBox.BackColor = Color.FromArgb(60, 60, 65);
            pictureBox.Cursor = Cursors.Hand;
            pictureBox.Click += (s, e) => this.OnClick(e);
            pictureBox.DoubleClick += (s, e) => this.OnDoubleClick(e);

            label = new Label();
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.ForeColor = Color.White;
            label.BackColor = Color.Transparent;
            label.AutoEllipsis = true;
            label.Click += (s, e) => this.OnClick(e);
            label.DoubleClick += (s, e) => this.OnDoubleClick(e);

            this.Controls.Add(pictureBox);
            this.Controls.Add(label);

            this.BackColor = Color.FromArgb(45, 45, 48);
            this.Cursor = Cursors.Hand;

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            int padding = 4;
            this.Size = new Size(_thumbnailSize + padding * 2, _thumbnailSize + 24 + padding * 2);

            pictureBox.Location = new Point(padding, padding);
            pictureBox.Size = new Size(_thumbnailSize, _thumbnailSize);

            label.Location = new Point(0, _thumbnailSize + padding);
            label.Size = new Size(_thumbnailSize + padding * 2, 24);
        }

        public void SetItem(string fileName, Image thumbnail)
        {
            label.Text = fileName;

            if (thumbnail != null)
            {
                pictureBox.Image = thumbnail;
            }
            else
            {
                pictureBox.Image = null;
            }
        }

        public void SetThumbnail(Image thumbnail)
        {
            pictureBox.Image = thumbnail;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pictureBox?.Dispose();
                label?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
