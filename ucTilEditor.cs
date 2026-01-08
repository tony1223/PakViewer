using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Lin.Helper.Core.Tile;
using PakViewer.Utility;

namespace PakViewer
{
    /// <summary>
    /// TIL 檔案編輯器控件
    /// </summary>
    public class ucTilEditor : UserControl, IEditorTab
    {
        private IContainer components = null;

        // UI 元件
        private ToolStrip toolStrip;
        private ToolStripButton btnSave;
        private ToolStripButton btnSaveAs;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripLabel lblFileName;
        private ToolStripLabel lblBlockCount;
        private Panel mainPanel;
        private FlowLayoutPanel flowLayoutPanel;

        // 資料
        private string _filePath;
        private byte[] _rawData;
        private List<byte[]> _blocks;
        private L1Til.CompressionType _compression;
        private bool _hasUnsavedChanges = false;

        // 常數
        private const int BLOCKS_PER_ROW = 8;
        private const int BLOCK_PANEL_WIDTH = 90;
        private const int BLOCK_PANEL_HEIGHT = 120;
        private const int TILE_SIZE = 24;

        // Bit tooltips
        private static readonly Dictionary<int, (string off, string on)> BitDescriptions = new Dictionary<int, (string, string)>
        {
            { 0, ("置中菱形 (Centered)", "左對齊菱形 (Left-aligned)") },
            { 1, ("Simple Diamond", "Compressed RLE") },
            { 2, ("正常顯示 (Normal)", "半透明 (50% opacity)") },
            { 3, ("無陰影 (No shadow)", "有陰影 (Has shadow)") },
            { 4, ("不透明 (Opaque)", "透明 (Transparent)") }
        };

        #region IEditorTab Implementation

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void Save()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            SaveToFile(_filePath);
        }

        #endregion

        public string FilePath => _filePath;

        public ucTilEditor()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 載入 TIL 檔案
        /// </summary>
        public void LoadFile(string filePath)
        {
            _filePath = filePath;
            _rawData = File.ReadAllBytes(filePath);

            // 解析 TIL
            var result = ImageConvert.Load_TIL_Sheet_Editable(_rawData);
            _blocks = result.blocks;

            // 偵測壓縮類型
            _compression = L1Til.DetectCompression(_rawData);

            // 更新 UI
            lblFileName.Text = Path.GetFileName(filePath);
            lblBlockCount.Text = $"Blocks: {_blocks.Count}";

            // 顯示 blocks
            RenderBlocks();

            _hasUnsavedChanges = false;
        }

        /// <summary>
        /// 渲染所有 blocks
        /// </summary>
        private void RenderBlocks()
        {
            flowLayoutPanel.SuspendLayout();
            flowLayoutPanel.Controls.Clear();

            for (int i = 0; i < _blocks.Count; i++)
            {
                var blockPanel = CreateBlockPanel(i, _blocks[i]);
                flowLayoutPanel.Controls.Add(blockPanel);
            }

            flowLayoutPanel.ResumeLayout(true);
        }

        /// <summary>
        /// 建立單一 block 的面板
        /// </summary>
        private Panel CreateBlockPanel(int index, byte[] blockData)
        {
            var panel = new Panel
            {
                Width = BLOCK_PANEL_WIDTH,
                Height = BLOCK_PANEL_HEIGHT,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2),
                Tag = index
            };

            int yPos = 2;

            // #index 標籤
            var lblIndex = new Label
            {
                Text = $"#{index}",
                Location = new Point(2, yPos),
                Size = new Size(BLOCK_PANEL_WIDTH - 4, 14),
                Font = new Font("Consolas", 8f, FontStyle.Bold)
            };
            panel.Controls.Add(lblIndex);
            yPos += 16;

            // Bit 按鈕區域
            byte flags = blockData.Length > 0 ? blockData[0] : (byte)0;

            int xPos = 2;
            for (int bit = 0; bit < 3; bit++)
            {
                var btnBit = CreateBitButton(index, bit, flags);
                btnBit.Location = new Point(xPos, yPos);
                panel.Controls.Add(btnBit);
                xPos += 22;
            }

            // Type 標籤
            var lblType = new Label
            {
                Text = $"T:{flags:X2}",
                Location = new Point(xPos, yPos),
                Size = new Size(30, 18),
                Font = new Font("Consolas", 7f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(lblType);
            yPos += 22;

            // Tile 預覽
            var pbTile = new PictureBox
            {
                Location = new Point((BLOCK_PANEL_WIDTH - TILE_SIZE * 2) / 2, yPos),
                Size = new Size(TILE_SIZE * 2, TILE_SIZE * 2),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BorderStyle = BorderStyle.FixedSingle,
                Tag = index
            };

            // 渲染單一 block
            var tileBitmap = RenderSingleBlock(blockData);
            pbTile.Image = tileBitmap;

            // 如果 bit2 = 1，加上橘色邊框
            bool hasBit2 = (flags & 0x04) != 0;
            if (hasBit2)
            {
                pbTile.BackColor = Color.Orange;
                pbTile.Padding = new Padding(2);
            }

            panel.Controls.Add(pbTile);

            return panel;
        }

        /// <summary>
        /// 建立 bit 切換按鈕
        /// </summary>
        private Button CreateBitButton(int blockIndex, int bitIndex, byte flags)
        {
            bool isSet = (flags & (1 << bitIndex)) != 0;

            var btn = new Button
            {
                Text = $"B{bitIndex}",
                Size = new Size(20, 18),
                Font = new Font("Consolas", 6f),
                BackColor = isSet ? Color.LightGreen : Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Tag = new Tuple<int, int>(blockIndex, bitIndex)
            };
            btn.FlatAppearance.BorderSize = 1;

            // Tooltip
            var tooltip = new ToolTip();
            var desc = BitDescriptions.ContainsKey(bitIndex) ? BitDescriptions[bitIndex] : ("Off", "On");
            tooltip.SetToolTip(btn, $"Bit{bitIndex}: {(isSet ? desc.Item2 : desc.Item1)}\n雙擊切換");

            // 雙擊切換
            btn.DoubleClick += BtnBit_DoubleClick;

            return btn;
        }

        /// <summary>
        /// Bit 按鈕雙擊事件
        /// </summary>
        private void BtnBit_DoubleClick(object sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is Tuple<int, int> tag)
            {
                int blockIndex = tag.Item1;
                int bitIndex = tag.Item2;

                // 切換 bit
                byte oldFlags = _blocks[blockIndex][0];
                byte newFlags = (byte)(oldFlags ^ (1 << bitIndex));
                _blocks[blockIndex][0] = newFlags;

                // 更新按鈕顯示
                bool isSet = (newFlags & (1 << bitIndex)) != 0;
                btn.BackColor = isSet ? Color.LightGreen : Color.LightGray;

                // 更新 tooltip
                var desc = BitDescriptions.ContainsKey(bitIndex) ? BitDescriptions[bitIndex] : ("Off", "On");
                var tooltip = new ToolTip();
                tooltip.SetToolTip(btn, $"Bit{bitIndex}: {(isSet ? desc.Item2 : desc.Item1)}\n雙擊切換");

                // 更新同一 panel 的 Type 標籤和 tile 預覽
                var panel = btn.Parent as Panel;
                if (panel != null)
                {
                    UpdateBlockPanel(panel, blockIndex);
                }

                // 標記為已修改
                _hasUnsavedChanges = true;
                UpdateTitle();
            }
        }

        /// <summary>
        /// 更新單一 block panel 的顯示
        /// </summary>
        private void UpdateBlockPanel(Panel panel, int blockIndex)
        {
            byte flags = _blocks[blockIndex][0];

            // 更新 Type 標籤
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Label lbl && lbl.Text.StartsWith("T:"))
                {
                    lbl.Text = $"T:{flags:X2}";
                }
                else if (ctrl is PictureBox pb)
                {
                    // 更新 tile 預覽
                    pb.Image?.Dispose();
                    pb.Image = RenderSingleBlock(_blocks[blockIndex]);

                    // 更新邊框
                    bool hasBit2 = (flags & 0x04) != 0;
                    pb.BackColor = hasBit2 ? Color.Orange : SystemColors.Control;
                }
            }
        }

        /// <summary>
        /// 渲染單一 block 為 Bitmap
        /// </summary>
        private Bitmap RenderSingleBlock(byte[] blockData)
        {
            var bmp = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppArgb);

            if (blockData == null || blockData.Length < 2) return bmp;

            byte flags = blockData[0];
            bool hasBit2 = (flags & 0x04) != 0;

            // 使用 L1Til.RenderBlockToBgra 渲染
            byte[] bgraCanvas = new byte[TILE_SIZE * TILE_SIZE * 4];
            L1Til.RenderBlockToBgra(blockData, 0, 0, bgraCanvas, TILE_SIZE, TILE_SIZE, 0, 0, 0, applyTypeAlpha: hasBit2);

            // 將 BGRA 資料複製到 Bitmap
            var bmpData = bmp.LockBits(new Rectangle(0, 0, TILE_SIZE, TILE_SIZE),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(bgraCanvas, 0, bmpData.Scan0, bgraCanvas.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        /// <summary>
        /// 更新標題 (顯示是否有未儲存變更)
        /// </summary>
        private void UpdateTitle()
        {
            string marker = _hasUnsavedChanges ? " *" : "";
            lblFileName.Text = Path.GetFileName(_filePath) + marker;
        }

        /// <summary>
        /// 儲存到檔案
        /// </summary>
        private void SaveToFile(string filePath)
        {
            try
            {
                // 重新編碼 TIL
                byte[] newData = ReEncodeTil();
                File.WriteAllBytes(filePath, newData);

                _filePath = filePath;
                _rawData = newData;
                _hasUnsavedChanges = false;
                UpdateTitle();

                MessageBox.Show($"已儲存至 {filePath}", "儲存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 重新編碼 TIL 檔案
        /// </summary>
        private byte[] ReEncodeTil()
        {
            // 計算總大小
            int totalSize = 4; // header (block count)
            foreach (var block in _blocks)
            {
                totalSize += 4 + block.Length; // offset size + block data
            }

            var result = new byte[totalSize];
            int pos = 0;

            // 寫入 block count
            var countBytes = BitConverter.GetBytes(_blocks.Count);
            Array.Copy(countBytes, 0, result, pos, 4);
            pos += 4;

            // 寫入每個 block
            foreach (var block in _blocks)
            {
                Array.Copy(block, 0, result, pos, block.Length);
                pos += block.Length;
            }

            return result;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                btnSaveAs_Click(sender, e);
                return;
            }
            SaveToFile(_filePath);
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "TIL 檔案|*.til|所有檔案|*.*";
                dialog.FileName = Path.GetFileName(_filePath);
                dialog.InitialDirectory = Path.GetDirectoryName(_filePath);

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SaveToFile(dialog.FileName);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolStrip = new ToolStrip();
            this.btnSave = new ToolStripButton();
            this.btnSaveAs = new ToolStripButton();
            this.toolStripSeparator1 = new ToolStripSeparator();
            this.lblFileName = new ToolStripLabel();
            this.lblBlockCount = new ToolStripLabel();
            this.mainPanel = new Panel();
            this.flowLayoutPanel = new FlowLayoutPanel();

            this.toolStrip.SuspendLayout();
            this.mainPanel.SuspendLayout();
            this.SuspendLayout();

            // toolStrip
            this.toolStrip.Items.AddRange(new ToolStripItem[] {
                this.btnSave,
                this.btnSaveAs,
                this.toolStripSeparator1,
                this.lblFileName,
                this.lblBlockCount
            });
            this.toolStrip.Location = new Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new Size(800, 25);
            this.toolStrip.TabIndex = 0;

            // btnSave
            this.btnSave.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new Size(39, 22);
            this.btnSave.Text = "儲存";
            this.btnSave.Click += new EventHandler(this.btnSave_Click);

            // btnSaveAs
            this.btnSaveAs.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnSaveAs.Name = "btnSaveAs";
            this.btnSaveAs.Size = new Size(59, 22);
            this.btnSaveAs.Text = "另存新檔";
            this.btnSaveAs.Click += new EventHandler(this.btnSaveAs_Click);

            // toolStripSeparator1
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new Size(6, 25);

            // lblFileName
            this.lblFileName.Name = "lblFileName";
            this.lblFileName.Size = new Size(0, 22);

            // lblBlockCount
            this.lblBlockCount.Name = "lblBlockCount";
            this.lblBlockCount.Size = new Size(0, 22);

            // mainPanel
            this.mainPanel.Controls.Add(this.flowLayoutPanel);
            this.mainPanel.Dock = DockStyle.Fill;
            this.mainPanel.Location = new Point(0, 25);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new Size(800, 575);
            this.mainPanel.TabIndex = 1;

            // flowLayoutPanel
            this.flowLayoutPanel.AutoScroll = true;
            this.flowLayoutPanel.Dock = DockStyle.Fill;
            this.flowLayoutPanel.Location = new Point(0, 0);
            this.flowLayoutPanel.Name = "flowLayoutPanel";
            this.flowLayoutPanel.Size = new Size(800, 575);
            this.flowLayoutPanel.TabIndex = 0;

            // ucTilEditor
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Controls.Add(this.mainPanel);
            this.Controls.Add(this.toolStrip);
            this.Name = "ucTilEditor";
            this.Size = new Size(800, 600);

            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.mainPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
