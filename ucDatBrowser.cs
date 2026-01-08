using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PakViewer.Utility;

namespace PakViewer
{
    /// <summary>
    /// 天M DAT 檔案瀏覽器控件
    /// </summary>
    public class ucDatBrowser : UserControl, IEditorTab
    {
        private IContainer components = null;

        // UI 元件
        private Panel toolbarPanel;
        // Row 1
        private Label lblDatInfo;
        private Button btnExport;
        private Button btnExportAll;
        // Row 2
        private Label lblFilter;
        private TextBox txtFilter;
        private CheckBox chkGalleryMode;
        private SplitContainer splitContainer;
        private ListView lvFiles;
        private FlowLayoutPanel galleryPanel;
        private ucImgViewer imageViewer;
        private ContextMenuStrip galleryContextMenu;

        // Gallery 常數
        private const int THUMB_SIZE = 80;
        private const int THUMB_PANEL_WIDTH = 100;
        private const int THUMB_PANEL_HEIGHT = 110;

        // 資料
        private List<string> _datFilePaths;
        private List<DatTools.DatFile> _datFileObjects;
        private List<DatTools.DatIndexEntry> _allEntries;
        private List<DatTools.DatIndexEntry> _filteredEntries;

        #region IEditorTab Implementation

        public bool HasUnsavedChanges => false;  // DAT Browser 是唯讀的

        public void Save()
        {
            // DAT Browser 不支援儲存
        }

        #endregion

        public ucDatBrowser()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 載入 DAT 檔案
        /// </summary>
        public void LoadDatFiles(string[] filePaths)
        {
            _datFilePaths = new List<string>(filePaths);
            _datFileObjects = new List<DatTools.DatFile>();
            _allEntries = new List<DatTools.DatIndexEntry>();

            foreach (string filePath in filePaths)
            {
                try
                {
                    var datFile = new DatTools.DatFile(filePath);
                    datFile.ReadFooter();
                    datFile.DecryptIndex();
                    datFile.ParseEntries();

                    _datFileObjects.Add(datFile);
                    _allEntries.AddRange(datFile.Entries);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"載入 {Path.GetFileName(filePath)} 失敗：{ex.Message}",
                        "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // 更新 UI
            lblDatInfo.Text = $"DAT: {_datFileObjects.Count} 檔案, {_allEntries.Count} 項目";

            // 初始化篩選
            _filteredEntries = new List<DatTools.DatIndexEntry>(_allEntries);
            lvFiles.VirtualListSize = _filteredEntries.Count;
        }

        /// <summary>
        /// 取得 Tab 標題
        /// </summary>
        public string GetTabTitle()
        {
            if (_datFilePaths == null || _datFilePaths.Count == 0)
                return "DAT Browser";

            if (_datFilePaths.Count == 1)
                return Path.GetFileName(_datFilePaths[0]);

            return $"{Path.GetFileName(_datFilePaths[0])} (+{_datFilePaths.Count - 1})";
        }

        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filter = txtFilter.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(filter))
            {
                _filteredEntries = new List<DatTools.DatIndexEntry>(_allEntries);
            }
            else
            {
                _filteredEntries = _allEntries
                    .Where(e => e.Path.ToLower().Contains(filter))
                    .ToList();
            }

            lvFiles.VirtualListSize = _filteredEntries.Count;
            lvFiles.Invalidate();

            // 更新資訊
            if (_allEntries != null)
            {
                lblDatInfo.Text = $"DAT: {_datFileObjects?.Count ?? 0} 檔案, {_filteredEntries.Count}/{_allEntries.Count} 項目";
            }

            // 如果在相簿模式，重新載入縮圖
            if (chkGalleryMode.Checked)
            {
                PopulateGallery();
            }
        }

        private void chkGalleryMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkGalleryMode.Checked)
            {
                // 相簿模式：顯示 FlowLayoutPanel，隱藏 ListView 和右側預覽
                lvFiles.Visible = false;
                galleryPanel.Visible = true;
                splitContainer.Panel2Collapsed = true;
                PopulateGallery();
            }
            else
            {
                // 正常模式：顯示 ListView，隱藏 FlowLayoutPanel，顯示右側預覽
                galleryPanel.Visible = false;
                lvFiles.Visible = true;
                splitContainer.Panel2Collapsed = false;
                ClearGallery();
            }
        }

        private void PopulateGallery()
        {
            ClearGallery();

            if (_filteredEntries == null || _filteredEntries.Count == 0)
                return;

            galleryPanel.SuspendLayout();

            // 只載入圖片類型的檔案
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

            foreach (var entry in _filteredEntries)
            {
                string ext = Path.GetExtension(entry.Path).ToLower();
                if (!imageExtensions.Contains(ext))
                    continue;

                var thumbPanel = CreateThumbnailPanel(entry);
                galleryPanel.Controls.Add(thumbPanel);
            }

            galleryPanel.ResumeLayout(true);
        }

        private void ClearGallery()
        {
            foreach (Control ctrl in galleryPanel.Controls)
            {
                if (ctrl is Panel p)
                {
                    foreach (Control child in p.Controls)
                    {
                        if (child is PictureBox pb)
                            pb.Image?.Dispose();
                        child.Dispose();
                    }
                }
                ctrl.Dispose();
            }
            galleryPanel.Controls.Clear();
        }

        private Panel CreateThumbnailPanel(DatTools.DatIndexEntry entry)
        {
            var panel = new Panel
            {
                Width = THUMB_PANEL_WIDTH,
                Height = THUMB_PANEL_HEIGHT,
                Margin = new Padding(3),
                Tag = entry
            };

            // 縮圖 PictureBox
            var pb = new PictureBox
            {
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                Location = new Point((THUMB_PANEL_WIDTH - THUMB_SIZE) / 2, 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Tag = entry
            };

            // 載入縮圖
            try
            {
                var datFile = _datFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
                if (datFile != null)
                {
                    byte[] data = datFile.ExtractFile(entry);
                    using (var ms = new MemoryStream(data))
                    {
                        var img = Image.FromStream(ms);
                        pb.Image = new Bitmap(img);
                    }
                }
            }
            catch
            {
                // 載入失敗，顯示空白
            }

            // 檔名標籤
            var lbl = new Label
            {
                Text = Path.GetFileName(entry.Path),
                Location = new Point(0, THUMB_SIZE + 4),
                Size = new Size(THUMB_PANEL_WIDTH, 20),
                TextAlign = ContentAlignment.TopCenter,
                Font = new Font("Microsoft Sans Serif", 7f),
                AutoEllipsis = true
            };

            // 雙擊匯出
            pb.DoubleClick += (s, e) => ExportEntry(entry);
            lbl.DoubleClick += (s, e) => ExportEntry(entry);
            panel.DoubleClick += (s, e) => ExportEntry(entry);

            // 點擊選取
            pb.Click += (s, e) => SelectGalleryItem(panel);
            lbl.Click += (s, e) => SelectGalleryItem(panel);
            panel.Click += (s, e) => SelectGalleryItem(panel);

            // 右鍵選單
            pb.ContextMenuStrip = galleryContextMenu;
            lbl.ContextMenuStrip = galleryContextMenu;
            panel.ContextMenuStrip = galleryContextMenu;

            // 右鍵時也要選取
            pb.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) SelectGalleryItem(panel); };
            lbl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) SelectGalleryItem(panel); };
            panel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) SelectGalleryItem(panel); };

            panel.Controls.Add(pb);
            panel.Controls.Add(lbl);

            return panel;
        }

        private Panel _selectedGalleryItem = null;

        private void SelectGalleryItem(Panel panel)
        {
            // 取消前一個選取
            if (_selectedGalleryItem != null)
            {
                _selectedGalleryItem.BackColor = SystemColors.Control;
            }

            // 選取新的
            _selectedGalleryItem = panel;
            panel.BackColor = SystemColors.Highlight;
        }

        private void ExportSelectedGalleryItem()
        {
            if (_selectedGalleryItem?.Tag is DatTools.DatIndexEntry entry)
            {
                ExportEntry(entry);
            }
        }

        private void CopySelectedGalleryItemToClipboard()
        {
            if (_selectedGalleryItem == null)
                return;

            // 找到 PictureBox 取得圖片
            foreach (Control ctrl in _selectedGalleryItem.Controls)
            {
                if (ctrl is PictureBox pb && pb.Image != null)
                {
                    Clipboard.SetImage(pb.Image);
                    return;
                }
            }
        }

        private void lvFiles_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            ListViewItem createEmptyItem()
            {
                var item = new ListViewItem("");
                item.SubItems.Add("");
                item.SubItems.Add("");
                return item;
            }

            if (_filteredEntries == null || e.ItemIndex < 0 || e.ItemIndex >= _filteredEntries.Count)
            {
                e.Item = createEmptyItem();
                return;
            }

            try
            {
                var entry = _filteredEntries[e.ItemIndex];
                var item = new ListViewItem(entry.Path);
                item.SubItems.Add(FormatFileSize(entry.Size));
                item.SubItems.Add(entry.SourceDatName ?? "");
                item.Tag = entry;
                e.Item = item;
            }
            catch
            {
                e.Item = createEmptyItem();
            }
        }

        private void lvFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            int selectedIndex = lvFiles.SelectedIndices[0];
            if (_filteredEntries == null || selectedIndex >= _filteredEntries.Count)
                return;

            var entry = _filteredEntries[selectedIndex];
            DisplayEntry(entry);
        }

        private void DisplayEntry(DatTools.DatIndexEntry entry)
        {
            try
            {
                var datFile = _datFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
                if (datFile == null)
                    return;

                byte[] data = datFile.ExtractFile(entry);

                string ext = Path.GetExtension(entry.Path).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
                {
                    using (var ms = new MemoryStream(data))
                    {
                        var img = Image.FromStream(ms);
                        imageViewer.Image = new Bitmap(img);
                    }
                }
                else
                {
                    imageViewer.Image = null;
                }
            }
            catch (Exception ex)
            {
                imageViewer.Image = null;
                System.Diagnostics.Debug.WriteLine($"DisplayEntry error: {ex.Message}");
            }
        }

        private void lvFiles_DoubleClick(object sender, EventArgs e)
        {
            // 雙擊匯出
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            int selectedIndex = lvFiles.SelectedIndices[0];
            if (_filteredEntries == null || selectedIndex >= _filteredEntries.Count)
                return;

            var entry = _filteredEntries[selectedIndex];
            ExportEntry(entry);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
            {
                MessageBox.Show("請先選擇要匯出的檔案", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int selectedIndex = lvFiles.SelectedIndices[0];
            if (_filteredEntries == null || selectedIndex >= _filteredEntries.Count)
                return;

            var entry = _filteredEntries[selectedIndex];
            ExportEntry(entry);
        }

        private void btnExportAll_Click(object sender, EventArgs e)
        {
            if (_filteredEntries == null || _filteredEntries.Count == 0)
            {
                MessageBox.Show("沒有可匯出的檔案", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "選擇匯出目錄";
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                string outputDir = dlg.SelectedPath;
                int exported = 0;
                int failed = 0;

                foreach (var entry in _filteredEntries)
                {
                    try
                    {
                        var datFile = _datFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
                        if (datFile == null) continue;

                        byte[] data = datFile.ExtractFile(entry);

                        string outputPath = Path.Combine(outputDir, entry.Path.Replace('/', '\\'));
                        string dir = Path.GetDirectoryName(outputPath);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        File.WriteAllBytes(outputPath, data);
                        exported++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                MessageBox.Show($"匯出完成：成功 {exported}，失敗 {failed}",
                    "匯出結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportEntry(DatTools.DatIndexEntry entry)
        {
            try
            {
                var datFile = _datFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
                if (datFile == null)
                    return;

                using (var dlg = new SaveFileDialog())
                {
                    dlg.FileName = Path.GetFileName(entry.Path);
                    dlg.Filter = "所有檔案|*.*";

                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    byte[] data = datFile.ExtractFile(entry);
                    File.WriteAllBytes(dlg.FileName, data);

                    MessageBox.Show($"已匯出至 {dlg.FileName}", "匯出成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FormatFileSize(long size)
        {
            if (size < 1024)
                return $"{size} B";
            if (size < 1024 * 1024)
                return $"{size / 1024.0:F1} KB";
            return $"{size / (1024.0 * 1024.0):F1} MB";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 清理 DAT 檔案物件
                if (_datFileObjects != null)
                {
                    _datFileObjects.Clear();
                    _datFileObjects = null;
                }

                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolbarPanel = new Panel();
            this.lblDatInfo = new Label();
            this.btnExport = new Button();
            this.btnExportAll = new Button();
            this.lblFilter = new Label();
            this.txtFilter = new TextBox();
            this.chkGalleryMode = new CheckBox();
            this.splitContainer = new SplitContainer();
            this.lvFiles = new ListView();
            this.galleryPanel = new FlowLayoutPanel();
            this.galleryContextMenu = new ContextMenuStrip();
            this.imageViewer = new ucImgViewer();

            this.toolbarPanel.SuspendLayout();
            ((ISupportInitialize)this.splitContainer).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();

            // toolbarPanel (兩行)
            this.toolbarPanel.Controls.Add(this.lblDatInfo);
            this.toolbarPanel.Controls.Add(this.btnExport);
            this.toolbarPanel.Controls.Add(this.btnExportAll);
            this.toolbarPanel.Controls.Add(this.lblFilter);
            this.toolbarPanel.Controls.Add(this.txtFilter);
            this.toolbarPanel.Controls.Add(this.chkGalleryMode);
            this.toolbarPanel.Dock = DockStyle.Top;
            this.toolbarPanel.Location = new Point(0, 0);
            this.toolbarPanel.Name = "toolbarPanel";
            this.toolbarPanel.Size = new Size(800, 56);
            this.toolbarPanel.TabIndex = 0;

            // === Row 1 (Y = 4) ===

            // lblDatInfo
            this.lblDatInfo.AutoSize = true;
            this.lblDatInfo.Location = new Point(5, 7);
            this.lblDatInfo.Name = "lblDatInfo";
            this.lblDatInfo.Size = new Size(50, 12);
            this.lblDatInfo.Text = "DAT: -";

            // btnExport
            this.btnExport.Location = new Point(200, 3);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new Size(70, 23);
            this.btnExport.TabIndex = 1;
            this.btnExport.Text = "匯出選取";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new EventHandler(this.btnExport_Click);

            // btnExportAll
            this.btnExportAll.Location = new Point(275, 3);
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new Size(70, 23);
            this.btnExportAll.TabIndex = 2;
            this.btnExportAll.Text = "匯出全部";
            this.btnExportAll.UseVisualStyleBackColor = true;
            this.btnExportAll.Click += new EventHandler(this.btnExportAll_Click);

            // === Row 2 (Y = 30) ===

            // lblFilter
            this.lblFilter.AutoSize = true;
            this.lblFilter.Location = new Point(5, 33);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new Size(41, 12);
            this.lblFilter.Text = "篩選:";

            // txtFilter
            this.txtFilter.Location = new Point(50, 30);
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new Size(250, 22);
            this.txtFilter.TabIndex = 3;
            this.txtFilter.TextChanged += new EventHandler(this.txtFilter_TextChanged);

            // chkGalleryMode
            this.chkGalleryMode.AutoSize = true;
            this.chkGalleryMode.Location = new Point(320, 32);
            this.chkGalleryMode.Name = "chkGalleryMode";
            this.chkGalleryMode.Size = new Size(72, 16);
            this.chkGalleryMode.TabIndex = 4;
            this.chkGalleryMode.Text = "相簿模式";
            this.chkGalleryMode.UseVisualStyleBackColor = true;
            this.chkGalleryMode.CheckedChanged += new EventHandler(this.chkGalleryMode_CheckedChanged);

            // splitContainer
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Location = new Point(0, 56);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Size = new Size(800, 544);
            this.splitContainer.SplitterDistance = 350;
            this.splitContainer.TabIndex = 1;

            // lvFiles
            this.lvFiles.Dock = DockStyle.Fill;
            this.lvFiles.FullRowSelect = true;
            this.lvFiles.GridLines = true;
            this.lvFiles.HideSelection = false;
            this.lvFiles.Location = new Point(0, 0);
            this.lvFiles.Name = "lvFiles";
            this.lvFiles.Size = new Size(350, 544);
            this.lvFiles.TabIndex = 0;
            this.lvFiles.UseCompatibleStateImageBehavior = false;
            this.lvFiles.View = View.Details;
            this.lvFiles.VirtualMode = true;
            this.lvFiles.VirtualListSize = 0;
            this.lvFiles.Columns.Add("檔名", 200, HorizontalAlignment.Left);
            this.lvFiles.Columns.Add("大小", 80, HorizontalAlignment.Right);
            this.lvFiles.Columns.Add("來源 DAT", 100, HorizontalAlignment.Left);
            this.lvFiles.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(this.lvFiles_RetrieveVirtualItem);
            this.lvFiles.SelectedIndexChanged += new EventHandler(this.lvFiles_SelectedIndexChanged);
            this.lvFiles.DoubleClick += new EventHandler(this.lvFiles_DoubleClick);

            // galleryPanel
            this.galleryPanel.AutoScroll = true;
            this.galleryPanel.Dock = DockStyle.Fill;
            this.galleryPanel.Location = new Point(0, 0);
            this.galleryPanel.Name = "galleryPanel";
            this.galleryPanel.Size = new Size(350, 544);
            this.galleryPanel.TabIndex = 1;
            this.galleryPanel.Visible = false;

            // galleryContextMenu
            var mnuExportSelected = new ToolStripMenuItem("匯出選取(&E)");
            mnuExportSelected.Click += (s, e) => ExportSelectedGalleryItem();
            var mnuCopyToClipboard = new ToolStripMenuItem("複製到剪貼簿(&C)");
            mnuCopyToClipboard.Click += (s, e) => CopySelectedGalleryItemToClipboard();
            this.galleryContextMenu.Items.AddRange(new ToolStripItem[] {
                mnuExportSelected,
                mnuCopyToClipboard
            });

            // imageViewer
            this.imageViewer.Dock = DockStyle.Fill;
            this.imageViewer.Location = new Point(0, 0);
            this.imageViewer.Name = "imageViewer";
            this.imageViewer.Size = new Size(446, 544);
            this.imageViewer.TabIndex = 0;

            // splitContainer.Panel1
            this.splitContainer.Panel1.Controls.Add(this.galleryPanel);
            this.splitContainer.Panel1.Controls.Add(this.lvFiles);

            // splitContainer.Panel2
            this.splitContainer.Panel2.Controls.Add(this.imageViewer);

            // ucDatBrowser
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.toolbarPanel);
            this.Name = "ucDatBrowser";
            this.Size = new Size(800, 600);

            this.toolbarPanel.ResumeLayout(false);
            this.toolbarPanel.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((ISupportInitialize)this.splitContainer).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
