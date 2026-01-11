using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Tile;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace PakViewer.Viewers
{
    /// <summary>
    /// TIL 地圖圖塊編輯器 - 使用 Drawable 高效渲染
    /// </summary>
    public class TilViewer : BaseViewer
    {
        private List<byte[]> _blocks;
        private L1Til.TileVersion _version;
        private int _tileSize;
        private Scrollable _scrollable;
        private Drawable _drawable;
        private Label _infoLabel;
        private Button _saveButton;

        // 編輯狀態
        private int _selectedBlock = -1;
        private int _editingBlockIndex = -1;
        private Dialog _editDialog;

        // 常數
        private const int COLUMNS = 8;
        private const int CELL_SIZE = 56;  // 每格大小
        private const int SPACING = 2;

        // 平行渲染
        private ConcurrentDictionary<int, Bitmap> _bitmaps = new();
        private volatile bool _isRendering = false;

        // 背景色選項
        private bool _useWhiteBackground = false;

        // Icon 定義
        private static readonly (string icon0, string icon1)[] BitIcons = {
            ("◀", "▶"),  // bit 0
            ("◆", "◇"),  // bit 1
            ("○", "●"),  // bit 2
            ("■", "□")   // bit 4
        };

        public override string[] SupportedExtensions => new[] { ".til" };
        public override bool CanEdit => true;
        public override bool HasChanges => _hasChanges;

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;
            _hasChanges = false;
            _bitmaps.Clear();

            try
            {
                _blocks = L1Til.Parse(data);
                _version = L1Til.GetVersion(data);
                _tileSize = L1Til.GetTileSize(_version);
            }
            catch
            {
                _blocks = null;
            }

            if (_blocks == null || _blocks.Count == 0)
            {
                _control = new Label { Text = "Failed to load TIL file" };
                return;
            }

            BuildUI();
            StartParallelRender();
        }

        private void BuildUI()
        {
            var layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 5), Padding = 5 };

            // Toolbar
            var toolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    (_saveButton = new Button { Text = "儲存變更", Enabled = false }),
                    new Label { Text = "|", VerticalAlignment = VerticalAlignment.Center },
                }
            };
            _saveButton.Click += OnSaveClick;

            var versionText = _version switch
            {
                L1Til.TileVersion.Classic => "Classic (24x24)",
                L1Til.TileVersion.Remaster => "Remaster (48x48)",
                _ => "Unknown"
            };

            _infoLabel = new Label
            {
                Text = $"Blocks: {_blocks.Count}  Version: {versionText}",
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Items.Add(_infoLabel);

            // 背景色選項
            toolbar.Items.Add(new Label { Text = "  |  ", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Items.Add(new Label { Text = "背景:", VerticalAlignment = VerticalAlignment.Center });
            var bgDropdown = new DropDown { Width = 80 };
            bgDropdown.Items.Add("黑色");
            bgDropdown.Items.Add("白色");
            bgDropdown.SelectedIndex = 0;
            bgDropdown.SelectedIndexChanged += (s, ev) =>
            {
                _useWhiteBackground = bgDropdown.SelectedIndex == 1;
                _drawable?.Invalidate();
            };
            toolbar.Items.Add(bgDropdown);

            // 圖例
            toolbar.Items.Add(new Label { Text = "  |  ", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Items.Add(new Label { Text = "□透明背景", TextColor = Colors.DodgerBlue, VerticalAlignment = VerticalAlignment.Center });

            layout.AddRow(toolbar);

            // Drawable 畫布
            int rows = (_blocks.Count + COLUMNS - 1) / COLUMNS;
            int width = COLUMNS * (CELL_SIZE + SPACING) + SPACING;
            int height = rows * (CELL_SIZE + SPACING) + SPACING;

            _drawable = new Drawable
            {
                Size = new Eto.Drawing.Size(width, height),
                BackgroundColor = Colors.DarkGray
            };
            _drawable.Paint += OnPaint;
            _drawable.MouseDown += OnMouseDown;
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

        private void StartParallelRender()
        {
            if (_isRendering) return;
            _isRendering = true;

            Task.Run(() =>
            {
                Parallel.ForEach(
                    Enumerable.Range(0, _blocks.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    index =>
                    {
                        if (_blocks == null || index >= _blocks.Count) return;
                        var bitmap = RenderBlockToBitmap(_blocks[index]);
                        if (bitmap != null)
                            _bitmaps[index] = bitmap;
                    });

                _isRendering = false;
                Application.Instance.Invoke(() => _drawable?.Invalidate());
            });
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (_blocks == null) return;

            var g = e.Graphics;
            var clip = e.ClipRectangle;

            for (int i = 0; i < _blocks.Count; i++)
            {
                int col = i % COLUMNS;
                int row = i / COLUMNS;
                int x = col * (CELL_SIZE + SPACING) + SPACING;
                int y = row * (CELL_SIZE + SPACING) + SPACING;

                // 只繪製可見區域
                if (y + CELL_SIZE < clip.Top || y > clip.Bottom) continue;
                if (x + CELL_SIZE < clip.Left || x > clip.Right) continue;

                byte flags = _blocks[i].Length > 0 ? _blocks[i][0] : (byte)0;

                // 背景色
                var bgColor = GetPreviewBackColor(flags);
                g.FillRectangle(bgColor, x, y, CELL_SIZE, CELL_SIZE);

                // 圖片
                if (_bitmaps.TryGetValue(i, out var bitmap))
                {
                    float imgSize = CELL_SIZE - 8;
                    float imgX = x + 4;
                    float imgY = y + 4;
                    g.DrawImage(bitmap, imgX, imgY, imgSize, imgSize);
                }

                // 選中框
                if (i == _selectedBlock)
                {
                    g.DrawRectangle(Colors.Yellow, x, y, CELL_SIZE - 1, CELL_SIZE - 1);
                    g.DrawRectangle(Colors.Yellow, x + 1, y + 1, CELL_SIZE - 3, CELL_SIZE - 3);
                }

                // Index 和 icons (簡化顯示)
                var indexText = $"#{i}";
                bool hasBit4 = (flags & 0x10) != 0;
                var textColor = (_useWhiteBackground && !hasBit4) ? Colors.Black : Colors.White;
                g.DrawText(new Font(SystemFont.Default, 7), textColor, x + 2, y + CELL_SIZE - 12, indexText);
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            int col = (int)(e.Location.X - SPACING) / (CELL_SIZE + SPACING);
            int row = (int)(e.Location.Y - SPACING) / (CELL_SIZE + SPACING);
            int index = row * COLUMNS + col;

            if (index >= 0 && index < _blocks.Count && col < COLUMNS)
            {
                _selectedBlock = index;
                byte flags = _blocks[index].Length > 0 ? _blocks[index][0] : (byte)0;
                string typeInfo = GetTypeInfo(flags);
                _infoLabel.Text = $"Block #{index}  Type: 0x{flags:X2} ({typeInfo})  [雙擊編輯]";
                _drawable.Invalidate();
            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_selectedBlock >= 0 && _selectedBlock < _blocks.Count)
            {
                ShowEditDialog(_selectedBlock);
            }
        }

        private void ShowEditDialog(int index)
        {
            byte flags = _blocks[index].Length > 0 ? _blocks[index][0] : (byte)0;
            byte originalFlags = flags;

            _editDialog = new Dialog
            {
                Title = $"編輯 Block #{index}",
                Size = new Eto.Drawing.Size(280, 280),
                Padding = 10
            };

            var layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(5, 8) };

            // 預覽圖 - 使用 Drawable 來正確縮放
            Bitmap currentPreviewBitmap = null;
            _bitmaps.TryGetValue(index, out currentPreviewBitmap);

            var previewDrawable = new Drawable
            {
                Size = new Eto.Drawing.Size(72, 72),
                BackgroundColor = GetPreviewBackColor(flags)
            };
            previewDrawable.Paint += (s, ev) =>
            {
                if (currentPreviewBitmap != null)
                {
                    ev.Graphics.DrawImage(currentPreviewBitmap, 0, 0, 72, 72);
                }
            };

            // 用於更新預覽的 Action
            Action updatePreview = () =>
            {
                var tempBlock = (byte[])_blocks[index].Clone();
                tempBlock[0] = flags;
                currentPreviewBitmap = RenderBlockToBitmap(tempBlock);
                previewDrawable.BackgroundColor = GetPreviewBackColor(flags);
                previewDrawable.Invalidate();
            };

            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Items = { new StackLayoutItem(null, true), previewDrawable, new StackLayoutItem(null, true) }
            });

            // 設定項
            string[] labels = { "對齊", "模式", "半透", "背景" };
            int[] bits = { 0, 1, 2, 4 };
            string[][] options = {
                new[] { "向左 ◀", "向右 ▶" },
                new[] { "完整 ◆", "部分 ◇" },
                new[] { "否 ○", "是 ●" },
                new[] { "填滿", "透明" }
            };

            var dropdowns = new DropDown[4];

            for (int i = 0; i < 4; i++)
            {
                int bitIndex = bits[i];
                bool currentValue = (flags & (1 << bitIndex)) != 0;

                var row = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 10 };
                row.Items.Add(new Label { Text = labels[i], Width = 50, VerticalAlignment = VerticalAlignment.Center });

                if (bitIndex == 1)
                {
                    // bit 1 唯讀
                    row.Items.Add(new Label
                    {
                        Text = currentValue ? options[i][1] : options[i][0],
                        TextColor = Colors.Gray,
                        Width = 100
                    });
                    dropdowns[i] = null;
                }
                else
                {
                    var dropdown = new DropDown { Width = 120 };
                    dropdown.Items.Add(options[i][0]);
                    dropdown.Items.Add(options[i][1]);
                    dropdown.SelectedIndex = currentValue ? 1 : 0;

                    int capturedBit = bitIndex;
                    dropdown.SelectedIndexChanged += (s, ev) =>
                    {
                        bool newVal = dropdown.SelectedIndex == 1;
                        if (newVal)
                            flags |= (byte)(1 << capturedBit);
                        else
                            flags &= (byte)~(1 << capturedBit);

                        updatePreview();
                    };
                    row.Items.Add(dropdown);
                    dropdowns[i] = dropdown;
                }

                layout.AddRow(row);
            }

            // 按鈕
            var btnCancel = new Button { Text = "取消" };
            btnCancel.Click += (s, ev) => _editDialog.Close();

            var btnOK = new Button { Text = "確定" };
            btnOK.Click += (s, ev) =>
            {
                if (flags != originalFlags)
                {
                    _blocks[index][0] = flags;
                    _hasChanges = true;
                    _saveButton.Enabled = true;

                    // 更新快取
                    var newBmp = RenderBlockToBitmap(_blocks[index]);
                    if (newBmp != null)
                        _bitmaps[index] = newBmp;

                    _drawable.Invalidate();
                    _infoLabel.Text = $"Blocks: {_blocks.Count}  (已修改)";
                }
                _editDialog.Close();
            };

            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items = { new StackLayoutItem(null, true), btnCancel, btnOK }
            });

            _editDialog.Content = layout;
            _editDialog.ShowModal();
        }

        private string GetTypeInfo(byte type)
        {
            var parts = new List<string>();
            parts.Add((type & 0x01) != 0 ? "左對齊" : "右對齊");
            parts.Add((type & 0x02) != 0 ? "壓縮" : "菱形");
            if ((type & 0x04) != 0) parts.Add("半透明");
            if ((type & 0x10) != 0) parts.Add("透明背景");
            return string.Join(", ", parts);
        }

        private Eto.Drawing.Color GetPreviewBackColor(byte flags)
        {
            bool hasBit4 = (flags & 0x10) != 0;

            if (hasBit4)
                return Eto.Drawing.Color.FromArgb(70, 90, 120);   // 藍色 (透明背景標記)
            else if (_useWhiteBackground)
                return Eto.Drawing.Color.FromArgb(220, 220, 220); // 白色背景
            else
                return Eto.Drawing.Color.FromArgb(50, 50, 50);    // 黑色背景
        }

        private Bitmap RenderBlockToBitmap(byte[] blockData)
        {
            try
            {
                byte flags = blockData.Length > 0 ? blockData[0] : (byte)0;
                bool hasBit2 = (flags & 0x04) != 0;

                var rgb555Canvas = new ushort[_tileSize * _tileSize];
                L1Til.RenderBlock(blockData, 0, 0, rgb555Canvas, _tileSize, _tileSize);

                using var img = new Image<Bgra32>(_tileSize, _tileSize);
                for (int py = 0; py < _tileSize; py++)
                {
                    for (int px = 0; px < _tileSize; px++)
                    {
                        int idx = py * _tileSize + px;
                        ushort rgb555 = rgb555Canvas[idx];

                        if (rgb555 == 0)
                        {
                            img[px, py] = new Bgra32(0, 0, 0, 0);
                        }
                        else
                        {
                            int r5 = (rgb555 >> 10) & 0x1F;
                            int g5 = (rgb555 >> 5) & 0x1F;
                            int b5 = rgb555 & 0x1F;
                            byte r = (byte)((r5 << 3) | (r5 >> 2));
                            byte g = (byte)((g5 << 3) | (g5 >> 2));
                            byte b = (byte)((b5 << 3) | (b5 >> 2));
                            byte alpha = hasBit2 ? (byte)128 : (byte)255;
                            img[px, py] = new Bgra32(r, g, b, alpha);
                        }
                    }
                }

                using var ms = new MemoryStream();
                img.Save(ms, new PngEncoder());
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (!_hasChanges) return;

            try
            {
                var newData = L1Til.BuildTil(_blocks);
                _data = newData;
                _hasChanges = false;
                _saveButton.Enabled = false;
                _infoLabel.Text = $"Blocks: {_blocks.Count}  (已儲存)";

                MessageBox.Show("變更已套用。請使用 Export 功能儲存檔案。", "儲存成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存失敗: {ex.Message}", "錯誤", MessageBoxType.Error);
            }
        }

        public override byte[] GetModifiedData()
        {
            if (_hasChanges)
                return L1Til.BuildTil(_blocks);
            return _data;
        }

        public override void Dispose()
        {
            foreach (var bmp in _bitmaps.Values)
                bmp?.Dispose();
            _bitmaps.Clear();
            _blocks = null;
            _drawable = null;
            _scrollable = null;
            base.Dispose();
        }
    }
}
