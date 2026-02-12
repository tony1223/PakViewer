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
using PakViewer.Localization;

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
            ("◀", "▶"),  // bit 0: 對齊
            ("◆", "◇"),  // bit 1: 模式
            ("○", "●"),  // bit 2: 半透明
            ("■", "□"),  // bit 4: 透明背景
            ("☁", "🔥")  // bit 4/5: 雲/煙 (inverted alpha)
        };

        public override string[] SupportedExtensions => new[] { ".til", ".ti2" };
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
                var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
                if (ext == ".ti2")
                {
                    var tileBlocks = MTil.ConvertToL1Til(data);
                    _blocks = tileBlocks.ToList();
                    _version = L1Til.TileVersion.Classic;
                    _tileSize = 24;
                }
                else
                {
                    _blocks = L1Til.Parse(data);
                    _version = L1Til.GetVersion(data);
                    _tileSize = L1Til.GetTileSize(_version);
                }
            }
            catch
            {
                _blocks = null;
            }

            if (_blocks == null || _blocks.Count == 0)
            {
                _control = new Label { Text = I18n.T("Error.LoadTil") };
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
                    (_saveButton = new Button { Text = I18n.T("Button.Save"), Enabled = false }),
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
                Text = I18n.T("Til.BlocksCount", _blocks.Count) + "  " + I18n.T("Til.Version", versionText),
                VerticalAlignment = VerticalAlignment.Center
            };
            toolbar.Items.Add(_infoLabel);

            // 背景色選項
            toolbar.Items.Add(new Label { Text = "  |  ", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Items.Add(new Label { Text = I18n.T("Til.Background"), VerticalAlignment = VerticalAlignment.Center });
            var bgDropdown = new DropDown { Width = 80 };
            bgDropdown.Items.Add(I18n.T("Color.Black"));
            bgDropdown.Items.Add(I18n.T("Color.White"));
            bgDropdown.SelectedIndex = 0;
            bgDropdown.SelectedIndexChanged += (s, ev) =>
            {
                _useWhiteBackground = bgDropdown.SelectedIndex == 1;
                _drawable?.Invalidate();
            };
            toolbar.Items.Add(bgDropdown);

            // 圖例
            toolbar.Items.Add(new Label { Text = "  |  ", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Items.Add(new Label { Text = I18n.T("Til.TransparentBg"), TextColor = Colors.DodgerBlue, VerticalAlignment = VerticalAlignment.Center });

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
                _infoLabel.Text = $"Block #{index}  {I18n.T("Til.Type")} 0x{flags:X2} ({typeInfo})  {I18n.T("Til.DoubleClickEdit")}";
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
                Title = I18n.T("Til.EditBlock", index),
                Size = new Eto.Drawing.Size(280, 360),
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

            // 顯示原始 type code
            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    new Label { Text = I18n.T("Til.Type"), Width = 50, VerticalAlignment = VerticalAlignment.Center },
                    new Label { Text = $"0x{flags:X2}", Font = new Font(SystemFont.Bold), VerticalAlignment = VerticalAlignment.Center }
                }
            });

            // 設定項 - 使用 bitwise 操作保留原始值
            string[] labels = { I18n.T("Til.Align"), I18n.T("Til.Mode"), I18n.T("Til.SemiTrans"), I18n.T("Til.CloudEffect"), I18n.T("Til.SmokeEffect") };
            int[] bits = { 0, 1, 2, 4, 5 };
            string[][] options = {
                new[] { I18n.T("Til.AlignLeft"), I18n.T("Til.AlignRight") },
                new[] { I18n.T("Til.Diamond"), I18n.T("Til.Compressed") },
                new[] { I18n.T("Til.NoWithIcon"), I18n.T("Til.YesWithIcon") },
                new[] { I18n.T("Til.No"), I18n.T("Til.YesWhiteTrans") },
                new[] { I18n.T("Til.No"), I18n.T("Til.YesBlackTrans") }
            };

            var dropdowns = new DropDown[5];

            for (int i = 0; i < 5; i++)
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
                        Width = 120
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
                        // 使用 bitwise 操作，只修改特定 bit，保留其他 bits
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
            var btnCancel = new Button { Text = I18n.T("Button.Cancel") };
            btnCancel.Click += (s, ev) => _editDialog.Close();

            var btnOK = new Button { Text = I18n.T("Button.OK") };
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
                    _infoLabel.Text = I18n.T("Til.BlocksModified", _blocks.Count);
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
            parts.Add((type & 0x01) != 0 ? I18n.T("Til.TypeAlignRight") : I18n.T("Til.TypeAlignLeft"));
            parts.Add((type & 0x02) != 0 ? I18n.T("Til.TypeCompressed") : I18n.T("Til.TypeDiamond"));
            if ((type & 0x04) != 0) parts.Add(I18n.T("Til.TypeSemiTrans"));
            if ((type & 0x10) != 0) parts.Add(I18n.T("Til.TypeCloud"));
            if ((type & 0x20) != 0) parts.Add(I18n.T("Til.TypeSmoke"));
            return string.Join(", ", parts);
        }

        private Eto.Drawing.Color GetPreviewBackColor(byte flags)
        {
            bool hasBit4 = (flags & 0x10) != 0;  // 雲 (白透明)
            bool hasBit5 = (flags & 0x20) != 0;  // 煙 (黑透明)

            if (hasBit5)
                return Eto.Drawing.Color.FromArgb(100, 60, 60);   // 紅褐色 (煙霧標記)
            else if (hasBit4)
                return Eto.Drawing.Color.FromArgb(70, 90, 120);   // 藍色 (雲霧標記)
            else if (_useWhiteBackground)
                return Eto.Drawing.Color.FromArgb(220, 220, 220); // 白色背景
            else
                return Eto.Drawing.Color.FromArgb(50, 50, 50);    // 黑色背景
        }

        private Bitmap RenderBlockToBitmap(byte[] blockData)
        {
            try
            {
                // 使用 L1Til.RenderBlockToBgra 統一渲染邏輯
                var bgraCanvas = new byte[_tileSize * _tileSize * 4];
                L1Til.RenderBlockToBgra(blockData, 0, 0, bgraCanvas, _tileSize, _tileSize,
                    0, 0, 0, applyTypeAlpha: true, transparentBackground: true);

                using var img = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(bgraCanvas, _tileSize, _tileSize);
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

            // 顯示儲存中狀態
            _saveButton.Text = I18n.T("Status.Saving");
            _saveButton.Enabled = false;
            Application.Instance.RunIteration();  // 強制更新 UI

            try
            {
                var newData = L1Til.BuildTil(_blocks);

                // 觸發儲存事件，讓主程式處理實際的 PAK 更新
                OnSaveRequested(newData);

                _data = newData;
                _hasChanges = false;
                _infoLabel.Text = I18n.T("Til.BlocksSaved", _blocks.Count);
                _saveButton.Text = I18n.T("Button.Save");
                // 儲存成功後按鈕保持禁用 (沒有變更)
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{I18n.T("Error.SaveFailed")}: {ex.Message}", I18n.T("Dialog.Error"), MessageBoxType.Error);
                _saveButton.Enabled = true;
                _saveButton.Text = I18n.T("Button.Save");
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
