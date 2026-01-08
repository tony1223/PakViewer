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
        private ToolStrip toolStrip;
        private ToolStripLabel lblDatInfo;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripLabel lblFilter;
        private ToolStripTextBox txtFilter;
        private ToolStripButton btnExport;
        private ToolStripButton btnExportAll;
        private SplitContainer splitContainer;
        private ListView lvFiles;
        private ucImgViewer imageViewer;

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
            this.toolStrip = new ToolStrip();
            this.lblDatInfo = new ToolStripLabel();
            this.toolStripSeparator1 = new ToolStripSeparator();
            this.lblFilter = new ToolStripLabel();
            this.txtFilter = new ToolStripTextBox();
            this.btnExport = new ToolStripButton();
            this.btnExportAll = new ToolStripButton();
            this.splitContainer = new SplitContainer();
            this.lvFiles = new ListView();
            this.imageViewer = new ucImgViewer();

            this.toolStrip.SuspendLayout();
            ((ISupportInitialize)this.splitContainer).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();

            // toolStrip
            this.toolStrip.Items.AddRange(new ToolStripItem[] {
                this.lblDatInfo,
                this.toolStripSeparator1,
                this.lblFilter,
                this.txtFilter,
                new ToolStripSeparator(),
                this.btnExport,
                this.btnExportAll
            });
            this.toolStrip.Location = new Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new Size(800, 25);
            this.toolStrip.TabIndex = 0;

            // lblDatInfo
            this.lblDatInfo.Name = "lblDatInfo";
            this.lblDatInfo.Size = new Size(60, 22);
            this.lblDatInfo.Text = "DAT: -";

            // toolStripSeparator1
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new Size(6, 25);

            // lblFilter
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new Size(39, 22);
            this.lblFilter.Text = "篩選:";

            // txtFilter
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new Size(150, 25);
            this.txtFilter.TextChanged += new EventHandler(this.txtFilter_TextChanged);

            // btnExport
            this.btnExport.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new Size(59, 22);
            this.btnExport.Text = "匯出選取";
            this.btnExport.Click += new EventHandler(this.btnExport_Click);

            // btnExportAll
            this.btnExportAll.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new Size(59, 22);
            this.btnExportAll.Text = "匯出全部";
            this.btnExportAll.Click += new EventHandler(this.btnExportAll_Click);

            // splitContainer
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Location = new Point(0, 25);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Size = new Size(800, 575);
            this.splitContainer.SplitterDistance = 350;
            this.splitContainer.TabIndex = 1;

            // lvFiles
            this.lvFiles.Dock = DockStyle.Fill;
            this.lvFiles.FullRowSelect = true;
            this.lvFiles.GridLines = true;
            this.lvFiles.HideSelection = false;
            this.lvFiles.Location = new Point(0, 0);
            this.lvFiles.Name = "lvFiles";
            this.lvFiles.Size = new Size(350, 575);
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

            // imageViewer
            this.imageViewer.Dock = DockStyle.Fill;
            this.imageViewer.Location = new Point(0, 0);
            this.imageViewer.Name = "imageViewer";
            this.imageViewer.Size = new Size(446, 575);
            this.imageViewer.TabIndex = 0;

            // splitContainer.Panel1
            this.splitContainer.Panel1.Controls.Add(this.lvFiles);

            // splitContainer.Panel2
            this.splitContainer.Panel2.Controls.Add(this.imageViewer);

            // ucDatBrowser
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.toolStrip);
            this.Name = "ucDatBrowser";
            this.Size = new Size(800, 600);

            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((ISupportInitialize)this.splitContainer).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
