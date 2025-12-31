// Decompiled with JetBrains decompiler
// Type: PakViewer.frmMain
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using PakViewer.Models;
using PakViewer.Properties;
using PakViewer.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace PakViewer
{
  public class frmMain : Form
  {
    private string _PackFileName;
    private bool _IsPackFileProtected;
    private bool _IsDESProtected;  // DES ECB 加密
    private bool _IsExtBFormat;    // _EXTB$ 擴展格式 (tile.idx 等)
    private byte[] _ExtBRawData;   // ExtB 格式的原始資料
    private List<int> _ExtBSortedOffsets;  // ExtB 格式的排序 offset 列表 (用於計算壓縮大小)
    private long _ExtBPakFileSize;  // ExtB PAK 檔案大小
    private Dictionary<string, int> _ExtBCompressionTypes;  // ExtB 格式的壓縮類型 (檔名 -> 壓縮類型)
    private L1PakTools.IndexRecord[] _IndexRecords;
    private string _TextLanguage;
    private frmMain.InviewDataType _InviewData;
    private bool _IsSpriteMode = false;
    private Dictionary<string, (L1PakTools.IndexRecord[] records, bool isProtected)> _SpritePackages;
    // SPR List 模式用的 Sprite 資料映射 (檔名如 "3227-5" -> Record)
    private Dictionary<string, (L1PakTools.IndexRecord record, string pakFile, bool isProtected)> _SprListSpriteRecords;
    // Sprite 分組相關
    private List<SpriteGroup> _SpriteGroups;
    private List<object> _SpriteDisplayItems; // 可以是 SpriteGroup 或 int (realIndex)
    private HashSet<string> _ExpandedGroups;
    // Sprite Mode + list.spr 分類
    private SprListFile _SpriteModeListSpr;  // 載入的 list.spr
    private Dictionary<int, SprListEntry> _SpriteIdToEntry;  // SpriteId -> Entry 映射
    private Button btnLoadListSpr;  // 載入 list.spr 按鈕
    private ComboBox cmbSpriteTypeFilter;  // 類型過濾下拉選單 (Sprite Mode)
    private ComboBox cmbSprListTypeFilter;  // 類型過濾下拉選單 (List SPR Mode)
    private int? _SprListTypeFilter;  // List SPR 模式的類型過濾值
    private int _SpriteSortColumn = -1;
    private bool _SpriteSortAscending = true;
    // Sprite 模式 Tab 相關
    private TabControl tabSpriteMode;
    private TabPage tabSprites;
    private TabPage tabOtherFiles;
    private ListView lvOtherFiles;
    private List<int> _OtherFilesIndexes;
    private IContainer components;
    private MenuStrip menuStrip1;
    private ToolStripMenuItem mnuFile;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripMenuItem mnuQuit;
    private OpenFileDialog dlgOpenFile;
    private SplitContainer splitContainer1;
    private ListView lvIndexInfo;
    private FolderBrowserDialog dlgOpenFolder;
    private ToolStripMenuItem mnuEdit;
    private ToolStripMenuItem mnuCreatResource;
    private ToolStripSeparator toolStripSeparator2;
    private ToolStripMenuItem mnuLanguage;
    private ToolStripMenuItem mnuTools;
    private RichTextBox TextViewer;
    private ToolStripMenuItem mnuOpen;
    private ToolStripMenuItem mnuFiller;
    private ToolStripMenuItem mnuFiller_All;  // 全部選項
    private List<ToolStripMenuItem> _DynamicExtFilters = new List<ToolStripMenuItem>();  // 動態副檔名選項
    private ToolStripMenuItem mnuFiller_Text_html;
    private ToolStripSeparator toolStripSeparator5;
    private ToolStripMenuItem mnuFiller_Text_C;
    private ToolStripMenuItem mnuFiller_Text_J;
    private ToolStripMenuItem mnuFiller_Text_H;
    private ToolStripMenuItem mnuFiller_Text_K;
    private ToolStripSeparator toolStripSeparator4;
    private ContextMenuStrip ctxMenu;
    private ToolStripMenuItem tsmExportTo;
    private ToolStripMenuItem tsmExport;
    private ToolStripMenuItem tsmCopyFileName;
    private ToolStripSeparator toolStripSeparator6;
    private ToolStripMenuItem tsmUnselectAll;
    private ToolStripMenuItem tsmSelectAll;
    private ToolStripSeparator toolStripSeparator10;
    private ToolStripMenuItem tsmDelete;
    private ToolStripMenuItem tsmOptimizePng;
    private StatusStrip statusStrip1;
    private ToolStripStatusLabel tssMessage;
    private ToolStripProgressBar tssProgress;
    private ToolStripStatusLabel tssProgressName;
    private ToolStripSeparator toolStripSeparator7;
    private ToolStripMenuItem mnuFiller_Sprite_spr;
    private ToolStripMenuItem mnuFiller_Tile_til;
    private ToolStripMenuItem mnuFiller_Sprite_img;
    private ToolStripMenuItem mnuFiller_Sprite_png;
    private ToolStripMenuItem mnuFiller_Sprite_tbt;
    private ToolStripMenuItem mnuTools_Export;
    private ToolStripMenuItem mnuTools_ExportTo;
    private ToolStripMenuItem mnuTools_Delete;
    private ToolStripSeparator toolStripSeparator8;
    private ToolStripStatusLabel tssLocker;
    private ToolStripMenuItem mnuTools_ClearSelect;
    private ToolStripMenuItem mnuTools_SelectAll;
    private ToolStripMenuItem mnuTools_Add;
    private ToolStripSeparator toolStripSeparator9;
    private ToolStripMenuItem mnuTools_Update;
    private ToolStripMenuItem mnuTools_OptimizePng;
    private ToolStripStatusLabel tssRecordCount;
    private ToolStripStatusLabel tssShowInListView;
    private ToolStripStatusLabel tssCheckedCount;
    private ToolStripMenuItem mnuRebuild;
    private OpenFileDialog dlgAddFiles;
    private ToolStripMenuItem mnuLanguage_TW;
    private ToolStripMenuItem mnuLanguage_EN;
    private ucSprViewer SprViewer;
    private ucSprListViewer SprListViewer;
    private ucSprActionViewer SprActionViewer;
    private ucSprDetailViewer SprDetailViewer;
    private ToolStripMenuItem mnuOpenSprList;
    private SprListFile _SprListFile;
    private List<SprListEntry> _FilteredSprListEntries;
    private bool _IsSprListMode = false;
    private Panel palSearch;
    private Label label1;
    private TextBox txtSearch;
    private SplitContainer splitContainer2;
    private ucImgViewer ImageViewer;
    private ToolStripSeparator toolStripSeparator3;
    private ToolStripMenuItem tsmCompare;
    private ToolStripMenuItem tsmCompTW;
    private ToolStripMenuItem tsmCompHK;
    private ToolStripMenuItem tsmCompJP;
    private ToolStripMenuItem tsmCompKO;
    private ucTextCompare TextCompViewer;
    private ComboBox cmbIdxFiles;
    private CheckBox chkSpriteMode;
    private CheckBox chkSprListMode;
    private string _LastSprListFile;
    private Label lblFolder;
    private Panel palToolbar;
    private Panel palContentSearch;
    private Label lblContentSearch;
    private TextBox txtContentSearch;
    private Button btnContentSearch;
    private Button btnClearSearch;
    private Label lblExtFilter;
    private ComboBox cmbExtFilter;
    private Label lblLangFilter;
    private ComboBox cmbLangFilter;
    private Button btnSaveText;
    private Button btnCancelEdit;
    private CheckBox chkSkipSaveConfirm;
    private System.ComponentModel.BackgroundWorker bgSearchWorker;
    private int _CurrentEditingRealIndex = -1;
    private System.Windows.Forms.Timer _SelectionTimer;
    private int _LastSelectedCount = -1;
    private bool _TextModified = false;
    private bool _IsCurrentFileXmlEncrypted = false;
    private Encoding _CurrentXmlEncoding = null;
    private string _SelectedFolder;
    private List<int> _FilteredIndexes;
    // 文字搜尋相關
    private string _TextSearchKeyword = "";
    private int _TextSearchLastIndex = 0;
    private HashSet<int> _CheckedIndexes;

    // DAT 模式相關
    private bool _IsDatMode = false;
    private List<string> _DatFiles;  // 開啟的 DAT 檔案路徑列表
    private List<DatTools.DatFile> _DatFileObjects;  // 解析後的 DAT 檔案物件
    private List<DatTools.DatIndexEntry> _AllDatEntries;  // 所有 DAT 檔案的條目 (合併)
    private List<DatTools.DatIndexEntry> _FilteredDatEntries;  // 篩選後的條目
    private Dictionary<string, List<DatTools.DatIndexEntry>> _DatGroups;  // 按目錄分組
    private List<string> _DatGroupKeys;  // 分組鍵列表
    private ToolStripMenuItem mnuOpenDat;  // 開啟 DAT 檔案選單
    private ListView lvDatFiles;  // 檔案列表 ListView
    private ucImgViewer DatImageViewer;  // DAT 圖片檢視器

    // 相簿模式相關
    private bool _IsGalleryMode = false;
    private ucGalleryViewer GalleryViewer;
    private ucGalleryViewer GalleryViewerOther;  // 用於「其他檔案」Tab
    private CheckBox chkGalleryMode;
    private List<GalleryItem> _GalleryItems;
    private List<GalleryItem> _GalleryItemsOther;  // 用於「其他檔案」Tab

    public frmMain()
    {
      this.InitializeComponent();
      this._CheckedIndexes = new HashSet<int>();
      // 初始化背景搜尋 Worker
      this.bgSearchWorker = new System.ComponentModel.BackgroundWorker();
      this.bgSearchWorker.WorkerReportsProgress = true;
      this.bgSearchWorker.WorkerSupportsCancellation = true;
      this.bgSearchWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgSearchWorker_DoWork);
      this.bgSearchWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.bgSearchWorker_ProgressChanged);
      this.bgSearchWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.bgSearchWorker_RunWorkerCompleted);
      this.TextViewer.Dock = DockStyle.Fill;
      this.ImageViewer.Dock = DockStyle.Fill;
      this.SprViewer.Dock = DockStyle.Fill;
      this.TextCompViewer.Dock = DockStyle.Fill;
      string defaultLang = Settings.Default.DefaultLang;
      this.mnuFiller_Text_C.Checked = defaultLang.Contains("-c");
      this.mnuFiller_Text_H.Checked = defaultLang.Contains("-h");
      this.mnuFiller_Text_J.Checked = defaultLang.Contains("-j");
      this.mnuFiller_Text_K.Checked = defaultLang.Contains("-k");
      // 啟用 DoubleBuffered 減少閃爍
      this.lvIndexInfo.GetType().GetProperty("DoubleBuffered",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .SetValue(this.lvIndexInfo, true, null);
      this.lvIndexInfo.Columns.Add("No.", 70, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("FileName", 150, HorizontalAlignment.Left);
      this.lvIndexInfo.Columns.Add("Size(KB)", 80, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("Position", 70, HorizontalAlignment.Right);
      this.splitContainer1.SplitterDistance = 390;
      this.mnuTools.Click += (EventHandler) ((sender, e) =>
      {
        this.mnuTools_Export.Enabled = this.lvIndexInfo.SelectedIndices.Count > 0;
        this.mnuTools_ExportTo.Enabled = this.mnuTools_Export.Enabled;
        this.mnuTools_Delete.Enabled = this.mnuTools_Export.Enabled;
        this.mnuTools_Add.Enabled = this._FilteredIndexes != null && this._FilteredIndexes.Count > 0;
        this.mnuTools_Update.Enabled = this._InviewData == frmMain.InviewDataType.Text && this.TextViewer.Modified;
      });
      this.mnuQuit.Click += (EventHandler) ((sender, e) => this.Close());
      L1PakTools.ShowProgress(this.tssProgress);

      // 初始化選取計算 Timer (每 100ms 檢查一次)
      this._SelectionTimer = new System.Windows.Forms.Timer();
      this._SelectionTimer.Interval = 100;
      this._SelectionTimer.Tick += SelectionTimer_Tick;
      this._SelectionTimer.Start();

      // 啟動時自動載入上次的資料夾和 idx 檔案
      this.LoadLastSession();
    }

    private void LoadLastSession()
    {
      string lastFolder = Settings.Default.LastFolder;
      string lastIdxFile = Settings.Default.LastIdxFile;

      if (string.IsNullOrEmpty(lastFolder) || !Directory.Exists(lastFolder))
        return;

      this._SelectedFolder = lastFolder;
      this.lblFolder.Text = "資料夾：" + this._SelectedFolder;

      // 掃描 idx 檔案
      string[] idxFiles = Directory.GetFiles(this._SelectedFolder, "*.idx", SearchOption.TopDirectoryOnly);
      this.cmbIdxFiles.Items.Clear();
      foreach (string file in idxFiles)
      {
        this.cmbIdxFiles.Items.Add(Path.GetFileName(file));
      }

      if (this.cmbIdxFiles.Items.Count == 0)
        return;

      // 選擇上次的 idx 檔案，或預設選擇 text.idx
      int selectIndex = -1;
      if (!string.IsNullOrEmpty(lastIdxFile))
      {
        selectIndex = this.cmbIdxFiles.Items.IndexOf(lastIdxFile);
      }
      if (selectIndex < 0)
      {
        selectIndex = this.cmbIdxFiles.Items.IndexOf("text.idx");
      }
      if (selectIndex < 0)
      {
        selectIndex = 0;
      }
      this.cmbIdxFiles.SelectedIndex = selectIndex;

      // 如果有上次的 SPR List 檔案，自動勾選 List SPR 模式
      string lastSprListFile = Settings.Default.LastSprListFile;
      if (!string.IsNullOrEmpty(lastSprListFile) && File.Exists(lastSprListFile))
      {
        this.chkSprListMode.Checked = true;
      }
    }

    private void mnuOpen_Click(object sender, EventArgs e)
    {
      this.dlgOpenFolder.Description = "Select Lineage Client Folder";
      // 設定上次的資料夾位置
      string lastFolder = Settings.Default.LastFolder;
      if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
      {
        this.dlgOpenFolder.SelectedPath = lastFolder;
      }

      if (this.dlgOpenFolder.ShowDialog((IWin32Window) this) != DialogResult.OK)
        return;

      // 如果在 SPR List 模式，退出它
      if (this._IsSprListMode)
      {
        this.ExitSprListMode();
      }

      this._SelectedFolder = this.dlgOpenFolder.SelectedPath;
      this.lblFolder.Text = "資料夾：" + this._SelectedFolder;

      // 儲存資料夾位置
      Settings.Default.LastFolder = this._SelectedFolder;
      Settings.Default.Save();

      // Scan for .idx files
      string[] idxFiles = Directory.GetFiles(this._SelectedFolder, "*.idx", SearchOption.TopDirectoryOnly);

      this.cmbIdxFiles.Items.Clear();
      foreach (string file in idxFiles)
      {
        this.cmbIdxFiles.Items.Add(Path.GetFileName(file));
      }

      if (this.cmbIdxFiles.Items.Count > 0)
      {
        // 優先選擇 text.idx
        int textIdxIndex = this.cmbIdxFiles.Items.IndexOf("text.idx");
        if (textIdxIndex >= 0)
        {
          this.cmbIdxFiles.SelectedIndex = textIdxIndex;
        }
        else
        {
          this.cmbIdxFiles.SelectedIndex = 0;
        }
      }
      else
      {
        MessageBox.Show("所選資料夾中找不到 .idx 檔案。");
      }
    }

    private void mnuOpenSprList_Click(object sender, EventArgs e)
    {
      using (var dlg = new OpenFileDialog())
      {
        dlg.Title = "開啟 SPR 列表檔 (list.spr / wlist.spr)";
        dlg.Filter = "SPR 列表檔 (*.spr;*.txt)|*.spr;*.txt|所有檔案 (*.*)|*.*";

        // 使用上次的資料夾
        if (!string.IsNullOrEmpty(this._SelectedFolder))
          dlg.InitialDirectory = this._SelectedFolder;

        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        this.Cursor = Cursors.WaitCursor;
        try
        {
          // 載入 SPR 列表檔案
          this._SprListFile = SprListParser.LoadFromFile(dlg.FileName);
          this._FilteredSprListEntries = this._SprListFile.Entries;
          this._IsSprListMode = true;

          // 更新左側列表欄位
          this.lvIndexInfo.Columns.Clear();
          this.lvIndexInfo.Columns.Add("ID", 60, HorizontalAlignment.Right);
          this.lvIndexInfo.Columns.Add("名稱", 120, HorizontalAlignment.Left);
          this.lvIndexInfo.Columns.Add("圖檔", 60, HorizontalAlignment.Right);
          this.lvIndexInfo.Columns.Add("圖數", 50, HorizontalAlignment.Right);
          this.lvIndexInfo.Columns.Add("類型", 80, HorizontalAlignment.Left);
          this.lvIndexInfo.Columns.Add("動作", 50, HorizontalAlignment.Right);

          // 更新列表
          this.lvIndexInfo.VirtualListSize = this._FilteredSprListEntries.Count;
          this.lvIndexInfo.Invalidate();

          // 載入 Sprite*.idx (從已開啟的客戶端資料夾)
          if (!string.IsNullOrEmpty(this._SelectedFolder))
          {
            LoadSpriteIdxForSprList(this._SelectedFolder);
          }

          // 顯示動作檢視器 (這會建立 SprActionViewer 如果還不存在)
          this.ShowSprActionViewer();

          // 設定 SPR 資料提供者 (必須在 ShowSprActionViewer 之後)
          this.SprActionViewer.SetSpriteDataProvider(this.GetSpriteDataBySpriteKey);

          // 儲存最後開啟的檔案路徑
          this._LastSprListFile = dlg.FileName;
          Settings.Default.LastSprListFile = dlg.FileName;
          Settings.Default.Save();

          // 更新勾選狀態
          this.chkSprListMode.Checked = true;

          int spriteCount = this._SprListSpriteRecords?.Count ?? 0;
          this.tssMessage.Text = $"已載入 SPR 列表: {Path.GetFileName(dlg.FileName)} ({this._SprListFile.Entries.Count} 條目, {spriteCount} 圖檔)";
          this.tssRecordCount.Text = $"Records:{this._SprListFile.Entries.Count}";
          this.tssShowInListView.Text = $"Shown:{this._FilteredSprListEntries.Count}";
        }
        catch (Exception ex)
        {
          MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
          this.Cursor = Cursors.Default;
        }
      }
    }

    private void ShowSprActionViewer()
    {
      // 隱藏其他檢視器
      this.TextViewer.Visible = false;
      this.TextCompViewer.Visible = false;
      this.ImageViewer.Visible = false;
      this.SprViewer.Visible = false;
      this.SprListViewer.Visible = false;

      // 確保 SprActionViewer 存在
      if (this.SprActionViewer == null)
      {
        this.SprActionViewer = new ucSprActionViewer();
        this.SprActionViewer.Dock = DockStyle.Fill;
        this.splitContainer1.Panel2.Controls.Add(this.SprActionViewer);
      }

      this.SprActionViewer.Visible = true;
      this.SprActionViewer.BringToFront();
      this._InviewData = InviewDataType.SprList;
    }

    private void ShowSprDetailViewer()
    {
      // 隱藏其他檢視器
      this.TextViewer.Visible = false;
      this.TextCompViewer.Visible = false;
      this.ImageViewer.Visible = false;
      this.SprViewer.Visible = false;
      this.SprListViewer.Visible = false;
      if (this.SprActionViewer != null)
        this.SprActionViewer.Visible = false;

      // 確保 SprDetailViewer 存在
      if (this.SprDetailViewer == null)
      {
        this.SprDetailViewer = new ucSprDetailViewer();
        this.SprDetailViewer.Dock = DockStyle.Fill;
        this.SprDetailViewer.SetSpriteDataProvider(this.GetSpriteDataByKey);
        this.splitContainer1.Panel2.Controls.Add(this.SprDetailViewer);
      }

      this.SprDetailViewer.Visible = true;
      this.SprDetailViewer.BringToFront();
    }

    private void ShowSpriteGroupDetail(SpriteGroup group)
    {
      // 顯示 detail viewer
      ShowSprDetailViewer();

      // 準備檔案清單
      var files = new List<ucSprDetailViewer.SprFileInfo>();
      foreach (int realIndex in group.ItemIndexes)
      {
        var record = this._IndexRecords[realIndex];
        files.Add(new ucSprDetailViewer.SprFileInfo
        {
          RealIndex = realIndex,
          FileName = record.FileName,
          FileSize = record.FileSize,
          Offset = record.Offset,
          SourcePak = record.SourcePak
        });
      }

      // 取得類型資訊
      string displayPrefix = group.Prefix.TrimEnd('-');
      string typeInfo = "";
      if (this._SpriteIdToEntry != null && int.TryParse(displayPrefix, out int spriteId))
      {
        if (this._SpriteIdToEntry.TryGetValue(spriteId, out var entry))
        {
          typeInfo = $" [{entry.TypeName}:{entry.Name}]";
        }
      }

      // 更新狀態列
      this.tssMessage.Text = $"{displayPrefix} - {files.Count} 個檔案{typeInfo}";

      // 顯示群組的檔案
      this.SprDetailViewer.ShowGroup(group.Prefix, files);
    }

    private void ExitSprListMode()
    {
      this._IsSprListMode = false;
      this._SprListFile = null;
      this._FilteredSprListEntries = null;
      this._SprListTypeFilter = null;
      this._LastSprListFile = null;

      // 清除記憶的 list.spr 路徑
      Settings.Default.LastSprListFile = "";
      Settings.Default.Save();

      // 取消事件處理避免無窮迴圈
      this.chkSprListMode.CheckedChanged -= new EventHandler(this.chkSprListMode_CheckedChanged);
      this.chkSprListMode.Checked = false;
      this.chkSprListMode.CheckedChanged += new EventHandler(this.chkSprListMode_CheckedChanged);

      // 隱藏類型過濾下拉選單
      if (this.cmbSprListTypeFilter != null)
      {
        this.cmbSprListTypeFilter.Visible = false;
      }

      // 還原左側列表欄位
      this.lvIndexInfo.Columns.Clear();
      this.lvIndexInfo.Columns.Add("No.", 70, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("FileName", 150, HorizontalAlignment.Left);
      this.lvIndexInfo.Columns.Add("Size(KB)", 80, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("Position", 70, HorizontalAlignment.Right);

      // 隱藏動作檢視器
      if (this.SprActionViewer != null)
      {
        this.SprActionViewer.Visible = false;
        this.SprActionViewer.Clear();
      }
    }

    private byte[] GetSpriteDataForEntry(int entryId)
    {
      // 嘗試從已載入的 Sprite 資料中取得對應的 .spr 檔案
      if (this._IndexRecords == null || string.IsNullOrEmpty(this._PackFileName))
        return null;

      string sprFileName = entryId.ToString() + ".spr";

      for (int i = 0; i < this._IndexRecords.Length; i++)
      {
        var record = this._IndexRecords[i];
        if (record.FileName.Equals(sprFileName, StringComparison.OrdinalIgnoreCase))
        {
          try
          {
            using (var fs = new FileStream(this._PackFileName.Replace(".idx", ".pak"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
              byte[] data = new byte[record.FileSize];
              fs.Seek(record.Offset, SeekOrigin.Begin);
              fs.Read(data, 0, record.FileSize);
              if (this._IsPackFileProtected)
              {
                data = L1PakTools.Decode(data, 0);
              }
              return data;
            }
          }
          catch
          {
            return null;
          }
        }
      }

      return null;
    }

    private void LoadIdxFile(string idxFileName)
    {
      this._PackFileName = Path.Combine(this._SelectedFolder, idxFileName);
      this.Cursor = Cursors.WaitCursor;
      this.lvIndexInfo.VirtualListSize = 0;

      var sw = System.Diagnostics.Stopwatch.StartNew();
      byte[] indexData = this.LoadIndexData(this._PackFileName);
      long loadMs = sw.ElapsedMilliseconds;

      sw.Restart();
      this._IndexRecords = this.CreatIndexRecords(indexData);
      long createMs = sw.ElapsedMilliseconds;

      // ExtB 格式需要 PAK 檔案大小來計算最後一個 entry 的壓縮大小
      if (this._IsExtBFormat)
      {
        string pakFile = this._PackFileName.Replace(".idx", ".pak");
        if (File.Exists(pakFile))
        {
          this._ExtBPakFileSize = new FileInfo(pakFile).Length;
        }
      }

      sw.Restart();
      if (this._IndexRecords == null)
      {
        int num2 = (int) MessageBox.Show("無法解析檔案。檔案可能已損壞或不是正確的 idx 檔案。");
        this.mnuFiller.Enabled = false;
        this.mnuRebuild.Enabled = false;
        this.tssMessage.Text = "";
      }
      else
      {
        this.BuildDynamicExtensionFilter(this._IndexRecords);
        this.ShowRecords(this._IndexRecords);
        long showMs = sw.ElapsedMilliseconds;
        this.mnuFiller.Enabled = true;
        this.mnuRebuild.Enabled = true;
        this.tssMessage.Text = string.Format("Load:{0}ms | Parse:{1}ms | Show:{2}ms", loadMs, createMs, showMs);
      }
      this.Cursor = Cursors.Default;
    }

    private void cmbIdxFiles_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this._IsSpriteMode)
        return; // Sprite 模式下不處理單一檔案選擇

      if (this.cmbIdxFiles.SelectedItem != null && !string.IsNullOrEmpty(this._SelectedFolder))
      {
        string idxFile = this.cmbIdxFiles.SelectedItem.ToString();
        this.LoadIdxFile(idxFile);
        // 儲存選擇的 idx 檔案
        Settings.Default.LastIdxFile = idxFile;
        Settings.Default.Save();
      }
    }

    private void chkSprListMode_CheckedChanged(object sender, EventArgs e)
    {
      if (this.chkSprListMode.Checked)
      {
        // 嘗試載入上次的檔案
        string lastFile = Settings.Default.LastSprListFile;
        if (!string.IsNullOrEmpty(lastFile) && File.Exists(lastFile))
        {
          LoadSprListFile(lastFile);
        }
        else
        {
          // 檔案不存在，提示選取
          MessageBox.Show("找不到上次開啟的 SPR 列表檔，請選擇一個新檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
          this.chkSprListMode.Checked = false;
          mnuOpenSprList_Click(null, null);
        }
      }
      else
      {
        // 取消勾選 = 退出 SPR List 模式
        if (this._IsSprListMode)
        {
          ExitSprListMode();
        }
      }
    }

    private void LoadSprListFile(string filePath)
    {
      this.Cursor = Cursors.WaitCursor;
      try
      {
        // 載入 SPR 列表檔案
        this._SprListFile = SprListParser.LoadFromFile(filePath);
        this._FilteredSprListEntries = this._SprListFile.Entries;
        this._IsSprListMode = true;
        this._LastSprListFile = filePath;

        // 載入 Sprite*.idx (從已開啟的客戶端資料夾)
        if (!string.IsNullOrEmpty(this._SelectedFolder))
        {
          LoadSpriteIdxForSprList(this._SelectedFolder);
        }

        // 更新左側列表欄位
        this.lvIndexInfo.Columns.Clear();
        this.lvIndexInfo.Columns.Add("ID", 60, HorizontalAlignment.Right);
        this.lvIndexInfo.Columns.Add("名稱", 120, HorizontalAlignment.Left);
        this.lvIndexInfo.Columns.Add("圖檔", 60, HorizontalAlignment.Right);
        this.lvIndexInfo.Columns.Add("圖數", 50, HorizontalAlignment.Right);
        this.lvIndexInfo.Columns.Add("類型", 80, HorizontalAlignment.Left);
        this.lvIndexInfo.Columns.Add("動作", 50, HorizontalAlignment.Right);

        // 初始化類型過濾下拉選單
        InitSprListTypeFilter();

        // 更新列表
        this.lvIndexInfo.VirtualListSize = this._FilteredSprListEntries.Count;
        this.lvIndexInfo.Invalidate();

        // 顯示動作檢視器 (這會建立 SprActionViewer 如果還不存在)
        this.ShowSprActionViewer();

        // 設定 SPR 資料提供者 (必須在 ShowSprActionViewer 之後)
        this.SprActionViewer.SetSpriteDataProvider(this.GetSpriteDataBySpriteKey);

        // 更新勾選狀態 (避免重複觸發事件)
        this.chkSprListMode.CheckedChanged -= new EventHandler(this.chkSprListMode_CheckedChanged);
        this.chkSprListMode.Checked = true;
        this.chkSprListMode.CheckedChanged += new EventHandler(this.chkSprListMode_CheckedChanged);

        int spriteCount = this._SprListSpriteRecords?.Count ?? 0;
        this.tssMessage.Text = $"已載入 SPR 列表: {Path.GetFileName(filePath)} ({this._SprListFile.Entries.Count} 條目, {spriteCount} 圖檔)";
        this.tssRecordCount.Text = $"Records:{this._SprListFile.Entries.Count}";
        this.tssShowInListView.Text = $"Shown:{this._FilteredSprListEntries.Count}";
      }
      catch (Exception ex)
      {
        MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        this.chkSprListMode.Checked = false;
      }
      finally
      {
        this.Cursor = Cursors.Default;
      }
    }

    private void LoadSpriteIdxForSprList(string folder)
    {
      // 清空舊資料
      this._SprListSpriteRecords = new Dictionary<string, (L1PakTools.IndexRecord record, string pakFile, bool isProtected)>();

      if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
      {
        this.tssMessage.Text = $"LoadSpriteIdx: 資料夾無效 ({folder ?? "null"})";
        return;
      }

      // 找到所有 Sprite*.idx 檔案
      string[] spriteFiles = Directory.GetFiles(folder, "Sprite*.idx", SearchOption.TopDirectoryOnly);
      if (spriteFiles.Length == 0)
      {
        this.tssMessage.Text = $"LoadSpriteIdx: 找不到 Sprite*.idx (在 {folder})";
        return;
      }

      foreach (string idxFile in spriteFiles)
      {
        string pakFile = idxFile.Replace(".idx", ".pak");
        if (!File.Exists(pakFile))
          continue;

        // 載入並判斷是否加密 (使用與 LoadIndexData 相同的邏輯)
        byte[] indexData = File.ReadAllBytes(idxFile);
        int recordCount = (indexData.Length - 4) / 28;
        if (indexData.Length < 32 || (indexData.Length - 4) % 28 != 0)
          continue;
        if ((long) BitConverter.ToUInt32(indexData, 0) != (long) recordCount)
          continue;

        bool isProtected = false;
        if (!Regex.IsMatch(Encoding.Default.GetString(indexData, 8, 20), "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
        {
          if (!Regex.IsMatch(L1PakTools.Decode_Index_FirstRecord(indexData).FileName, "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase))
            continue;
          isProtected = true;
          indexData = L1PakTools.Decode(indexData, 4);
        }

        // 直接解析 records (不使用 CreatIndexRecords，因為它依賴 _IsPackFileProtected)
        int startOffset = isProtected ? 0 : 4;
        int recordCount2 = (indexData.Length - startOffset) / 28;

        // 建立檔名 -> Record 的映射
        // 檔名格式: "3227-5.spr" -> key = "3227-5"
        for (int i = 0; i < recordCount2; i++)
        {
          int idx = startOffset + i * 28;
          var record = new L1PakTools.IndexRecord(indexData, idx);
          string key = Path.GetFileNameWithoutExtension(record.FileName);
          this._SprListSpriteRecords[key] = (record, pakFile, isProtected);
        }
      }
    }

    /// <summary>
    /// 給 Sprite 模式用 (spriteKey 格式: "pakFile|fileName")
    /// </summary>
    private byte[] GetSpriteDataByKey(string spriteKey)
    {
      string[] parts = spriteKey.Split('|');
      if (parts.Length != 2)
        return null;

      string pakFile = parts[0];
      string fileName = parts[1];

      if (this._SpritePackages != null && this._SpritePackages.TryGetValue(pakFile, out var packageInfo))
      {
        foreach (var record in packageInfo.records)
        {
          if (record.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
          {
            try
            {
              using (var fs = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
              {
                byte[] data = new byte[record.FileSize];
                fs.Seek(record.Offset, SeekOrigin.Begin);
                fs.Read(data, 0, record.FileSize);
                if (packageInfo.isProtected)
                {
                  data = L1PakTools.Decode(data, 0);
                }
                return data;
              }
            }
            catch
            {
              return null;
            }
          }
        }
      }

      return null;
    }

    /// <summary>
    /// 給 SprList 模式用 (spriteKey 格式: "3225-0" spriteId-subId)
    /// </summary>
    private byte[] GetSpriteDataBySpriteKey(string spriteKey)
    {
      if (this._SprListSpriteRecords != null && this._SprListSpriteRecords.TryGetValue(spriteKey, out var info))
      {
        try
        {
          using (var fs = new FileStream(info.pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          {
            byte[] data = new byte[info.record.FileSize];
            fs.Seek(info.record.Offset, SeekOrigin.Begin);
            fs.Read(data, 0, info.record.FileSize);
            if (info.isProtected)
            {
              data = L1PakTools.Decode(data, 0);
            }
            return data;
          }
        }
        catch
        {
          return null;
        }
      }

      return null;
    }

    // ============ Sprite Mode + list.spr 分類功能 ============

    private void btnLoadListSpr_Click(object sender, EventArgs e)
    {
      using (var dlg = new OpenFileDialog())
      {
        dlg.Title = "選擇 list.spr 檔案";
        dlg.Filter = "SPR List|*.spr;*.txt|All files|*.*";
        if (!string.IsNullOrEmpty(this._SelectedFolder))
          dlg.InitialDirectory = this._SelectedFolder;

        if (dlg.ShowDialog() == DialogResult.OK)
        {
          LoadListSprForSpriteMode(dlg.FileName);
        }
      }
    }

    private void LoadListSprForSpriteMode(string filePath)
    {
      try
      {
        this.Cursor = Cursors.WaitCursor;

        // 解析 list.spr
        this._SpriteModeListSpr = SprListParser.LoadFromFile(filePath);

        // 建立 SpriteId -> Entry 映射
        this._SpriteIdToEntry = new Dictionary<int, SprListEntry>();
        foreach (var entry in this._SpriteModeListSpr.Entries)
        {
          int spriteId = entry.SpriteId;
          if (!this._SpriteIdToEntry.ContainsKey(spriteId))
          {
            this._SpriteIdToEntry[spriteId] = entry;
          }
        }

        // 收集所有類型
        var types = new HashSet<int?>();
        types.Add(null); // 未連結
        foreach (var entry in this._SpriteModeListSpr.Entries)
        {
          types.Add(entry.TypeId);
        }

        // 更新類型過濾下拉選單
        this.cmbSpriteTypeFilter.Items.Clear();
        this.cmbSpriteTypeFilter.Items.Add("全部");
        this.cmbSpriteTypeFilter.Items.Add("未連結");
        foreach (var typeId in types.Where(t => t.HasValue).OrderBy(t => t.Value))
        {
          string typeName = SprListEntry.GetTypeNameById(typeId);
          this.cmbSpriteTypeFilter.Items.Add($"{typeId}: {typeName}");
        }
        this.cmbSpriteTypeFilter.SelectedIndex = 0;

        // 重新建立分組
        BuildSpriteGroups();
        BuildSpriteDisplayItems();
        this.lvIndexInfo.VirtualListSize = this._SpriteDisplayItems.Count;
        this.lvIndexInfo.Invalidate();

        int linkedCount = this._SpriteIdToEntry.Count;
        this.tssMessage.Text = $"已載入 list.spr: {linkedCount} 個連結";
      }
      catch (Exception ex)
      {
        MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        this.Cursor = Cursors.Default;
      }
    }

    private void cmbSpriteTypeFilter_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (!this._IsSpriteMode || this._SpriteModeListSpr == null)
        return;

      // 重新建立分組（會套用類型過濾）
      BuildSpriteGroups();
      BuildSpriteDisplayItems();
      this.lvIndexInfo.VirtualListSize = this._SpriteDisplayItems.Count;
      this.lvIndexInfo.Invalidate();
    }

    // ============ List SPR Mode 類型篩選 ============

    private void InitSprListTypeFilter()
    {
      if (this._SprListFile == null) return;

      // 收集所有類型
      var types = new HashSet<int?>();
      foreach (var entry in this._SprListFile.Entries)
      {
        types.Add(entry.TypeId);
      }

      // 填入下拉選單
      this.cmbSprListTypeFilter.Items.Clear();
      this.cmbSprListTypeFilter.Items.Add("全部類型");
      foreach (var typeId in types.Where(t => t.HasValue).OrderBy(t => t.Value))
      {
        string typeName = SprListEntry.GetTypeNameById(typeId);
        this.cmbSprListTypeFilter.Items.Add($"{typeId}: {typeName}");
      }
      // 無類型的條目
      if (types.Contains(null))
      {
        this.cmbSprListTypeFilter.Items.Add("無類型");
      }

      this.cmbSprListTypeFilter.SelectedIndex = 0;
      this._SprListTypeFilter = null;
      this.cmbSprListTypeFilter.Visible = true;
    }

    private void cmbSprListTypeFilter_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (!this._IsSprListMode || this._SprListFile == null)
        return;

      string selected = this.cmbSprListTypeFilter.SelectedItem?.ToString() ?? "";

      if (selected == "全部類型" || this.cmbSprListTypeFilter.SelectedIndex == 0)
      {
        this._SprListTypeFilter = null;
        this._FilteredSprListEntries = this._SprListFile.Entries;
      }
      else if (selected == "無類型")
      {
        this._SprListTypeFilter = null;
        this._FilteredSprListEntries = this._SprListFile.Entries
          .Where(e => !e.TypeId.HasValue)
          .ToList();
      }
      else
      {
        // 格式 "10: 怪物"
        int colonIdx = selected.IndexOf(':');
        if (colonIdx > 0 && int.TryParse(selected.Substring(0, colonIdx), out int filterType))
        {
          this._SprListTypeFilter = filterType;
          this._FilteredSprListEntries = this._SprListFile.Entries
            .Where(e => e.TypeId == filterType)
            .ToList();
        }
      }

      // 更新列表
      this.lvIndexInfo.VirtualListSize = this._FilteredSprListEntries.Count;
      this.lvIndexInfo.Invalidate();
      this.tssShowInListView.Text = $"Shown:{this._FilteredSprListEntries.Count}";
    }

    /// <summary>
    /// 取得 sprite prefix (如 "3225-") 對應的 list.spr 類型
    /// </summary>
    private int? GetSpriteTypeForPrefix(string prefix)
    {
      if (this._SpriteIdToEntry == null)
        return null;

      // prefix 格式為 "3225-"，取出數字部分
      string numPart = prefix.TrimEnd('-');
      if (int.TryParse(numPart, out int spriteId))
      {
        if (this._SpriteIdToEntry.TryGetValue(spriteId, out var entry))
        {
          return entry.TypeId;
        }
      }
      return null;
    }

    /// <summary>
    /// 檢查 sprite group 是否符合當前類型過濾
    /// </summary>
    private bool MatchesTypeFilter(SpriteGroup group)
    {
      if (this.cmbSpriteTypeFilter == null || this.cmbSpriteTypeFilter.SelectedIndex <= 0)
        return true; // "全部"

      string selected = this.cmbSpriteTypeFilter.SelectedItem?.ToString() ?? "";
      int? groupType = GetSpriteTypeForPrefix(group.Prefix);

      if (selected == "未連結")
      {
        // 未連結 = SpriteIdToEntry 裡找不到
        string numPart = group.Prefix.TrimEnd('-');
        if (int.TryParse(numPart, out int spriteId))
        {
          return this._SpriteIdToEntry == null || !this._SpriteIdToEntry.ContainsKey(spriteId);
        }
        return true;
      }

      // 格式 "10: 怪物"
      int colonIdx = selected.IndexOf(':');
      if (colonIdx > 0 && int.TryParse(selected.Substring(0, colonIdx), out int filterType))
      {
        return groupType == filterType;
      }

      return true;
    }

    private void chkSpriteMode_CheckedChanged(object sender, EventArgs e)
    {
      if (string.IsNullOrEmpty(this._SelectedFolder))
        return;

      this._IsSpriteMode = this.chkSpriteMode.Checked;

      if (this._IsSpriteMode)
      {
        // 切換到 Sprite 模式：更新下拉選單顯示所有 Sprite*.idx 檔案
        string[] spriteFiles = Directory.GetFiles(this._SelectedFolder, "Sprite*.idx", SearchOption.TopDirectoryOnly);
        Array.Sort(spriteFiles, StringComparer.OrdinalIgnoreCase);

        this.cmbIdxFiles.Items.Clear();
        this.cmbIdxFiles.Items.Add("[Sprite 模式: " + spriteFiles.Length + " 個檔案]");
        foreach (string file in spriteFiles)
        {
          this.cmbIdxFiles.Items.Add("  " + Path.GetFileName(file));
        }
        this.cmbIdxFiles.SelectedIndex = 0;
        this.cmbIdxFiles.Enabled = false;

        // 語言過濾設為全部
        this.cmbLangFilter.SelectedIndex = 0;

        // 創建 TabControl
        SetupSpriteModeTab();

        this.LoadSpriteMode();

        // 顯示 list.spr 分類控件
        this.btnLoadListSpr.Visible = true;
        this.cmbSpriteTypeFilter.Visible = true;
      }
      else
      {
        // 切換回一般模式
        RemoveSpriteModeTab();

        // 隱藏 list.spr 分類控件
        this.btnLoadListSpr.Visible = false;
        this.cmbSpriteTypeFilter.Visible = false;
        this._SpriteModeListSpr = null;
        this._SpriteIdToEntry = null;

        this._SpritePackages = null;
        this.cmbIdxFiles.Enabled = true;

        // 重新掃描所有 idx 檔案
        string[] idxFiles = Directory.GetFiles(this._SelectedFolder, "*.idx", SearchOption.TopDirectoryOnly);
        this.cmbIdxFiles.Items.Clear();
        foreach (string file in idxFiles)
        {
          this.cmbIdxFiles.Items.Add(Path.GetFileName(file));
        }

        // 選擇上次的檔案或預設
        if (this.cmbIdxFiles.Items.Count > 0)
        {
          string lastIdxFile = Settings.Default.LastIdxFile;
          int selectIndex = -1;
          if (!string.IsNullOrEmpty(lastIdxFile))
          {
            selectIndex = this.cmbIdxFiles.Items.IndexOf(lastIdxFile);
          }
          if (selectIndex < 0)
          {
            selectIndex = this.cmbIdxFiles.Items.IndexOf("Text.idx");
          }
          if (selectIndex < 0)
          {
            selectIndex = 0;
          }
          this.cmbIdxFiles.SelectedIndex = selectIndex;
        }
      }
    }

    private void chkGalleryMode_CheckedChanged(object sender, EventArgs e)
    {
      this._IsGalleryMode = this.chkGalleryMode.Checked;

      if (this._IsGalleryMode)
      {
        EnterGalleryMode();
      }
      else
      {
        ExitGalleryMode();
      }
    }

    private void EnterGalleryMode()
    {
      // 建立 GalleryViewer 如果還不存在
      if (this.GalleryViewer == null)
      {
        this.GalleryViewer = new ucGalleryViewer();
        this.GalleryViewer.Dock = DockStyle.Fill;
        this.GalleryViewer.ThumbnailLoader = LoadThumbnailForGallery;
        this.GalleryViewer.ItemSelected += GalleryViewer_ItemSelected;
        this.GalleryViewer.ItemDoubleClicked += GalleryViewer_ItemDoubleClicked;
      }

      // 根據目前模式準備圖片項目列表
      this._GalleryItems = new List<GalleryItem>();

      if (this._IsSpriteMode && this._IndexRecords != null)
      {
        // Sprite 模式：SPR Tab 顯示 .spr 檔案（每個 SpriteId 只顯示第一個）
        var seenPrefixes = new HashSet<string>();
        for (int i = 0; i < this._IndexRecords.Length; i++)
        {
          var record = this._IndexRecords[i];
          string ext = Path.GetExtension(record.FileName).ToLower();
          if (ext == ".spr")
          {
            // 取得 prefix (如 "1234-" 從 "1234-5.spr")
            string nameWithoutExt = Path.GetFileNameWithoutExtension(record.FileName);
            int dashIndex = nameWithoutExt.LastIndexOf('-');
            string prefix = dashIndex > 0 ? nameWithoutExt.Substring(0, dashIndex + 1) : nameWithoutExt;

            // 只加入每個 prefix 的第一個檔案
            if (!seenPrefixes.Contains(prefix))
            {
              seenPrefixes.Add(prefix);
              this._GalleryItems.Add(new GalleryItem
              {
                Index = i,
                FileName = prefix.TrimEnd('-'),  // 顯示 SpriteId
                FileSize = record.FileSize,
                Offset = record.Offset,
                SourcePak = record.SourcePak
              });
            }
          }
        }

        // 建立「其他檔案」Tab 的 GalleryViewer
        if (this.GalleryViewerOther == null)
        {
          this.GalleryViewerOther = new ucGalleryViewer();
          this.GalleryViewerOther.Dock = DockStyle.Fill;
          this.GalleryViewerOther.ThumbnailLoader = LoadThumbnailForGalleryOther;
          this.GalleryViewerOther.ItemSelected += GalleryViewerOther_ItemSelected;
          this.GalleryViewerOther.ItemDoubleClicked += GalleryViewerOther_ItemDoubleClicked;
        }

        // 其他檔案：顯示 .img, .png, .tbt 等圖片
        this._GalleryItemsOther = new List<GalleryItem>();
        if (this._OtherFilesIndexes != null)
        {
          foreach (int idx in this._OtherFilesIndexes)
          {
            var record = this._IndexRecords[idx];
            string ext = Path.GetExtension(record.FileName).ToLower();
            if (ext == ".img" || ext == ".png" || ext == ".tbt" || ext == ".til")
            {
              this._GalleryItemsOther.Add(new GalleryItem
              {
                Index = idx,
                FileName = record.FileName,
                FileSize = record.FileSize,
                Offset = record.Offset,
                SourcePak = record.SourcePak
              });
            }
          }
        }

        // 替換兩個 Tab 的內容
        this.tabSprites.Controls.Clear();
        this.tabSprites.Controls.Add(this.GalleryViewer);
        this.tabOtherFiles.Controls.Clear();
        this.tabOtherFiles.Controls.Add(this.GalleryViewerOther);

        this.GalleryViewer.Visible = true;
        this.GalleryViewer.SetItems(this._GalleryItems);
        this.GalleryViewerOther.Visible = true;
        this.GalleryViewerOther.SetItems(this._GalleryItemsOther);

        this.tssMessage.Text = $"相簿模式: SPR {this._GalleryItems.Count} 個, 其他 {this._GalleryItemsOther.Count} 個";
      }
      else if (this._FilteredIndexes != null && this._IndexRecords != null)
      {
        // 一般模式：從篩選後的列表中取得圖片檔案
        foreach (int idx in this._FilteredIndexes)
        {
          var record = this._IndexRecords[idx];
          string ext = Path.GetExtension(record.FileName).ToLower();
          if (ext == ".spr" || ext == ".img" || ext == ".png" || ext == ".tbt" || ext == ".til")
          {
            this._GalleryItems.Add(new GalleryItem
            {
              Index = idx,
              FileName = record.FileName,
              FileSize = record.FileSize,
              Offset = record.Offset,
              SourcePak = record.SourcePak
            });
          }
        }

        // 一般模式下替換 splitContainer2.Panel2 內容
        this.lvIndexInfo.Visible = false;
        if (!this.splitContainer2.Panel2.Controls.Contains(this.GalleryViewer))
        {
          this.splitContainer2.Panel2.Controls.Clear();
          this.splitContainer2.Panel2.Controls.Add(this.GalleryViewer);
        }

        this.GalleryViewer.Visible = true;
        this.GalleryViewer.SetItems(this._GalleryItems);
        this.tssMessage.Text = $"相簿模式: 顯示 {this._GalleryItems.Count} 個圖片檔案";
      }
    }

    private void ExitGalleryMode()
    {
      if (this.GalleryViewer != null)
      {
        this.GalleryViewer.Clear();
        this.GalleryViewer.Visible = false;
      }

      if (this.GalleryViewerOther != null)
      {
        this.GalleryViewerOther.Clear();
        this.GalleryViewerOther.Visible = false;
      }

      // 還原 ListView
      if (this._IsSpriteMode && this.tabSpriteMode != null)
      {
        // 還原 SPR Tab
        if (!this.tabSprites.Controls.Contains(this.lvIndexInfo))
        {
          this.tabSprites.Controls.Clear();
          this.tabSprites.Controls.Add(this.lvIndexInfo);
        }
        // 還原其他檔案 Tab
        if (this.lvOtherFiles != null && !this.tabOtherFiles.Controls.Contains(this.lvOtherFiles))
        {
          this.tabOtherFiles.Controls.Clear();
          this.tabOtherFiles.Controls.Add(this.lvOtherFiles);
        }
      }
      else
      {
        if (!this.splitContainer2.Panel2.Controls.Contains(this.lvIndexInfo))
        {
          this.splitContainer2.Panel2.Controls.Clear();
          this.splitContainer2.Panel2.Controls.Add(this.lvIndexInfo);
        }
      }

      this.lvIndexInfo.Visible = true;
      this.lvIndexInfo.Dock = DockStyle.Fill;

      this.tssMessage.Text = "";
    }

    private Image LoadThumbnailForGallery(int itemIndex)
    {
      if (this._GalleryItems == null || itemIndex < 0 || itemIndex >= this._GalleryItems.Count)
        return null;

      var item = this._GalleryItems[itemIndex];
      if (this._IndexRecords == null || item.Index < 0 || item.Index >= this._IndexRecords.Length)
        return null;

      var record = this._IndexRecords[item.Index];

      // 決定 PAK 檔案路徑
      string pakFile;
      if (this._IsSpriteMode && !string.IsNullOrEmpty(record.SourcePak))
      {
        pakFile = record.SourcePak;
      }
      else
      {
        if (string.IsNullOrEmpty(this._PackFileName))
          return null;
        pakFile = this._PackFileName.Replace(".idx", ".pak");
      }

      if (!File.Exists(pakFile))
        return null;

      try
      {
        byte[] data;
        using (var fs = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
          data = new byte[record.FileSize];
          fs.Seek(record.Offset, SeekOrigin.Begin);
          fs.Read(data, 0, record.FileSize);
        }

        // 解碼（如果需要）
        if (this._IsPackFileProtected)
        {
          data = L1PakTools.Decode(data, 0);
        }

        // 根據副檔名處理
        string ext = Path.GetExtension(record.FileName).ToLower();
        Image result = null;

        switch (ext)
        {
          case ".spr":
            // SPR 檔案：取得第一個 frame
            var frames = L1Spr.Load(data);
            if (frames != null && frames.Length > 0)
            {
              result = frames[0].image;
            }
            break;

          case ".img":
            result = ImageConvert.Load_IMG(data);
            break;

          case ".png":
          case ".bmp":
            using (var ms = new MemoryStream(data))
            {
              result = Image.FromStream(ms);
            }
            break;

          case ".tbt":
            result = ImageConvert.Load_TBT(data);
            break;

          case ".til":
            result = ImageConvert.Load_TIL(data);
            break;
        }

        return result;
      }
      catch
      {
        return null;
      }
    }

    private void GalleryViewer_ItemSelected(object sender, GalleryItemSelectedEventArgs e)
    {
      if (e.Item == null)
        return;

      // 載入並顯示選取的檔案
      LoadAndDisplayFile(e.Item.Index);
    }

    private void GalleryViewer_ItemDoubleClicked(object sender, GalleryItemSelectedEventArgs e)
    {
      if (e.Item == null)
        return;

      // 雙擊時也是載入檔案
      LoadAndDisplayFile(e.Item.Index);
    }

    private Image LoadThumbnailForGalleryOther(int itemIndex)
    {
      if (this._GalleryItemsOther == null || itemIndex < 0 || itemIndex >= this._GalleryItemsOther.Count)
        return null;

      var item = this._GalleryItemsOther[itemIndex];
      if (this._IndexRecords == null || item.Index < 0 || item.Index >= this._IndexRecords.Length)
        return null;

      var record = this._IndexRecords[item.Index];

      // 決定 PAK 檔案路徑
      string pakFile;
      if (!string.IsNullOrEmpty(record.SourcePak))
      {
        pakFile = record.SourcePak;
      }
      else
      {
        if (string.IsNullOrEmpty(this._PackFileName))
          return null;
        pakFile = this._PackFileName.Replace(".idx", ".pak");
      }

      if (!File.Exists(pakFile))
        return null;

      try
      {
        byte[] data;
        using (var fs = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
          data = new byte[record.FileSize];
          fs.Seek(record.Offset, SeekOrigin.Begin);
          fs.Read(data, 0, record.FileSize);
        }

        if (this._IsPackFileProtected)
        {
          data = L1PakTools.Decode(data, 0);
        }

        string ext = Path.GetExtension(record.FileName).ToLower();
        Image result = null;

        switch (ext)
        {
          case ".img":
            result = ImageConvert.Load_IMG(data);
            break;
          case ".png":
          case ".bmp":
            using (var ms = new MemoryStream(data))
            {
              result = Image.FromStream(ms);
            }
            break;
          case ".tbt":
            result = ImageConvert.Load_TBT(data);
            break;
          case ".til":
            result = ImageConvert.Load_TIL(data);
            break;
        }

        return result;
      }
      catch
      {
        return null;
      }
    }

    private void GalleryViewerOther_ItemSelected(object sender, GalleryItemSelectedEventArgs e)
    {
      if (e.Item == null)
        return;

      LoadAndDisplayFile(e.Item.Index);
    }

    private void GalleryViewerOther_ItemDoubleClicked(object sender, GalleryItemSelectedEventArgs e)
    {
      if (e.Item == null)
        return;

      LoadAndDisplayFile(e.Item.Index);
    }

    private void SetupSpriteModeTab()
    {
      if (this.tabSpriteMode != null)
        return;

      // 創建 TabControl
      this.tabSpriteMode = new TabControl();
      this.tabSpriteMode.Dock = DockStyle.Fill;

      // 創建 SPR 頁籤
      this.tabSprites = new TabPage("SPR 檔案");
      this.tabSprites.Controls.Add(this.lvIndexInfo);
      this.lvIndexInfo.Dock = DockStyle.Fill;

      // 設置 master-detail 模式的欄位
      this.lvIndexInfo.Columns.Clear();
      this.lvIndexInfo.Columns.Add("SpriteId", 80, HorizontalAlignment.Left);
      this.lvIndexInfo.Columns.Add("資訊", 180, HorizontalAlignment.Left);
      this.lvIndexInfo.Columns.Add("大小(KB)", 80, HorizontalAlignment.Right);

      // 創建其他檔案頁籤
      this.tabOtherFiles = new TabPage("其他檔案");

      // 創建第二個 ListView
      this.lvOtherFiles = new ListView();
      this.lvOtherFiles.Dock = DockStyle.Fill;
      this.lvOtherFiles.View = View.Details;
      this.lvOtherFiles.FullRowSelect = true;
      this.lvOtherFiles.GridLines = true;
      this.lvOtherFiles.VirtualMode = true;
      this.lvOtherFiles.Columns.Add("No.", 70, HorizontalAlignment.Right);
      this.lvOtherFiles.Columns.Add("FileName", 150, HorizontalAlignment.Left);
      this.lvOtherFiles.Columns.Add("Size(KB)", 80, HorizontalAlignment.Right);
      this.lvOtherFiles.Columns.Add("Position", 70, HorizontalAlignment.Right);
      this.lvOtherFiles.RetrieveVirtualItem += lvOtherFiles_RetrieveVirtualItem;
      this.lvOtherFiles.SelectedIndexChanged += lvOtherFiles_SelectedIndexChanged;
      this.lvOtherFiles.ColumnClick += lvOtherFiles_ColumnClick;

      this.tabOtherFiles.Controls.Add(this.lvOtherFiles);

      // 加入頁籤
      this.tabSpriteMode.TabPages.Add(this.tabSprites);
      this.tabSpriteMode.TabPages.Add(this.tabOtherFiles);
      this.tabSpriteMode.SelectedIndexChanged += (s, ev) => this._LastSelectedCount = -1; // 切換 Tab 時重算

      // 替換 splitContainer2.Panel2 的內容
      this.splitContainer2.Panel2.Controls.Clear();
      this.splitContainer2.Panel2.Controls.Add(this.tabSpriteMode);

      // 創建右側 Detail Viewer
      if (this.SprDetailViewer == null)
      {
        this.SprDetailViewer = new ucSprDetailViewer();
        this.SprDetailViewer.Dock = DockStyle.Fill;
        this.SprDetailViewer.SetSpriteDataProvider(this.GetSpriteDataByKey);
        this.splitContainer1.Panel2.Controls.Add(this.SprDetailViewer);
      }
    }

    private void RemoveSpriteModeTab()
    {
      if (this.tabSpriteMode == null)
        return;

      // 把 lvIndexInfo 移回 splitContainer2.Panel2
      this.tabSprites.Controls.Remove(this.lvIndexInfo);
      this.splitContainer2.Panel2.Controls.Clear();
      this.splitContainer2.Panel2.Controls.Add(this.lvIndexInfo);
      this.lvIndexInfo.Dock = DockStyle.Fill;

      // 還原原來的欄位
      this.lvIndexInfo.Columns.Clear();
      this.lvIndexInfo.Columns.Add("No.", 70, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("FileName", 150, HorizontalAlignment.Left);
      this.lvIndexInfo.Columns.Add("Size(KB)", 80, HorizontalAlignment.Right);
      this.lvIndexInfo.Columns.Add("Position", 70, HorizontalAlignment.Right);

      // 隱藏 SprDetailViewer
      if (this.SprDetailViewer != null)
      {
        this.SprDetailViewer.Visible = false;
        this.SprDetailViewer.Clear();
      }

      // 清理
      this.lvOtherFiles.Dispose();
      this.lvOtherFiles = null;
      this.tabSprites.Dispose();
      this.tabSprites = null;
      this.tabOtherFiles.Dispose();
      this.tabOtherFiles = null;
      this.tabSpriteMode.Dispose();
      this.tabSpriteMode = null;
      this._OtherFilesIndexes = null;
    }

    private void lvOtherFiles_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
      // 建立空白項目的輔助方法
      ListViewItem createEmptyItem()
      {
        var listItem = new ListViewItem("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        return listItem;
      }

      if (this._OtherFilesIndexes == null || e.ItemIndex < 0 || e.ItemIndex >= this._OtherFilesIndexes.Count)
      {
        e.Item = createEmptyItem();
        return;
      }

      try
      {
        int realIndex = this._OtherFilesIndexes[e.ItemIndex];
        if (this._IndexRecords == null || realIndex < 0 || realIndex >= this._IndexRecords.Length)
        {
          e.Item = createEmptyItem();
          return;
        }

        var record = this._IndexRecords[realIndex];
        string sizeText = string.Format("{0:F1}", record.FileSize / 1024.0);
        var item = new ListViewItem(string.Format("{0, 5}", realIndex + 1));
        item.SubItems.Add(record.FileName);
        item.SubItems.Add(sizeText);
        item.SubItems.Add(record.Offset.ToString("X8"));
        e.Item = item;
      }
      catch
      {
        e.Item = createEmptyItem();
      }
    }

    private void lvOtherFiles_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lvOtherFiles.SelectedIndices.Count != 1)
        return;

      int selectedIndex = this.lvOtherFiles.SelectedIndices[0];
      if (this._OtherFilesIndexes == null || selectedIndex >= this._OtherFilesIndexes.Count)
        return;

      int realIndex = this._OtherFilesIndexes[selectedIndex];
      LoadAndDisplayFile(realIndex);
    }

    private void lvOtherFiles_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (this._OtherFilesIndexes == null)
        return;

      // 簡單排序
      bool ascending = true;
      if (this.lvOtherFiles.Tag is int lastCol && Math.Abs(lastCol) == e.Column)
      {
        ascending = lastCol < 0;
      }
      this.lvOtherFiles.Tag = ascending ? e.Column : -e.Column;

      switch (e.Column)
      {
        case 1: // FileName
          if (ascending)
            this._OtherFilesIndexes.Sort((a, b) => string.Compare(this._IndexRecords[a].FileName, this._IndexRecords[b].FileName, StringComparison.OrdinalIgnoreCase));
          else
            this._OtherFilesIndexes.Sort((a, b) => string.Compare(this._IndexRecords[b].FileName, this._IndexRecords[a].FileName, StringComparison.OrdinalIgnoreCase));
          break;
        case 2: // Size
          if (ascending)
            this._OtherFilesIndexes.Sort((a, b) => this._IndexRecords[a].FileSize.CompareTo(this._IndexRecords[b].FileSize));
          else
            this._OtherFilesIndexes.Sort((a, b) => this._IndexRecords[b].FileSize.CompareTo(this._IndexRecords[a].FileSize));
          break;
        default: // No. or Position
          if (ascending)
            this._OtherFilesIndexes.Sort();
          else
            this._OtherFilesIndexes.Sort((a, b) => b.CompareTo(a));
          break;
      }

      this.lvOtherFiles.Invalidate();
    }

    private void LoadAndDisplayFile(int realIndex)
    {
      this.tssMessage.Text = "";
      this.ImageViewer.Image = (Image) null;

      L1PakTools.IndexRecord record = this._IndexRecords[realIndex];

      // 決定要讀取的 PAK 檔案
      string pakFile;
      if (this._IsSpriteMode && !string.IsNullOrEmpty(record.SourcePak))
      {
        pakFile = record.SourcePak;
      }
      else
      {
        if (string.IsNullOrEmpty(this._PackFileName))
          return;
        pakFile = this._PackFileName.Replace(".idx", ".pak");
      }

      if (!File.Exists(pakFile))
      {
        this.tssMessage.Text = "PAK file not found: " + pakFile;
        return;
      }

      try
      {
        FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        object obj = this.LoadPakData_(fs, record);
        fs.Close();
        this.ViewerSwitch();
        if (this._InviewData == frmMain.InviewDataType.Text || this._InviewData == frmMain.InviewDataType.Empty)
        {
          this.TextViewer.TextChanged -= new EventHandler(this.TextViewer_TextChanged);
          if (obj is string)
            this.TextViewer.Text = (string) obj;
          else if (obj is byte[])
            this.TextViewer.Text = Encoding.GetEncoding("big5").GetString((byte[]) obj);
          this.TextViewer.Tag = (object) (realIndex + 1).ToString();
          this.TextViewer.ReadOnly = this._IsSpriteMode;
          if (this._InviewData == frmMain.InviewDataType.Empty)
          {
            this.TextViewer.Visible = true;
            this.ImageViewer.Visible = false;
            this.SprViewer.Visible = false;
          }
        }
        else if (this._InviewData == frmMain.InviewDataType.IMG || this._InviewData == frmMain.InviewDataType.BMP || (this._InviewData == frmMain.InviewDataType.TBT || this._InviewData == frmMain.InviewDataType.TIL))
        {
          this.ImageViewer.Image = (Image) obj;
        }
        else if (this._InviewData == frmMain.InviewDataType.SPR)
        {
          this.SprViewer.SprFrames = (L1Spr.Frame[]) obj;
          this.SprViewer.Start();
        }
        this.tssMessage.Text = string.Format("{0}  [{1}KB]", record.FileName, string.Format("{0:F1}", record.FileSize / 1024.0));
      }
      catch (Exception ex)
      {
        this._InviewData = frmMain.InviewDataType.Empty;
        this.tssMessage.Text = "*Error *: Can't open this file! " + ex.Message;
      }
    }

    private void LoadSpriteMode()
    {
      this.Cursor = Cursors.WaitCursor;
      this.lvIndexInfo.VirtualListSize = 0;

      var sw = System.Diagnostics.Stopwatch.StartNew();

      // 找到所有 Sprite*.idx 檔案
      string[] spriteFiles = Directory.GetFiles(this._SelectedFolder, "Sprite*.idx", SearchOption.TopDirectoryOnly);
      if (spriteFiles.Length == 0)
      {
        MessageBox.Show("找不到 Sprite*.idx 檔案。", "Sprite 模式", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        this.chkSpriteMode.Checked = false;
        this.Cursor = Cursors.Default;
        return;
      }

      // 排序檔案名稱
      Array.Sort(spriteFiles, StringComparer.OrdinalIgnoreCase);

      this._SpritePackages = new Dictionary<string, (L1PakTools.IndexRecord[] records, bool isProtected)>();
      var allRecords = new List<L1PakTools.IndexRecord>();
      int totalFiles = 0;

      foreach (string idxFile in spriteFiles)
      {
        string pakFile = idxFile.Replace(".idx", ".pak");
        if (!File.Exists(pakFile))
          continue;

        byte[] indexData = this.LoadIndexData(idxFile);
        if (indexData == null)
          continue;

        var records = this.CreatIndexRecords(indexData);
        if (records == null)
          continue;

        // 為每個記錄設定來源 PAK
        var recordsWithSource = new L1PakTools.IndexRecord[records.Length];
        for (int i = 0; i < records.Length; i++)
        {
          recordsWithSource[i] = new L1PakTools.IndexRecord(
            records[i].FileName,
            records[i].FileSize,
            records[i].Offset,
            pakFile
          );
          allRecords.Add(recordsWithSource[i]);
        }

        this._SpritePackages[pakFile] = (recordsWithSource, this._IsPackFileProtected);
        totalFiles++;
      }

      long loadMs = sw.ElapsedMilliseconds;

      sw.Restart();
      this._IndexRecords = allRecords.ToArray();
      this._PackFileName = null; // Sprite 模式下沒有單一 PAK 檔案
      long createMs = sw.ElapsedMilliseconds;

      sw.Restart();
      if (this._IndexRecords.Length == 0)
      {
        MessageBox.Show("所有 Sprite*.idx 檔案都是空的或無法讀取。", "Sprite 模式", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        this.chkSpriteMode.Checked = false;
      }
      else
      {
        this.ShowRecords(this._IndexRecords);
        long showMs = sw.ElapsedMilliseconds;
        this.mnuFiller.Enabled = false;  // Sprite 模式下停用填充功能
        this.mnuRebuild.Enabled = false; // Sprite 模式下停用重建功能
        this.tssMessage.Text = string.Format("Sprite 模式: {0} 個檔案, {1} 筆記錄 | Load:{2}ms | Parse:{3}ms | Show:{4}ms",
          totalFiles, this._IndexRecords.Length, loadMs, createMs, showMs);
      }

      this.Cursor = Cursors.Default;
    }

    private void mnuFiller_Text_Language(object sender, EventArgs e)
    {
      this._TextLanguage = (this.mnuFiller_Text_C.Checked ? "-c" : "") + (this.mnuFiller_Text_H.Checked ? "-h" : "") + (this.mnuFiller_Text_J.Checked ? "-j" : "") + (this.mnuFiller_Text_K.Checked ? "-k" : "");
      Settings.Default.DefaultLang = this._TextLanguage;
      Settings.Default.Save();
      if (this._IndexRecords == null)
        return;
      this.ShowRecords(this._IndexRecords);
    }

    private void mnuFiller_FileStyle(object sender, EventArgs e)
    {
      if (this._IndexRecords == null)
        return;
      this.ShowRecords(this._IndexRecords);
    }

    /// <summary>
    /// 根據 PAK 中的檔案動態建立副檔名篩選器選單
    /// </summary>
    private void BuildDynamicExtensionFilter(L1PakTools.IndexRecord[] records)
    {
      // 收集所有副檔名
      var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var record in records)
      {
        string ext = Path.GetExtension(record.FileName).ToLower();
        if (!string.IsNullOrEmpty(ext))
          extensions.Add(ext);
      }

      // 清除舊的動態選項
      foreach (var item in this._DynamicExtFilters)
      {
        this.mnuFiller.DropDownItems.Remove(item);
      }
      this._DynamicExtFilters.Clear();

      // 移除「全部」選項（如果存在）
      if (this.mnuFiller_All != null)
      {
        this.mnuFiller.DropDownItems.Remove(this.mnuFiller_All);
      }

      // 建立「全部」選項
      this.mnuFiller_All = new ToolStripMenuItem();
      this.mnuFiller_All.Text = "全部";
      this.mnuFiller_All.Checked = true;
      this.mnuFiller_All.CheckOnClick = true;
      this.mnuFiller_All.CheckedChanged += (s, e) =>
      {
        if (this.mnuFiller_All.Checked)
        {
          // 取消所有其他選項
          foreach (var item in this._DynamicExtFilters)
          {
            if (item != null) item.Checked = false;
          }
        }
        this.ShowRecords(this._IndexRecords);
      };

      // 插入到選單最前面
      this.mnuFiller.DropDownItems.Insert(0, this.mnuFiller_All);

      // 建立分隔線
      var separator = new ToolStripSeparator();
      this.mnuFiller.DropDownItems.Insert(1, separator);
      this._DynamicExtFilters.Add(null); // placeholder for separator

      // 建立副檔名選項（按字母排序）
      int insertIndex = 2;
      foreach (string ext in extensions.OrderBy(e => e))
      {
        var menuItem = new ToolStripMenuItem();
        menuItem.Text = $"*{ext}";
        menuItem.Checked = false;
        menuItem.CheckOnClick = true;
        menuItem.Tag = ext;
        menuItem.CheckedChanged += (s, e) =>
        {
          // 如果有任何副檔名被選中，取消「全部」
          bool anyChecked = this._DynamicExtFilters.Any(m => m != null && m.Checked);
          if (anyChecked && this.mnuFiller_All.Checked)
          {
            this.mnuFiller_All.Checked = false;
          }
          else if (!anyChecked && !this.mnuFiller_All.Checked)
          {
            this.mnuFiller_All.Checked = true;
          }
          this.ShowRecords(this._IndexRecords);
        };
        this.mnuFiller.DropDownItems.Insert(insertIndex++, menuItem);
        this._DynamicExtFilters.Add(menuItem);
      }

      // 加入分隔線（在語言選項之前）
      var langSeparator = new ToolStripSeparator();
      this.mnuFiller.DropDownItems.Insert(insertIndex, langSeparator);
      this._DynamicExtFilters.Add(null); // placeholder
    }

    private byte[] LoadIndexData(string IndexFile)
    {
      byte[] numArray = File.ReadAllBytes(IndexFile);

      // 重置狀態
      this._IsPackFileProtected = false;
      this._IsDESProtected = false;
      this._IsExtBFormat = false;
      this._ExtBRawData = null;
      this.tssLocker.Visible = false;

      // 優先檢測 _EXTB$ 格式
      if (PakReader.IsExtBFormat(numArray))
      {
        this._IsExtBFormat = true;
        this._ExtBRawData = numArray;
        return numArray; // 返回原始資料，由 CreatIndexRecords 處理
      }

      int num = (numArray.Length - 4) / 28;
      if (numArray.Length < 32 || (numArray.Length - 4) % 28 != 0)
        return (byte[]) null;
      if ((long) BitConverter.ToUInt32(numArray, 0) != (long) num)
        return (byte[]) null;

      // 讀取第一條記錄的 size（未解密）
      int rawFirstSize = BitConverter.ToInt32(numArray, 4 + 24);

      // 檢查檔名是否有效 AND size 是否合理（非負數且不過大）
      bool filenameValid = Regex.IsMatch(Encoding.Default.GetString(numArray, 8, 20), "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase);
      bool sizeValid = rawFirstSize >= 0 && rawFirstSize < 100000000; // 小於 100MB

      if (!filenameValid || !sizeValid)
      {
        // 嘗試 L1 解密
        var firstRecord = L1PakTools.Decode_Index_FirstRecord(numArray);
        bool l1FilenameValid = Regex.IsMatch(firstRecord.FileName, "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase);
        bool l1SizeValid = firstRecord.FileSize >= 0 && firstRecord.FileSize < 100000000;

        if (l1FilenameValid && l1SizeValid)
        {
          // L1 解密成功
          this._IsPackFileProtected = true;
          this.tssLocker.Visible = true;
          this.tssProgressName.Text = "解碼中... ";
          numArray = L1PakTools.Decode(numArray, 4);
          this.tssProgressName.Text = "";
          return numArray;
        }

        // L1 失敗，嘗試 DES 解密
        this.tssProgressName.Text = "DES 解碼中... ";
        byte[] desDecrypted = DecryptIndexDES(numArray);
        this.tssProgressName.Text = "";
        if (desDecrypted != null)
        {
          // 檢查 DES 解密後的第一條記錄
          var desFirstRecord = new L1PakTools.IndexRecord(desDecrypted, 0);
          bool desFilenameValid = Regex.IsMatch(desFirstRecord.FileName, "^([a-zA-Z0-9_\\-\\.']+)", RegexOptions.IgnoreCase);
          bool desSizeValid = desFirstRecord.FileSize >= 0 && desFirstRecord.FileSize < 100000000;

          if (desFilenameValid && desSizeValid)
          {
            this._IsDESProtected = true;
            this._IsPackFileProtected = true;
            this.tssLocker.Visible = true;
            return desDecrypted;
          }
        }
        return (byte[]) null;
      }

      return numArray;
    }

    /// <summary>
    /// DES ECB 解密 idx 資料
    /// 密鑰: ~!@#%^$< (0x7e 0x21 0x40 0x23 0x25 0x5e 0x24 0x3c)
    /// </summary>
    private byte[] DecryptIndexDES(byte[] idxData)
    {
      try
      {
        byte[] key = new byte[] { 0x7e, 0x21, 0x40, 0x23, 0x25, 0x5e, 0x24, 0x3c }; // ~!@#%^$<
        byte[] entriesData = new byte[idxData.Length - 4];
        Array.Copy(idxData, 4, entriesData, 0, entriesData.Length);

        using (var des = System.Security.Cryptography.DES.Create())
        {
          des.Key = key;
          des.Mode = System.Security.Cryptography.CipherMode.ECB;
          des.Padding = System.Security.Cryptography.PaddingMode.None;

          using (var decryptor = des.CreateDecryptor())
          {
            int blockCount = entriesData.Length / 8;
            for (int i = 0; i < blockCount; i++)
            {
              int offset = i * 8;
              byte[] block = new byte[8];
              Array.Copy(entriesData, offset, block, 0, 8);
              byte[] decrypted = decryptor.TransformFinalBlock(block, 0, 8);
              Array.Copy(decrypted, 0, entriesData, offset, 8);
            }
          }
        }
        return entriesData;
      }
      catch
      {
        return null;
      }
    }

    private L1PakTools.IndexRecord[] CreatIndexRecords(byte[] IndexData)
    {
      if (IndexData == null)
        return (L1PakTools.IndexRecord[]) null;

      // _EXTB$ 格式處理
      if (this._IsExtBFormat)
      {
        return ParseExtBRecords(IndexData);
      }

      // DES 解密後資料從 0 開始；L1 解密後也從 0 開始；未加密從 4 開始
      int num = (this._IsPackFileProtected || this._IsDESProtected) ? 0 : 4;
      int length = (IndexData.Length - num) / 28;
      L1PakTools.IndexRecord[] indexRecordArray = new L1PakTools.IndexRecord[length];

      // 直接解析，不更新進度條（速度快很多）
      for (int index1 = 0; index1 < length; ++index1)
      {
        int index2 = num + index1 * 28;
        indexRecordArray[index1] = new L1PakTools.IndexRecord(IndexData, index2);
      }
      return indexRecordArray;
    }

    /// <summary>
    /// 解析 _EXTB$ 格式的索引記錄
    /// Header: 16 bytes, Entry: 128 bytes each
    /// Entry 結構:
    ///   Offset 0-3:     Unknown (可能是排序用的 key)
    ///   Offset 4-7:     Compression (0=none, 1=zlib, 2=brotli)
    ///   Offset 8-119:   Filename (112 bytes, null-padded)
    ///   Offset 120-123: PAK Offset (真正的檔案位置)
    ///   Offset 124-127: Uncompressed Size
    /// </summary>
    private L1PakTools.IndexRecord[] ParseExtBRecords(byte[] data)
    {
      const int headerSize = 0x10;  // 16 bytes
      const int entrySize = 0x80;   // 128 bytes

      int entryCount = (data.Length - headerSize) / entrySize;
      var records = new List<L1PakTools.IndexRecord>();
      var allOffsets = new HashSet<int>();
      this._ExtBCompressionTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < entryCount; i++)
      {
        int entryOffset = headerSize + i * entrySize;

        int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);  // 真正的 PAK offset
        int compression = BitConverter.ToInt32(data, entryOffset + 4);
        int fileSize = BitConverter.ToInt32(data, entryOffset + 124);   // Uncompressed size

        allOffsets.Add(pakOffset);

        // 讀取檔名 (從 offset+8 開始，最多 112 bytes)
        int nameStart = entryOffset + 8;
        int nameEnd = nameStart;
        while (nameEnd < entryOffset + 120 && data[nameEnd] != 0 &&
               data[nameEnd] >= 32 && data[nameEnd] <= 126)
        {
          nameEnd++;
        }

        if (nameEnd > nameStart)
        {
          string fileName = Encoding.ASCII.GetString(data, nameStart, nameEnd - nameStart);
          if (!string.IsNullOrEmpty(fileName) && fileName.Contains("."))
          {
            records.Add(new L1PakTools.IndexRecord(fileName, fileSize, pakOffset));
            this._ExtBCompressionTypes[fileName] = compression;
          }
        }
      }

      // 建立排序的 offset 列表 (用於計算壓縮大小)
      this._ExtBSortedOffsets = allOffsets.ToList();
      this._ExtBSortedOffsets.Sort();

      return records.ToArray();
    }

    private void ShowRecords(L1PakTools.IndexRecord[] Records)
    {
      // 使用 VirtualMode - 只建立過濾後的索引列表
      this._FilteredIndexes = new List<int>(Records.Length);
      this._CheckedIndexes.Clear();

      // 取得副檔名過濾條件（下拉選單優先）
      string extFilter = this.cmbExtFilter != null && this.cmbExtFilter.SelectedIndex > 0
        ? this.cmbExtFilter.SelectedItem.ToString()
        : null;

      // 取得語言過濾條件（下拉選單）
      string langFilter = null;
      if (this.cmbLangFilter != null && this.cmbLangFilter.SelectedIndex > 0)
      {
        string selected = this.cmbLangFilter.SelectedItem.ToString();
        langFilter = selected.Substring(0, 2); // 取得 "-c", "-h", "-j", "-k"
      }

      // 建立選單篩選器的副檔名集合（使用動態生成的選項）
      var menuExtFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (extFilter == null && !this._IsSpriteMode)
      {
        // 如果「全部」沒有勾選，收集所有勾選的副檔名
        if (this.mnuFiller_All?.Checked != true)
        {
          foreach (var item in this._DynamicExtFilters)
          {
            if (item != null && item.Checked && item.Tag is string ext)
            {
              menuExtFilters.Add(ext);
            }
          }
        }
      }

      // 建立選單篩選器的語言集合（僅當下拉選單語言為「全部」時使用）
      var menuLangFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (langFilter == null && !this._IsSpriteMode)
      {
        if (this.mnuFiller_Text_C?.Checked == true) menuLangFilters.Add("-c");
        if (this.mnuFiller_Text_H?.Checked == true) menuLangFilters.Add("-h");
        if (this.mnuFiller_Text_J?.Checked == true) menuLangFilters.Add("-j");
        if (this.mnuFiller_Text_K?.Checked == true) menuLangFilters.Add("-k");
        // 如果都選了或都沒選，視為不篩選
        if (menuLangFilters.Count == 0 || menuLangFilters.Count >= 4) menuLangFilters.Clear();
      }

      for (int ID = 0; ID < Records.Length; ++ID)
      {
        L1PakTools.IndexRecord record = Records[ID];
        string extension = Path.GetExtension(record.FileName).ToLower();

        // 副檔名過濾（下拉選單優先）
        if (extFilter != null && extension != extFilter)
          continue;

        // 選單副檔名過濾（僅當下拉選單為「全部」且有勾選時）
        if (menuExtFilters.Count > 0 && !menuExtFilters.Contains(extension))
          continue;

        // 語言過濾（下拉選單優先）
        if (langFilter != null)
        {
          string withoutExtension = Path.GetFileNameWithoutExtension(record.FileName).ToLower();
          int dashIndex = withoutExtension.LastIndexOf("-");
          if (dashIndex >= 0 && withoutExtension.Length >= 2)
          {
            string fileLang = withoutExtension.Substring(withoutExtension.Length - 2);
            if (fileLang != langFilter)
              continue;
          }
        }

        // 選單語言過濾（僅當下拉選單語言為「全部」時）
        if (menuLangFilters.Count > 0)
        {
          string withoutExtension = Path.GetFileNameWithoutExtension(record.FileName).ToLower();
          int dashIndex = withoutExtension.LastIndexOf("-");
          if (dashIndex >= 0 && withoutExtension.Length >= 2)
          {
            string fileLang = "-" + withoutExtension.Substring(withoutExtension.Length - 1);
            // 檢查是否符合任一勾選的語言
            bool langMatch = menuLangFilters.Any(l => withoutExtension.EndsWith(l, StringComparison.OrdinalIgnoreCase));
            if (!langMatch)
              continue;
          }
        }

        this._FilteredIndexes.Add(ID);
      }

      // Sprite 模式下建立分組
      if (this._IsSpriteMode)
      {
        BuildSpriteGroups();
        BuildSpriteDisplayItems();
        this.lvIndexInfo.VirtualListSize = this._SpriteDisplayItems.Count;
      }
      else
      {
        this._SpriteGroups = null;
        this._SpriteDisplayItems = null;
        this.lvIndexInfo.VirtualListSize = this._FilteredIndexes.Count;
      }

      this.lvIndexInfo.Invalidate();
      this.tssRecordCount.Text = string.Format("全部：{0}", (object) Records.Length);
      this.tssShowInListView.Text = string.Format("顯示：{0}", (object) this._FilteredIndexes.Count);
      this.tssCheckedCount.Text = string.Format("已選：{0}", (object) this._CheckedIndexes.Count);

      // 如果相簿模式開啟，更新相簿內容
      if (this._IsGalleryMode)
      {
        RefreshGalleryMode();
      }
    }

    /// <summary>
    /// 重新整理相簿模式的內容（篩選後）
    /// </summary>
    private void RefreshGalleryMode()
    {
      if (!this._IsGalleryMode || this._IndexRecords == null)
        return;

      this._GalleryItems = new List<GalleryItem>();

      if (this._IsSpriteMode)
      {
        // Sprite 模式：SPR Tab 顯示 .spr 檔案（每個 SpriteId 只顯示第一個）
        var seenPrefixes = new HashSet<string>();
        foreach (int idx in this._FilteredIndexes)
        {
          var record = this._IndexRecords[idx];
          string ext = Path.GetExtension(record.FileName).ToLower();
          if (ext == ".spr")
          {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(record.FileName);
            int dashIndex = nameWithoutExt.LastIndexOf('-');
            string prefix = dashIndex > 0 ? nameWithoutExt.Substring(0, dashIndex + 1) : nameWithoutExt;

            if (!seenPrefixes.Contains(prefix))
            {
              seenPrefixes.Add(prefix);
              this._GalleryItems.Add(new GalleryItem
              {
                Index = idx,
                FileName = prefix.TrimEnd('-'),
                FileSize = record.FileSize,
                Offset = record.Offset,
                SourcePak = record.SourcePak
              });
            }
          }
        }

        // 其他檔案
        this._GalleryItemsOther = new List<GalleryItem>();
        if (this._OtherFilesIndexes != null)
        {
          foreach (int idx in this._OtherFilesIndexes)
          {
            var record = this._IndexRecords[idx];
            string ext = Path.GetExtension(record.FileName).ToLower();
            if (ext == ".img" || ext == ".png" || ext == ".tbt" || ext == ".til")
            {
              this._GalleryItemsOther.Add(new GalleryItem
              {
                Index = idx,
                FileName = record.FileName,
                FileSize = record.FileSize,
                Offset = record.Offset,
                SourcePak = record.SourcePak
              });
            }
          }
        }

        this.GalleryViewer?.SetItems(this._GalleryItems);
        this.GalleryViewerOther?.SetItems(this._GalleryItemsOther);
        this.tssMessage.Text = $"相簿模式: SPR {this._GalleryItems.Count} 個, 其他 {this._GalleryItemsOther?.Count ?? 0} 個";
      }
      else
      {
        // 一般模式
        foreach (int idx in this._FilteredIndexes)
        {
          var record = this._IndexRecords[idx];
          string ext = Path.GetExtension(record.FileName).ToLower();
          if (ext == ".spr" || ext == ".img" || ext == ".png" || ext == ".tbt" || ext == ".til")
          {
            this._GalleryItems.Add(new GalleryItem
            {
              Index = idx,
              FileName = record.FileName,
              FileSize = record.FileSize,
              Offset = record.Offset,
              SourcePak = record.SourcePak
            });
          }
        }

        this.GalleryViewer?.SetItems(this._GalleryItems);
        this.tssMessage.Text = $"相簿模式: 顯示 {this._GalleryItems.Count} 個圖片檔案";
      }
    }

    private void BuildSpriteGroups()
    {
      if (this._ExpandedGroups == null)
        this._ExpandedGroups = new HashSet<string>();

      var groupDict = new Dictionary<string, SpriteGroup>();

      foreach (int idx in this._FilteredIndexes)
      {
        var record = this._IndexRecords[idx];
        string ext = Path.GetExtension(record.FileName).ToLower();

        // 只對 .spr 檔案分組
        if (ext == ".spr")
        {
          string nameWithoutExt = Path.GetFileNameWithoutExtension(record.FileName);
          int lastDash = nameWithoutExt.LastIndexOf('-');
          string prefix;
          if (lastDash > 0)
          {
            prefix = nameWithoutExt.Substring(0, lastDash + 1); // 包含 "-"
          }
          else
          {
            prefix = nameWithoutExt + "-"; // 沒有 dash 的檔案自成一組
          }

          if (!groupDict.TryGetValue(prefix, out SpriteGroup group))
          {
            group = new SpriteGroup(prefix);
            group.IsExpanded = this._ExpandedGroups.Contains(prefix);
            groupDict[prefix] = group;
          }
          group.ItemIndexes.Add(idx);
          group.TotalSize += record.FileSize;
        }
      }

      // 對每個群組內的項目按數字排序
      foreach (var group in groupDict.Values)
      {
        group.ItemIndexes.Sort((a, b) =>
        {
          int numA = SpriteGroup.GetSuffixNumber(this._IndexRecords[a].FileName);
          int numB = SpriteGroup.GetSuffixNumber(this._IndexRecords[b].FileName);
          return numA.CompareTo(numB);
        });
      }

      // 依照排序方式排列群組
      this._SpriteGroups = groupDict.Values.ToList();
      SortSpriteGroups();
    }

    private void SortSpriteGroups()
    {
      if (this._SpriteGroups == null) return;

      switch (this._SpriteSortColumn)
      {
        case 2: // Size
          if (this._SpriteSortAscending)
            this._SpriteGroups.Sort((a, b) => a.TotalSize.CompareTo(b.TotalSize));
          else
            this._SpriteGroups.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
          break;
        case 1: // FileName - 使用數字優先排序
        default:
          if (this._SpriteSortAscending)
            this._SpriteGroups.Sort((a, b) => SpriteGroup.ComparePrefixes(a.Prefix, b.Prefix));
          else
            this._SpriteGroups.Sort((a, b) => SpriteGroup.ComparePrefixes(b.Prefix, a.Prefix));
          break;
      }
    }

    private void BuildSpriteDisplayItems()
    {
      this._SpriteDisplayItems = new List<object>();

      // 只加入 .spr 群組 (master-only view，不展開子項)
      foreach (var group in this._SpriteGroups)
      {
        // 檢查是否符合類型過濾
        if (!MatchesTypeFilter(group))
          continue;

        this._SpriteDisplayItems.Add(group);
        // 移除展開邏輯 - 子項目現在由右側 detail viewer 顯示
      }

      // 收集非 .spr 的檔案到 _OtherFilesIndexes (用於第二個 Tab)
      this._OtherFilesIndexes = new List<int>();
      foreach (int idx in this._FilteredIndexes)
      {
        string ext = Path.GetExtension(this._IndexRecords[idx].FileName).ToLower();
        if (ext != ".spr")
        {
          this._OtherFilesIndexes.Add(idx);
        }
      }

      // 更新 Tab 標題顯示數量
      UpdateSpriteModeTabTitles();

      // 更新其他檔案 ListView 的數量
      if (this.lvOtherFiles != null)
      {
        this.lvOtherFiles.VirtualListSize = this._OtherFilesIndexes.Count;
      }
    }

    private void UpdateSpriteModeTabTitles()
    {
      if (this.tabSprites != null)
      {
        int sprCount = this._SpriteGroups?.Sum(g => g.ItemIndexes.Count) ?? 0;
        this.tabSprites.Text = $"SPR 檔案 ({sprCount})";
      }
      if (this.tabOtherFiles != null)
      {
        int otherCount = this._OtherFilesIndexes?.Count ?? 0;
        this.tabOtherFiles.Text = $"其他檔案 ({otherCount})";
      }
    }

    private void lvIndexInfo_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
      // SPR List 模式
      if (this._IsSprListMode && this._FilteredSprListEntries != null)
      {
        if (e.ItemIndex < 0 || e.ItemIndex >= this._FilteredSprListEntries.Count)
        {
          e.Item = new ListViewItem("");
          return;
        }

        var entry = this._FilteredSprListEntries[e.ItemIndex];
        var listItem = new ListViewItem(entry.Id.ToString());
        listItem.SubItems.Add(entry.Name);
        listItem.SubItems.Add(entry.SpriteId.ToString());  // 圖檔編號
        listItem.SubItems.Add(entry.ImageCount.ToString());
        listItem.SubItems.Add(entry.TypeName);
        listItem.SubItems.Add(entry.Actions.Count.ToString());
        listItem.Tag = entry;
        e.Item = listItem;
        return;
      }

      // Sprite 模式使用不同的顯示邏輯 (master-detail view，只顯示群組)
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        // 建立空白項目的輔助方法 (3 欄: SpriteId, 資訊, 大小)
        ListViewItem createEmptyItem()
        {
          var item = new ListViewItem("");
          item.SubItems.Add("");
          item.SubItems.Add("");
          return item;
        }

        if (e.ItemIndex < 0 || e.ItemIndex >= this._SpriteDisplayItems.Count)
        {
          e.Item = createEmptyItem();
          return;
        }

        try
        {
          object item = this._SpriteDisplayItems[e.ItemIndex];
          if (item is SpriteGroup group)
          {
            // 顯示群組標題（移除結尾的 -）
            string displayPrefix = group.Prefix.TrimEnd('-');
            string sizeText = string.Format("{0:F1}", group.TotalSize / 1024.0);

            // 取得類型資訊
            string typeInfo = "";
            if (this._SpriteIdToEntry != null && int.TryParse(displayPrefix, out int spriteId))
            {
              if (this._SpriteIdToEntry.TryGetValue(spriteId, out var entry))
              {
                typeInfo = $" [{entry.TypeName}:{entry.Name}]";
              }
            }

            // 顯示: SpriteId (檔案數) [類型:名稱]
            var listItem = new ListViewItem(displayPrefix);  // SpriteId
            listItem.SubItems.Add("(" + group.ItemIndexes.Count + ")" + typeInfo);  // 檔案數 + 類型
            listItem.SubItems.Add(sizeText);  // Size (KB)
            e.Item = listItem;
          }
          else
          {
            e.Item = createEmptyItem();
          }
        }
        catch
        {
          e.Item = createEmptyItem();
        }
        return;
      }

      // 一般模式
      if (this._FilteredIndexes == null || e.ItemIndex < 0 || e.ItemIndex >= this._FilteredIndexes.Count)
      {
        // 建立空白項目避免錯誤
        var listItem = new ListViewItem("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        e.Item = listItem;
        return;
      }
      try
      {
        int realIndex = this._FilteredIndexes[e.ItemIndex];
        e.Item = this.CreatListViewItem(realIndex, this._IndexRecords[realIndex]);
        e.Item.Checked = this._CheckedIndexes.Contains(realIndex);
      }
      catch
      {
        var listItem = new ListViewItem("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        e.Item = listItem;
      }
    }

    private void lvIndexInfo_ItemCheck(object sender, ItemCheckEventArgs e)
    {
      if (this._FilteredIndexes == null || e.Index >= this._FilteredIndexes.Count)
        return;
      int realIndex = this._FilteredIndexes[e.Index];
      if (e.NewValue == CheckState.Checked)
        this._CheckedIndexes.Add(realIndex);
      else
        this._CheckedIndexes.Remove(realIndex);
      this.tssCheckedCount.Text = string.Format("已選：{0}", (object) this._CheckedIndexes.Count);
    }

    private ListViewItem CreatListViewItem(int ID, L1PakTools.IndexRecord IdxRec)
    {
      string sizeText = string.Format("{0:F1}", IdxRec.FileSize / 1024.0);

      return new ListViewItem(string.Format("{0, 5}", (object) (ID + 1)))
      {
        SubItems = {
          IdxRec.FileName,
          sizeText,
          IdxRec.Offset.ToString("X8")
        }
      };
    }

    private void lvIndexInfo_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      // Sprite 模式下使用自訂排序
      if (this._IsSpriteMode && this._SpriteGroups != null)
      {
        // 切換排序方向
        if (this._SpriteSortColumn == e.Column)
        {
          this._SpriteSortAscending = !this._SpriteSortAscending;
        }
        else
        {
          this._SpriteSortColumn = e.Column;
          this._SpriteSortAscending = true;
        }

        // 重新排序並顯示
        SortSpriteGroups();
        BuildSpriteDisplayItems();
        this.lvIndexInfo.VirtualListSize = this._SpriteDisplayItems.Count;
        this.lvIndexInfo.Invalidate();
        return;
      }

      // 一般模式
      int column = e.Column;
      if (this.lvIndexInfo.Tag == null)
        this.lvIndexInfo.Tag = (object) 0;
      if (Math.Abs((int) this.lvIndexInfo.Tag) == column)
        column = -(int) this.lvIndexInfo.Tag;
      this.lvIndexInfo.Tag = (object) column;
      this.lvIndexInfo.ListViewItemSorter = (IComparer) new frmMain.ListViewItemComparer(column);
    }

    private void lvIndexInfo_MouseClick(object sender, MouseEventArgs e)
    {
      if (this.lvIndexInfo.HitTest(e.Location).Item != null)
        this.lvIndexInfo.ContextMenuStrip = this.ctxMenu;
      else
        this.lvIndexInfo.ContextMenuStrip = (ContextMenuStrip) null;
    }

    private void lvIndexInfo_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
    }

    private void SelectionTimer_Tick(object sender, EventArgs e)
    {
      // 只在 Sprite 模式下計算選取大小
      if (!this._IsSpriteMode)
        return;

      // 判斷目前選中的 Tab
      bool isOtherFilesTab = this.tabSpriteMode != null &&
                             this.tabSpriteMode.SelectedTab == this.tabOtherFiles;

      int selectedCount;
      long totalSize = 0;
      int fileCount = 0;

      if (isOtherFilesTab && this.lvOtherFiles != null && this._OtherFilesIndexes != null)
      {
        // 其他檔案 Tab
        selectedCount = this.lvOtherFiles.SelectedIndices.Count;

        if (selectedCount == this._LastSelectedCount)
          return;
        this._LastSelectedCount = selectedCount;

        if (selectedCount == 0)
        {
          this.tssCheckedCount.Text = "已選：0";
          return;
        }

        foreach (int virtualIndex in this.lvOtherFiles.SelectedIndices)
        {
          if (virtualIndex < 0 || virtualIndex >= this._OtherFilesIndexes.Count)
            continue;
          int realIndex = this._OtherFilesIndexes[virtualIndex];
          if (realIndex >= 0 && realIndex < this._IndexRecords.Length)
          {
            totalSize += this._IndexRecords[realIndex].FileSize;
            fileCount++;
          }
        }
      }
      else if (this._SpriteDisplayItems != null)
      {
        // SPR 檔案 Tab
        selectedCount = this.lvIndexInfo.SelectedIndices.Count;

        if (selectedCount == this._LastSelectedCount)
          return;
        this._LastSelectedCount = selectedCount;

        if (selectedCount == 0)
        {
          this.tssCheckedCount.Text = "已選：0";
          return;
        }

        foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
        {
          if (virtualIndex < 0 || virtualIndex >= this._SpriteDisplayItems.Count)
            continue;
          object item = this._SpriteDisplayItems[virtualIndex];
          if (item is SpriteGroup group)
          {
            totalSize += group.TotalSize;
            fileCount += group.ItemIndexes.Count;
          }
          else if (item is int fileRealIndex)
          {
            if (fileRealIndex >= 0 && fileRealIndex < this._IndexRecords.Length)
            {
              totalSize += this._IndexRecords[fileRealIndex].FileSize;
              fileCount++;
            }
          }
        }
      }
      else
      {
        return;
      }

      double sizeMB = totalSize / 1024.0 / 1024.0;
      string sizeStr = sizeMB >= 1 ? $"{sizeMB:F2} MB" : $"{totalSize / 1024.0:F1} KB";
      this.tssCheckedCount.Text = $"已選：{selectedCount} 項 ({fileCount} 檔案), {sizeStr}";
    }

    private void lvIndexInfo_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lvIndexInfo.SelectedIndices.Count != 1)
        return;
      this.tssMessage.Text = "";
      this.ImageViewer.Image = (Image) null;

      int selectedVirtualIndex = this.lvIndexInfo.SelectedIndices[0];

      // SPR List 模式
      if (this._IsSprListMode && this._FilteredSprListEntries != null)
      {
        if (selectedVirtualIndex >= 0 && selectedVirtualIndex < this._FilteredSprListEntries.Count)
        {
          var entry = this._FilteredSprListEntries[selectedVirtualIndex];
          if (this.SprActionViewer != null)
          {
            this.SprActionViewer.ShowEntry(entry);
          }
          this.tssMessage.Text = $"#{entry.Id} {entry.Name} - {entry.Actions.Count} 動作";
        }
        return;
      }

      // Sprite 模式下處理群組點擊 - 顯示 detail
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        if (selectedVirtualIndex >= this._SpriteDisplayItems.Count)
          return;

        object item = this._SpriteDisplayItems[selectedVirtualIndex];

        // 如果點擊的是群組，在右側 detail viewer 顯示
        if (item is SpriteGroup group)
        {
          ShowSpriteGroupDetail(group);
          return;
        }

        // 如果是檔案項目，繼續正常處理 (不應該發生，因為現在只顯示 master)
        if (!(item is int))
          return;
      }

      // 檢查是否有未儲存的變更
      if (this._TextModified && this._CurrentEditingRealIndex >= 0)
      {
        L1PakTools.IndexRecord oldRecord = this._IndexRecords[this._CurrentEditingRealIndex];
        DialogResult result = MessageBox.Show(
          "您對 \"" + oldRecord.FileName + "\" 有未儲存的變更。\n\n是否要在切換前儲存？",
          "未儲存的變更",
          MessageBoxButtons.YesNoCancel,
          MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
          this.btnSaveText_Click(sender, e);
        }
        else if (result == DialogResult.Cancel)
        {
          return;
        }
      }

      // 重置編輯狀態
      this._TextModified = false;
      this.btnSaveText.Enabled = false;
      this.btnCancelEdit.Enabled = false;
      this._CurrentEditingRealIndex = -1;

      // 取得真實索引
      int realIndex;
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        object item = this._SpriteDisplayItems[selectedVirtualIndex];
        if (!(item is int idx))
          return;
        realIndex = idx;
      }
      else
      {
        if (this._FilteredIndexes == null || selectedVirtualIndex >= this._FilteredIndexes.Count)
          return;
        realIndex = this._FilteredIndexes[selectedVirtualIndex];
      }

      L1PakTools.IndexRecord record = this._IndexRecords[realIndex];

      // 決定要讀取的 PAK 檔案
      string pakFile;
      if (this._IsSpriteMode && !string.IsNullOrEmpty(record.SourcePak))
      {
        pakFile = record.SourcePak;
      }
      else
      {
        if (string.IsNullOrEmpty(this._PackFileName))
          return;
        pakFile = this._PackFileName.Replace(".idx", ".pak");
      }

      if (!File.Exists(pakFile))
      {
        this.tssMessage.Text = "PAK file not found: " + pakFile;
        return;
      }

      try
      {
        FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        object obj = this.LoadPakData_(fs, record);
        fs.Close();
        this.ViewerSwitch();
        if (this._InviewData == frmMain.InviewDataType.Text || this._InviewData == frmMain.InviewDataType.Empty)
        {
          // 暫時移除 TextChanged 事件避免觸發
          this.TextViewer.TextChanged -= new EventHandler(this.TextViewer_TextChanged);

          if (obj is string)
            this.TextViewer.Text = (string) obj;
          else if (obj is byte[])
            this.TextViewer.Text = Encoding.GetEncoding("big5").GetString((byte[]) obj);
          this.TextViewer.Tag = (object) (realIndex + 1).ToString();

          // 設定當前編輯的檔案索引（Sprite 模式下禁用編輯）
          if (!this._IsSpriteMode)
          {
            this._CurrentEditingRealIndex = realIndex;
            // 重新綁定 TextChanged 事件
            this.TextViewer.TextChanged += new EventHandler(this.TextViewer_TextChanged);
          }

          // 設定 TextViewer 的唯讀狀態
          this.TextViewer.ReadOnly = this._IsSpriteMode;

          // 如果是 Empty 類型，強制顯示 TextViewer
          if (this._InviewData == frmMain.InviewDataType.Empty)
          {
            this.TextViewer.Visible = true;
            this.ImageViewer.Visible = false;
            this.SprViewer.Visible = false;
          }
        }
        else if (this._InviewData == frmMain.InviewDataType.IMG || this._InviewData == frmMain.InviewDataType.BMP || (this._InviewData == frmMain.InviewDataType.TBT || this._InviewData == frmMain.InviewDataType.TIL))
        {
          this.ImageViewer.Image = (Image) obj;
        }
        else if (this._InviewData == frmMain.InviewDataType.SPR)
        {
          this.SprViewer.SprFrames = (L1Spr.Frame[]) obj;
          this.SprViewer.Start();
        }
      }
      catch (Exception ex)
      {
        this.tssMessage.Text = "Error: " + ex.Message;
      }
    }

    private void ViewerSwitch()
    {
      this.TextCompViewer.Visible = false;
      if (this._InviewData == frmMain.InviewDataType.Text)
      {
        this.TextViewer.Visible = true;
        this.ImageViewer.Visible = false;
        this.SprViewer.Visible = false;
      }
      else if (this._InviewData == frmMain.InviewDataType.IMG || this._InviewData == frmMain.InviewDataType.BMP || this._InviewData == frmMain.InviewDataType.TBT)
      {
        this.ImageViewer.Visible = true;
        this.TextViewer.Visible = false;
        this.SprViewer.Visible = false;
      }
      else
      {
        if (this._InviewData != frmMain.InviewDataType.SPR)
          return;
        this.SprViewer.Visible = true;
        this.ImageViewer.Visible = false;
        this.TextViewer.Visible = false;
      }
    }

    private object LoadPakData(FileStream fs, ListViewItem lvItem)
    {
      L1PakTools.IndexRecord indexRecord = this._IndexRecords[int.Parse(lvItem.Text) - 1];
      return this.LoadPakData_(fs, indexRecord);
    }

    private byte[] LoadPakBytes_(FileStream fs, ListViewItem lvItem) {
        L1PakTools.IndexRecord IdxRec = this._IndexRecords[int.Parse(lvItem.Text) - 1];
        return LoadPakBytes_(fs, IdxRec);
    }

    /// <summary>
    /// 從 PAK header 自動偵測壓縮類型
    /// </summary>
    private int DetectExtBCompression(byte[] header)
    {
      if (header.Length >= 2)
      {
        // zlib: 78 9C, 78 DA, 78 01, 78 5E
        if (header[0] == 0x78 && (header[1] == 0x9C || header[1] == 0xDA ||
            header[1] == 0x01 || header[1] == 0x5E))
          return 1;  // zlib
        // brotli: 通常以 0x5B 或 0x1B 開頭
        if (header[0] == 0x5B || header[0] == 0x1B)
          return 2;  // brotli
      }
      return 0;  // none/raw
    }

    /// <summary>
    /// 計算 ExtB 格式中指定 offset 的壓縮大小
    /// </summary>
    private int GetExtBCompressedSize(int offset)
    {
      if (this._ExtBSortedOffsets == null) return 0;

      int idx = this._ExtBSortedOffsets.BinarySearch(offset);
      if (idx < 0) return 0;

      if (idx + 1 < this._ExtBSortedOffsets.Count)
        return this._ExtBSortedOffsets[idx + 1] - offset;
      else
        return (int)(this._ExtBPakFileSize - offset);
    }

    /// <summary>
    /// 解壓縮 ExtB 格式資料
    /// </summary>
    private byte[] DecompressExtBData(byte[] compressedData, int compressionType, int uncompressedSize)
    {
      try
      {
        if (compressionType == 1) // zlib
        {
          using (var ms = new MemoryStream(compressedData))
          using (var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress))
          using (var output = new MemoryStream())
          {
            zlib.CopyTo(output);
            return output.ToArray();
          }
        }
        else if (compressionType == 2) // brotli
        {
          using (var ms = new MemoryStream(compressedData))
          using (var brotli = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionMode.Decompress))
          using (var output = new MemoryStream())
          {
            brotli.CopyTo(output);
            return output.ToArray();
          }
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Decompression failed: {ex.Message}");
      }
      return compressedData; // 解壓失敗則返回原始資料
    }

    /// <summary>
    /// 壓縮 ExtB 格式資料
    /// </summary>
    private byte[] CompressExtBData(byte[] data, int compressionType)
    {
      try
      {
        if (compressionType == 1) // zlib
        {
          using (var output = new MemoryStream())
          {
            using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
              zlib.Write(data, 0, data.Length);
            }
            return output.ToArray();
          }
        }
        else if (compressionType == 2) // brotli
        {
          using (var output = new MemoryStream())
          {
            using (var brotli = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
              brotli.Write(data, 0, data.Length);
            }
            return output.ToArray();
          }
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Compression failed: {ex.Message}");
      }
      return data; // 壓縮失敗則返回原始資料
    }

    /// <summary>
    /// 從原始 ExtB IDX 資料取得指定檔名的壓縮類型
    /// </summary>
    private int GetExtBCompressionType(string fileName)
    {
      if (this._ExtBRawData == null) return 1; // 預設 zlib

      const int headerSize = 0x10;
      const int entrySize = 0x80;
      int entryCount = (this._ExtBRawData.Length - headerSize) / entrySize;

      for (int i = 0; i < entryCount; i++)
      {
        int entryOffset = headerSize + i * entrySize;
        int nameStart = entryOffset + 8;
        int nameEnd = nameStart;
        while (nameEnd < entryOffset + 120 && this._ExtBRawData[nameEnd] != 0) nameEnd++;
        string name = Encoding.ASCII.GetString(this._ExtBRawData, nameStart, nameEnd - nameStart);

        if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
          return BitConverter.ToInt32(this._ExtBRawData, entryOffset + 4);
        }
      }
      return 1; // 預設 zlib
    }

    /// <summary>
    /// 讀取 ExtB 格式的壓縮資料 (不解壓)
    /// </summary>
    private byte[] ReadExtBCompressedData(FileStream fs, L1PakTools.IndexRecord rec)
    {
      int compressedSize = GetExtBCompressedSize(rec.Offset);
      if (compressedSize <= 0) return null;

      byte[] data = new byte[compressedSize];
      fs.Seek(rec.Offset, SeekOrigin.Begin);
      fs.Read(data, 0, compressedSize);
      return data;
    }

    private byte[] LoadPakBytes_(FileStream fs, L1PakTools.IndexRecord IdxRec) {
        string[] array = new string[14]
        {
    ".img",
    ".png",
    ".tbt",
    ".til",
    ".html",
    ".tbl",
    ".spr",
    ".bmp",
    ".h",
    ".ht",
    ".htm",
    ".txt",
    ".def",
    ".xml"
        };
        frmMain.InviewDataType[] inviewDataTypeArray = new frmMain.InviewDataType[14]
        {
    frmMain.InviewDataType.IMG,
    frmMain.InviewDataType.BMP,
    frmMain.InviewDataType.TBT,
    frmMain.InviewDataType.Empty,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.SPR,
    frmMain.InviewDataType.BMP,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text,
    frmMain.InviewDataType.Text
        };
        int index = Array.IndexOf<string>(array, Path.GetExtension(IdxRec.FileName).ToLower());
        this._InviewData = index != -1 ? inviewDataTypeArray[index] : frmMain.InviewDataType.Empty;

        if (IdxRec.FileName.IndexOf("list.spr") != -1)
        {
            this._InviewData = frmMain.InviewDataType.Text;
        }

        // ExtB 格式需要讀取壓縮資料並解壓
        if (this._IsExtBFormat)
        {
            int compressedSize = GetExtBCompressedSize(IdxRec.Offset);
            if (compressedSize > 0)
            {
                byte[] compressedData = new byte[compressedSize];
                fs.Seek((long)IdxRec.Offset, SeekOrigin.Begin);
                fs.Read(compressedData, 0, compressedSize);

                // 從 header 自動偵測壓縮類型
                int compressionType = DetectExtBCompression(compressedData);

                if (compressionType > 0)
                {
                    return DecompressExtBData(compressedData, compressionType, IdxRec.FileSize);
                }
                else
                {
                    // 無壓縮，直接返回資料 (可能需要截斷到正確大小)
                    if (compressedData.Length > IdxRec.FileSize && IdxRec.FileSize > 0)
                    {
                        byte[] result = new byte[IdxRec.FileSize];
                        Array.Copy(compressedData, result, IdxRec.FileSize);
                        return result;
                    }
                    return compressedData;
                }
            }
        }

        byte[] numArray = new byte[IdxRec.FileSize];
        fs.Seek((long)IdxRec.Offset, SeekOrigin.Begin);
        fs.Read(numArray, 0, IdxRec.FileSize);
        if (this._IsPackFileProtected)
        {
            this.tssProgressName.Text = "解碼中...";
            numArray = L1PakTools.Decode(numArray, 0);
            this.tssProgressName.Text = "";
            if (this._InviewData == frmMain.InviewDataType.SPR)
                this._InviewData = frmMain.InviewDataType.Text;
        }
         return numArray;
    }

    private object LoadPakData_(FileStream fs, L1PakTools.IndexRecord IdxRec)
    {
      string[] array = new string[14]
      {
        ".img",
        ".png",
        ".tbt",
        ".til",
        ".html",
        ".tbl",
        ".spr",
        ".bmp",
        ".h",
        ".ht",
        ".htm",
        ".txt",
        ".def",
        ".xml"
      };
      frmMain.InviewDataType[] inviewDataTypeArray = new frmMain.InviewDataType[14]
      {
        frmMain.InviewDataType.IMG,
        frmMain.InviewDataType.BMP,
        frmMain.InviewDataType.TBT,
        frmMain.InviewDataType.Empty,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.SPR,
        frmMain.InviewDataType.BMP,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text,
        frmMain.InviewDataType.Text
      };
      int index = Array.IndexOf<string>(array, Path.GetExtension(IdxRec.FileName).ToLower());
      this._InviewData = index != -1 ? inviewDataTypeArray[index] : frmMain.InviewDataType.Empty;

      if(IdxRec.FileName.IndexOf("list.spr") != -1)
      {
         this._InviewData = frmMain.InviewDataType.Text;
      }
      byte[] numArray = new byte[IdxRec.FileSize];
      fs.Seek((long) IdxRec.Offset, SeekOrigin.Begin);
      fs.Read(numArray, 0, IdxRec.FileSize);
      if (this._IsPackFileProtected)
      {
        this.tssProgressName.Text = "Decoding...";
        numArray = L1PakTools.Decode(numArray, 0);
        this.tssProgressName.Text = "";
        if (this._InviewData == frmMain.InviewDataType.SPR)
          this._InviewData = frmMain.InviewDataType.Text;
      }

      // Decrypt XML files if encrypted (starts with 'X')
      this._IsCurrentFileXmlEncrypted = false;
      this._CurrentXmlEncoding = null;
      bool isXmlFile = Path.GetExtension(IdxRec.FileName).ToLower() == ".xml";
      if (isXmlFile)
      {
        if (XmlCracker.IsEncrypted(numArray))
        {
          this._IsCurrentFileXmlEncrypted = true;
          numArray = XmlCracker.Decrypt(numArray);
        }
        // 從 XML 內容取得 encoding
        this._CurrentXmlEncoding = XmlCracker.GetXmlEncoding(numArray, IdxRec.FileName);
        this.tssMessage.Text = this._IsCurrentFileXmlEncrypted
          ? $"[XML Encrypted] [{this._CurrentXmlEncoding.EncodingName}]"
          : $"[{this._CurrentXmlEncoding.EncodingName}]";
      }

      object obj = (object) numArray;
      try
      {
        switch (this._InviewData)
        {
          case frmMain.InviewDataType.Text:
            // 對 XML 檔案使用解析出的 encoding，其他檔案使用檔名判斷
            if (isXmlFile && this._CurrentXmlEncoding != null)
            {
              obj = (object) this._CurrentXmlEncoding.GetString(numArray);
            }
            else
            {
              // -k: Korean (euc-kr), -j: Japanese (shift_jis), -h: Simplified Chinese (euc-cn/gb2312), -c or default: Traditional Chinese (big5)
              string fileNameLower = IdxRec.FileName.ToLower();
              if (fileNameLower.IndexOf("-k.") >= 0)
                obj = (object) Encoding.GetEncoding("euc-kr").GetString(numArray);
              else if (fileNameLower.IndexOf("-j.") >= 0)
                obj = (object) Encoding.GetEncoding("shift_jis").GetString(numArray);
              else if (fileNameLower.IndexOf("-h.") >= 0)
                obj = (object) Encoding.GetEncoding("gb2312").GetString(numArray);
              else
                obj = (object) Encoding.GetEncoding("big5").GetString(numArray);
            }
            break;
          case frmMain.InviewDataType.IMG:
            obj = (object) ImageConvert.Load_IMG(numArray);
            break;
          case frmMain.InviewDataType.BMP:
            MemoryStream memoryStream = new MemoryStream(numArray);
            try
            {
              obj = (object) Image.FromStream((Stream) memoryStream);
              break;
            }
            finally
            {
              memoryStream.Close();
            }
          case frmMain.InviewDataType.SPR:
            obj = (object) L1Spr.Load(numArray);
            break;
          case frmMain.InviewDataType.TIL:
            obj = (object) ImageConvert.Load_TIL(numArray);
            break;
          case frmMain.InviewDataType.TBT:
            obj = (object) ImageConvert.Load_TBT(numArray);
            break;
        }
      }
      catch
      {
        this._InviewData = frmMain.InviewDataType.Empty;
        this.tssMessage.Text = "*Error *: Can't open this file!";
      }
      return obj;
    }

    private void tsmSelectAll_Click(object sender, EventArgs e)
    {
      // 選擇所有項目（而不是勾選）
      if (this._FilteredIndexes != null && this._FilteredIndexes.Count > 0)
      {
        this.lvIndexInfo.BeginUpdate();
        for (int i = 0; i < this.lvIndexInfo.VirtualListSize; i++)
        {
          this.lvIndexInfo.SelectedIndices.Add(i);
        }
        this.lvIndexInfo.EndUpdate();
      }
    }

    private void tsmUnselectAll_Click(object sender, EventArgs e)
    {
      // 取消選擇所有項目
      this.lvIndexInfo.SelectedIndices.Clear();
    }

    private void tsmDelete_Click(object sender, EventArgs e)
    {
      // Sprite 模式使用特殊處理
      if (this._IsSpriteMode)
      {
        DeleteSpriteFiles();
        return;
      }

      if (this.lvIndexInfo.SelectedIndices.Count == 0 || this._FilteredIndexes == null)
        return;

      // 收集要刪除的檔案索引
      var indicesToDelete = new List<int>();
      var fileNames = new List<string>();

      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (virtualIndex < this._FilteredIndexes.Count)
        {
          int realIndex = this._FilteredIndexes[virtualIndex];
          indicesToDelete.Add(realIndex);
          fileNames.Add(this._IndexRecords[realIndex].FileName);
        }
      }

      if (indicesToDelete.Count == 0)
        return;

      // 確認對話框
      string message = indicesToDelete.Count == 1
        ? $"確定要刪除 \"{fileNames[0]}\" 嗎？\n\n這將會重建 PAK 和 IDX 檔案。\n修改前將會建立備份。"
        : $"確定要刪除選取的 {indicesToDelete.Count} 個檔案嗎？\n\n這將會重建 PAK 和 IDX 檔案。\n修改前將會建立備份。";

      DialogResult result = MessageBox.Show(
        message,
        "確認刪除",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning);

      if (result != DialogResult.Yes)
        return;

      try
      {
        string pakFile = this._PackFileName.Replace(".idx", ".pak");

        // 呼叫刪除功能
        var (error, newRecords) = PakReader.DeleteFilesCore(
          this._PackFileName,
          pakFile,
          this._IndexRecords,
          indicesToDelete.ToArray(),
          this._IsPackFileProtected);

        if (error != null)
        {
          MessageBox.Show("刪除檔案時發生錯誤：" + error, "刪除錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        // 更新記錄
        this._IndexRecords = newRecords;

        // 重置編輯狀態
        this._TextModified = false;
        this.btnSaveText.Enabled = false;
        this.btnCancelEdit.Enabled = false;
        this._CurrentEditingRealIndex = -1;

        // 重新過濾並顯示
        this.ShowRecords(this._IndexRecords);

        this.tssMessage.Text = indicesToDelete.Count == 1
          ? $"已刪除: {fileNames[0]}"
          : $"已刪除 {indicesToDelete.Count} 個檔案";
      }
      catch (IOException ex)
      {
        MessageBox.Show(
          "無法寫入檔案，檔案可能正被其他程式使用中。\n\n" +
          "請先關閉天堂遊戲或其他編輯器後再試一次。\n\n" +
          "Error: " + ex.Message,
          "檔案鎖定",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      }
      catch (Exception ex)
      {
        MessageBox.Show("刪除檔案時發生錯誤：" + ex.Message, "刪除錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void DeleteSpriteFiles()
    {
      if (this.lvIndexInfo.SelectedIndices.Count == 0 || this._SpriteDisplayItems == null)
        return;

      // 收集要刪除的檔案 (按 PAK 檔案分組)
      // key: PAK 檔案路徑, value: (檔名列表, 在該 PAK 中的索引列表)
      var deleteByPak = new Dictionary<string, (List<string> fileNames, List<int> indices)>();
      var allFileNames = new List<string>();

      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (virtualIndex < 0 || virtualIndex >= this._SpriteDisplayItems.Count)
          continue;

        object item = this._SpriteDisplayItems[virtualIndex];

        if (item is SpriteGroup group)
        {
          // 選中群組，刪除群組內所有檔案
          foreach (int realIndex in group.ItemIndexes)
          {
            AddFileToDeleteList(realIndex, deleteByPak, allFileNames);
          }
        }
        else if (item is int realIndex)
        {
          // 選中單一檔案
          AddFileToDeleteList(realIndex, deleteByPak, allFileNames);
        }
      }

      if (allFileNames.Count == 0)
        return;

      // 確認對話框
      string pakInfo = string.Join("\n", deleteByPak.Select(kv =>
        $"  {Path.GetFileName(kv.Key)}: {kv.Value.indices.Count} 個檔案"));

      string message = allFileNames.Count == 1
        ? $"確定要刪除 \"{allFileNames[0]}\" 嗎？\n\n將從以下 PAK 檔案中刪除：\n{pakInfo}"
        : $"確定要刪除選取的 {allFileNames.Count} 個檔案嗎？\n\n將從以下 PAK 檔案中刪除：\n{pakInfo}";

      DialogResult result = MessageBox.Show(
        message,
        "確認刪除 (Sprite 模式)",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning);

      if (result != DialogResult.Yes)
        return;

      try
      {
        this.Cursor = Cursors.WaitCursor;
        int totalDeleted = 0;
        var errors = new List<string>();
        int pakCount = deleteByPak.Count;
        int pakIndex = 0;

        // 逐一處理每個 PAK 檔案
        foreach (var kvp in deleteByPak)
        {
          pakIndex++;
          string pakFile = kvp.Key;  // key 是 pakFile (與 _SpritePackages 一致)
          string idxFile = pakFile.Replace(".pak", ".idx");
          var indicesToDelete = kvp.Value.indices;

          // 更新狀態
          this.tssMessage.Text = $"正在處理 {Path.GetFileName(pakFile)} ({pakIndex}/{pakCount})，刪除 {indicesToDelete.Count} 個檔案...";
          Application.DoEvents();

          // 取得該 PAK 的記錄
          if (!this._SpritePackages.TryGetValue(pakFile, out var packageInfo))
            continue;

          var (records, isProtected) = packageInfo;

          // 呼叫刪除功能（同一個 PAK 的所有檔案一次處理）
          var (error, newRecords) = PakReader.DeleteFilesCore(
            idxFile,
            pakFile,
            records,
            indicesToDelete.ToArray(),
            isProtected);

          if (error != null)
          {
            errors.Add($"{Path.GetFileName(pakFile)}: {error}");
          }
          else
          {
            totalDeleted += indicesToDelete.Count;

            // 從磁碟重新載入該 PAK 的 index（offset 已變更）
            var reloaded = PakReader.LoadIndex(idxFile);
            if (reloaded.HasValue)
            {
              this._SpritePackages[pakFile] = reloaded.Value;
            }
          }
        }

        if (errors.Count > 0)
        {
          MessageBox.Show(
            "部分檔案刪除時發生錯誤：\n" + string.Join("\n", errors),
            "刪除錯誤",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        }

        // 重新載入 Sprite 模式
        this.LoadSpriteMode();

        this.tssMessage.Text = $"已從 {deleteByPak.Count} 個 PAK 中刪除 {totalDeleted} 個檔案";
      }
      catch (IOException ex)
      {
        MessageBox.Show(
          "無法寫入檔案，檔案可能正被其他程式使用中。\n\n" +
          "請先關閉天堂遊戲或其他編輯器後再試一次。\n\n" +
          "Error: " + ex.Message,
          "檔案鎖定",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      }
      catch (Exception ex)
      {
        MessageBox.Show("刪除檔案時發生錯誤：" + ex.Message, "刪除錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        this.Cursor = Cursors.Default;
      }
    }

    private void AddFileToDeleteList(int realIndex,
      Dictionary<string, (List<string> fileNames, List<int> indices)> deleteByPak,
      List<string> allFileNames)
    {
      var record = this._IndexRecords[realIndex];
      string sourcePak = record.SourcePak;

      if (string.IsNullOrEmpty(sourcePak))
        return;

      // 找出該檔案在原始 PAK 中的索引
      if (!this._SpritePackages.TryGetValue(sourcePak, out var packageInfo))
        return;

      var (records, _) = packageInfo;

      // 在原始 PAK 中找到對應的索引
      int originalIndex = -1;
      for (int i = 0; i < records.Length; i++)
      {
        if (records[i].FileName == record.FileName && records[i].Offset == record.Offset)
        {
          originalIndex = i;
          break;
        }
      }

      if (originalIndex < 0)
        return;

      // 加入刪除清單
      if (!deleteByPak.ContainsKey(sourcePak))
      {
        deleteByPak[sourcePak] = (new List<string>(), new List<int>());
      }

      var (fileNames, indices) = deleteByPak[sourcePak];

      // 避免重複
      if (!indices.Contains(originalIndex))
      {
        indices.Add(originalIndex);
        fileNames.Add(record.FileName);
        allFileNames.Add(record.FileName);
      }
    }

    private void tsmOptimizePng_Click(object sender, EventArgs e)
    {
      if (this._IndexRecords == null || this.lvIndexInfo.SelectedIndices.Count == 0)
        return;

      // 收集選取的 PNG 檔案
      var pngFiles = new List<(int realIndex, string fileName, string pakFile)>();

      foreach (int idx in this.lvIndexInfo.SelectedIndices)
      {
        int realIndex = this._IsSpriteMode && this._SpriteDisplayItems != null
          ? (idx < this._SpriteDisplayItems.Count && this._SpriteDisplayItems[idx] is int ri ? ri : -1)
          : (this._FilteredIndexes != null && idx < this._FilteredIndexes.Count ? this._FilteredIndexes[idx] : -1);

        if (realIndex < 0 || realIndex >= this._IndexRecords.Length)
          continue;

        var record = this._IndexRecords[realIndex];
        if (!record.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
          continue;

        string pakFile = this._IsSpriteMode && !string.IsNullOrEmpty(record.SourcePak)
          ? record.SourcePak
          : this._PackFileName;

        pngFiles.Add((realIndex, record.FileName, pakFile));
      }

      if (pngFiles.Count == 0)
      {
        MessageBox.Show("沒有選取 PNG 檔案", "壓縮 PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      // 確認對話框
      if (MessageBox.Show(
        $"確定要壓縮 {pngFiles.Count} 個 PNG 檔案嗎？\n\n此操作會直接修改 PAK 檔案中的 PNG 資料。",
        "確認壓縮",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      try
      {
        this.Cursor = Cursors.WaitCursor;
        int successCount = 0;
        long totalSaved = 0;
        var errors = new List<string>();

        for (int i = 0; i < pngFiles.Count; i++)
        {
          var (realIndex, fileName, pakFile) = pngFiles[i];
          this.tssMessage.Text = $"壓縮中 ({i + 1}/{pngFiles.Count}): {fileName}";
          Application.DoEvents();

          try
          {
            // 讀取 PNG 資料
            var record = this._IndexRecords[realIndex];
            byte[] pngData;

            using (var fs = new FileStream(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
              fs.Seek(record.Offset, SeekOrigin.Begin);
              pngData = new byte[record.FileSize];
              fs.Read(pngData, 0, record.FileSize);

              if (this._IsPackFileProtected)
                pngData = L1PakTools.Decode(pngData, 0);
            }

            // 壓縮
            var (optimizedData, savedBytes, error) = Utility.PngOptimizer.OptimizeData(pngData);

            if (error != null)
            {
              errors.Add($"{fileName}: {error}");
              continue;
            }

            if (savedBytes <= 0)
            {
              // 已經是最佳壓縮，不需要更新
              successCount++;
              continue;
            }

            // 如果大小改變，需要更新 PAK（使用 update 邏輯）
            // 注意：PNG 壓縮後通常會變小，需要重建 PAK
            // 這裡簡化處理：只有大小不變時才直接覆蓋

            if (optimizedData.Length == pngData.Length)
            {
              // 大小相同，可以直接覆蓋
              byte[] dataToWrite = this._IsPackFileProtected
                ? L1PakTools.Encode(optimizedData, 0)
                : optimizedData;

              using (var fs = new FileStream(pakFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
              {
                fs.Seek(record.Offset, SeekOrigin.Begin);
                fs.Write(dataToWrite, 0, dataToWrite.Length);
              }

              successCount++;
              totalSaved += savedBytes;
            }
            else
            {
              // 大小改變，需要重建 PAK - 暫不支援
              errors.Add($"{fileName}: 壓縮後大小改變 ({pngData.Length} → {optimizedData.Length})，需重建 PAK (暫不支援)");
            }
          }
          catch (Exception ex)
          {
            errors.Add($"{fileName}: {ex.Message}");
          }
        }

        // 顯示結果
        string resultMsg = $"完成！成功壓縮 {successCount}/{pngFiles.Count} 個檔案";
        if (totalSaved > 0)
          resultMsg += $"\n節省空間: {totalSaved / 1024.0:F2} KB";

        if (errors.Count > 0)
        {
          resultMsg += $"\n\n錯誤 ({errors.Count}):\n" + string.Join("\n", errors.Take(10));
          if (errors.Count > 10)
            resultMsg += $"\n... 還有 {errors.Count - 10} 個錯誤";
        }

        this.tssMessage.Text = $"已壓縮 {successCount} 個 PNG，節省 {totalSaved / 1024.0:F2} KB";
        MessageBox.Show(resultMsg, "壓縮完成", MessageBoxButtons.OK,
          errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
      }
      finally
      {
        this.Cursor = Cursors.Default;
      }
    }

    private async void mnuTools_OptimizePng_Click(object sender, EventArgs e)
    {
      // 選擇要處理的資料夾（或使用目前開啟的資料夾）
      string targetFolder = this._IsSpriteMode ? this._SelectedFolder : Path.GetDirectoryName(this._PackFileName);

      if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
      {
        // 開啟資料夾選擇對話框
        using (var fbd = new FolderBrowserDialog())
        {
          fbd.Description = "選擇包含 PAK 檔案的資料夾";
          if (fbd.ShowDialog() != DialogResult.OK)
            return;
          targetFolder = fbd.SelectedPath;
        }
      }

      // 找出所有 idx 檔案
      var idxFiles = Directory.GetFiles(targetFolder, "*.idx");
      if (idxFiles.Length == 0)
      {
        MessageBox.Show("找不到任何 IDX 檔案", "批次壓縮 PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      // 確認對話框
      if (MessageBox.Show(
        $"在 {Path.GetFileName(targetFolder)} 中找到 {idxFiles.Length} 個 PAK 檔案。\n\n" +
        "確定要壓縮所有 PAK 中的 PNG 檔案嗎？\n\n" +
        "注意：此操作會重建所有 PAK 檔案，請確保已備份。",
        "確認批次壓縮",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      // 使用進度視窗
      using (var progressForm = new frmPngOptimizeProgress())
      {
        // 顯示視窗並開始處理
        progressForm.Show(this);
        await progressForm.ProcessAsync(idxFiles);

        // 更新狀態列
        long totalSaved = progressForm.TotalOriginalSize - progressForm.TotalNewSize;
        this.tssMessage.Text = $"壓縮完成！節省 {totalSaved / 1024.0 / 1024.0:F2} MB";

        // 如果有開啟 PAK，重新載入
        if (progressForm.TotalPngCount > 0)
        {
          if (this._IsSpriteMode)
          {
            this.LoadSpriteMode();
          }
          else if (!string.IsNullOrEmpty(this._PackFileName))
          {
            string idxFile = this._PackFileName.Replace(".pak", ".idx");
            if (File.Exists(idxFile))
            {
              byte[] idxData = this.LoadIndexData(idxFile);
              if (idxData != null)
              {
                this._IndexRecords = this.CreatIndexRecords(idxData);
                this.ShowRecords(this._IndexRecords);
              }
            }
          }
        }
      }
    }

    private void tsmCopyFileName_Click(object sender, EventArgs e)
    {
      if (this.lvIndexInfo.SelectedIndices.Count == 0 || this._FilteredIndexes == null)
        return;
      int virtualIndex = this.lvIndexInfo.SelectedIndices[0];
      int realIndex = this._FilteredIndexes[virtualIndex];
      string fileName = this._IndexRecords[realIndex].FileName;
      string fullPath = this._PackFileName + "#" + fileName;
      Clipboard.SetText(fullPath);
      this.tssMessage.Text = "Copied: " + fullPath;
    }

    private void tsmExport_Click(object sender, EventArgs e)
    {
      if (this.lvIndexInfo.SelectedIndices.Count == 0)
        return;

      // Sprite 模式下，匯出選取的群組
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        int exportedGroups = 0;
        foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
        {
          if (virtualIndex < this._SpriteDisplayItems.Count)
          {
            object item = this._SpriteDisplayItems[virtualIndex];
            if (item is SpriteGroup group)
            {
              this.ExportSpriteGroup(null, group);
              exportedGroups++;
            }
          }
        }
        if (exportedGroups > 0)
        {
          this.tssMessage.Text = $"已匯出 {exportedGroups} 個群組";
          return;
        }
      }

      if (this._FilteredIndexes == null)
        return;

      // 收集所有選取的實際索引
      List<int> realIndexes = new List<int>();
      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (virtualIndex < this._FilteredIndexes.Count)
          realIndexes.Add(this._FilteredIndexes[virtualIndex]);
      }

      string exportPath = Path.GetDirectoryName(this._PackFileName);
      ExportMultipleFiles(exportPath, realIndexes);
    }

    private void tsmExportTo_Click(object sender, EventArgs e)
    {
      if (this.dlgOpenFolder.ShowDialog((IWin32Window) this) != DialogResult.OK)
        return;
      if (this.lvIndexInfo.SelectedIndices.Count == 0)
        return;
      string selectedPath = this.dlgOpenFolder.SelectedPath;

      // Sprite 模式下，匯出選取的群組
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        int exportedGroups = 0;
        foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
        {
          if (virtualIndex < this._SpriteDisplayItems.Count)
          {
            object item = this._SpriteDisplayItems[virtualIndex];
            if (item is SpriteGroup group)
            {
              this.ExportSpriteGroup(selectedPath, group);
              exportedGroups++;
            }
          }
        }
        if (exportedGroups > 0)
        {
          this.tssMessage.Text = $"已匯出 {exportedGroups} 個群組到 {selectedPath}";
          return;
        }
      }

      if (this._FilteredIndexes == null)
        return;

      // 收集所有選取的實際索引
      List<int> realIndexes = new List<int>();
      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (virtualIndex < this._FilteredIndexes.Count)
          realIndexes.Add(this._FilteredIndexes[virtualIndex]);
      }

      ExportMultipleFiles(selectedPath, realIndexes);
    }

    private void ExportMultipleFiles(string exportPath, List<int> realIndexes)
    {
      if (realIndexes.Count == 0)
        return;

      string pakFile = this._PackFileName.Replace(".idx", ".pak");
      int exported = 0;

      // 使用 Dictionary 去重（相同檔名只保留最後一個）
      var exportDict = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

      // 先讀取所有資料
      using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        foreach (int realIndex in realIndexes)
        {
          var record = this._IndexRecords[realIndex];
          byte[] data = new byte[record.FileSize];
          fs.Seek(record.Offset, SeekOrigin.Begin);
          fs.Read(data, 0, record.FileSize);

          if (this._IsPackFileProtected)
            data = L1PakTools.Decode(data, 0);

          // 相同檔名會覆蓋，保留最後一個
          exportDict[record.FileName] = data;
        }
      }

      // 平行寫入檔案（已去重，不會有衝突）
      System.Threading.Tasks.Parallel.ForEach(exportDict, kvp =>
      {
        string filePath = Path.Combine(exportPath, kvp.Key);
        File.WriteAllBytes(filePath, kvp.Value);
        System.Threading.Interlocked.Increment(ref exported);
      });

      this.tssMessage.Text = $"已匯出 {exported} 個檔案到 {exportPath}";
    }

    private void mnuTools_Export_Click(object sender, EventArgs e)
    {
      // Sprite 模式下，匯出選取的群組
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        int exportedGroups = 0;
        foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
        {
          if (virtualIndex < this._SpriteDisplayItems.Count)
          {
            object item = this._SpriteDisplayItems[virtualIndex];
            if (item is SpriteGroup group)
            {
              this.ExportSpriteGroup(null, group);
              exportedGroups++;
            }
          }
        }
        if (exportedGroups > 0)
        {
          this.tssMessage.Text = $"已匯出 {exportedGroups} 個群組";
          return;
        }
      }

      // 將選取的虛擬索引轉換為實際索引
      List<int> realIndexes = new List<int>();
      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (this._FilteredIndexes != null && virtualIndex < this._FilteredIndexes.Count)
        {
          realIndexes.Add(this._FilteredIndexes[virtualIndex]);
        }
      }

      // Sprite 模式下，記錄可能來自不同的 pak 檔案
      if (this._PackFileName != null)
      {
        string exportPath = Path.GetDirectoryName(this._PackFileName);
        FileStream fs = File.Open(this._PackFileName.Replace(".idx", ".pak"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (int realIndex in realIndexes)
        {
          L1PakTools.IndexRecord record = this._IndexRecords[realIndex];
          object data = this.LoadPakData_(fs, record);
          this.ExportDataByIndex(exportPath, realIndex, data, this.LoadPakBytes_(fs, record));
        }
        fs.Close();
      }
      else
      {
        // Sprite 模式：按 SourcePak 分組處理
        var groupedByPak = realIndexes
          .Select(i => new { Index = i, Record = this._IndexRecords[i] })
          .GroupBy(x => x.Record.SourcePak);
        foreach (var group in groupedByPak)
        {
          string exportPath = Path.GetDirectoryName(group.Key);
          FileStream fs = File.Open(group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
          foreach (var item in group)
          {
            object data = this.LoadPakData_(fs, item.Record);
            this.ExportDataByIndex(exportPath, item.Index, data, this.LoadPakBytes_(fs, item.Record));
          }
          fs.Close();
        }
      }
    }

    private void mnuTools_ExportTo_Click(object sender, EventArgs e)
    {
      if (this.dlgOpenFolder.ShowDialog((IWin32Window) this) != DialogResult.OK)
        return;
      string selectedPath = this.dlgOpenFolder.SelectedPath;

      // Sprite 模式下，匯出選取的群組
      if (this._IsSpriteMode && this._SpriteDisplayItems != null)
      {
        int exportedGroups = 0;
        foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
        {
          if (virtualIndex < this._SpriteDisplayItems.Count)
          {
            object item = this._SpriteDisplayItems[virtualIndex];
            if (item is SpriteGroup group)
            {
              this.ExportSpriteGroup(selectedPath, group);
              exportedGroups++;
            }
          }
        }
        if (exportedGroups > 0)
        {
          this.tssMessage.Text = $"已匯出 {exportedGroups} 個群組到 {selectedPath}";
          return;
        }
      }

      // 將選取的虛擬索引轉換為實際索引
      List<int> realIndexes = new List<int>();
      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (this._FilteredIndexes != null && virtualIndex < this._FilteredIndexes.Count)
        {
          realIndexes.Add(this._FilteredIndexes[virtualIndex]);
        }
      }

      // Sprite 模式下，記錄可能來自不同的 pak 檔案
      if (this._PackFileName != null)
      {
        FileStream fs = File.Open(this._PackFileName.Replace(".idx", ".pak"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (int realIndex in realIndexes)
        {
          L1PakTools.IndexRecord record = this._IndexRecords[realIndex];
          object data = this.LoadPakData_(fs, record);
          this.ExportDataByIndex(selectedPath, realIndex, data, this.LoadPakBytes_(fs, record));
        }
        fs.Close();
      }
      else
      {
        // Sprite 模式：按 SourcePak 分組處理
        var groupedByPak = realIndexes
          .Select(i => new { Index = i, Record = this._IndexRecords[i] })
          .GroupBy(x => x.Record.SourcePak);
        foreach (var group in groupedByPak)
        {
          FileStream fs = File.Open(group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
          foreach (var item in group)
          {
            object data = this.LoadPakData_(fs, item.Record);
            this.ExportDataByIndex(selectedPath, item.Index, data, this.LoadPakBytes_(fs, item.Record));
          }
          fs.Close();
        }
      }
    }

    private void ExportSpriteGroup(string path, SpriteGroup group)
    {
      // 按 SourcePak 分組處理，提高效率
      var groupedByPak = group.ItemIndexes
        .Select(i => new { Index = i, Record = this._IndexRecords[i] })
        .GroupBy(x => x.Record.SourcePak);

      int exportedCount = 0;
      foreach (var pakGroup in groupedByPak)
      {
        string exportPath = path ?? Path.GetDirectoryName(pakGroup.Key);
        FileStream fs = File.Open(pakGroup.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (var item in pakGroup)
        {
          object data = this.LoadPakData_(fs, item.Record);
          this.ExportDataByIndex(exportPath, item.Index, data, this.LoadPakBytes_(fs, item.Record));
          exportedCount++;
        }
        fs.Close();
      }

      this.tssMessage.Text = $"已匯出 {group.Prefix} 群組共 {exportedCount} 個檔案";
    }

    private void ExportSelectedByIndex(string path, int realIndex)
    {
      L1PakTools.IndexRecord record = this._IndexRecords[realIndex];
      // Sprite 模式下使用 record.SourcePak，否則使用 _PackFileName
      string pakFile = this._PackFileName != null
        ? this._PackFileName.Replace(".idx", ".pak")
        : record.SourcePak;
      FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      object data = this.LoadPakData_(fs, record);
      this.ExportDataByIndex(path, realIndex, data, this.LoadPakBytes_(fs, record));
      fs.Close();
    }

    private void ExportDataByIndex(string path, int realIndex, object data, byte[] origin_bytes)
    {
      string str = this._IndexRecords[realIndex].FileName;
      if (path != null)
        str = path + "\\" + str;
      if (this._InviewData == frmMain.InviewDataType.Text)
        File.WriteAllText(str, (string) data);
      else if (this._InviewData == frmMain.InviewDataType.IMG)
        ((Image) data).Save(str.Replace(".img", ".bmp"), ImageFormat.Bmp);
      else if (this._InviewData == frmMain.InviewDataType.BMP)
        ((Image) data).Save(str, ImageFormat.Png);
      else if (this._InviewData == frmMain.InviewDataType.TBT)
        ((Image) data).Save(str.Replace(".tbt", ".gif"), ImageFormat.Gif);
      else if (this._InviewData == frmMain.InviewDataType.SPR)
      {
        L1Spr.Frame[] frameArray = null;
        if(data is byte[])
        {
            frameArray = L1Spr.Load((byte[])data);
        }else if(data is L1Spr.Frame[])
        {
            frameArray = (L1Spr.Frame[])data;
        }
        for (int index = 0; index < frameArray.Length; ++index)
        {
          if (frameArray[index].image != null)
            frameArray[index].image.Save(str.Replace(".spr", string.Format("-{0:D2}(view).bmp", (object) index)), ImageFormat.Bmp);
        }
        File.WriteAllBytes(str, (byte[])origin_bytes);
      }
      else
        File.WriteAllBytes(str, (byte[])origin_bytes);
    }

    private void ExportData(string Path, ListViewItem lvItem, object data,byte[] origin_bytes)
    {
      string str = this._IndexRecords[int.Parse(lvItem.Text) - 1].FileName;
      if (Path != null)
        str = Path + "\\" + str;
      if (this._InviewData == frmMain.InviewDataType.Text)
        File.WriteAllText(str, (string) data);
      else if (this._InviewData == frmMain.InviewDataType.IMG)
        ((Image) data).Save(str.Replace(".img", ".bmp"), ImageFormat.Bmp);
      else if (this._InviewData == frmMain.InviewDataType.BMP)
        ((Image) data).Save(str, ImageFormat.Png);
      else if (this._InviewData == frmMain.InviewDataType.TBT)
        ((Image) data).Save(str.Replace(".tbt", ".gif"), ImageFormat.Gif);
      else if (this._InviewData == frmMain.InviewDataType.SPR)
      {

        L1Spr.Frame[] frameArray = null;
        if(data is byte[])
        {
            frameArray = L1Spr.Load((byte[])data);
        }else if(data is L1Spr.Frame[])
        {
            frameArray = (L1Spr.Frame[])data;
        }
        for (int index = 0; index < frameArray.Length; ++index)
        {
          if (frameArray[index].image != null)
            frameArray[index].image.Save(str.Replace(".spr", string.Format("-{0:D2}(view).bmp", (object) index)), ImageFormat.Bmp);
        }
        File.WriteAllBytes(str, (byte[])origin_bytes);
      }
      else
        File.WriteAllBytes(str, (byte[])origin_bytes);
    }

    private void ExportSelected(string Path, ListViewItem lvItem)
    {
       FileStream fs = File.Open(this._PackFileName.Replace(".idx", ".pak"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      if (this._InviewData == frmMain.InviewDataType.Text)
        this.ExportData(Path, lvItem, (object) this.TextViewer.Text, this.LoadPakBytes_(fs, lvItem));
      else if (this._InviewData == frmMain.InviewDataType.IMG || this._InviewData == frmMain.InviewDataType.BMP)
      {
        this.ExportData(Path, lvItem, (object) this.ImageViewer.Image, this.LoadPakBytes_(fs, lvItem));
      }
      else
      {
        L1PakTools.IndexRecord indexRecord = this._IndexRecords[int.Parse(lvItem.Text) - 1];
        byte[] buffer = new byte[indexRecord.FileSize];
        fs.Seek((long) indexRecord.Offset, SeekOrigin.Begin);
        fs.Read(buffer, 0, indexRecord.FileSize);
        this.ExportData(Path, lvItem, (object) buffer,buffer);
      }
      fs.Close();
    }

    private void mnuTools_Delete_Click(object sender, EventArgs e)
    {
      if (this.lvIndexInfo.SelectedIndices.Count == 0)
      {
        MessageBox.Show("請先選擇要刪除的檔案。", "未選擇檔案", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      if (MessageBox.Show("請先刪除原始 PAK 檔案的舊備份！\n\n確定要刪除 " + this.lvIndexInfo.SelectedIndices.Count + " 個檔案嗎？", "警告！", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
        return;

      // 將選取的虛擬索引轉換為實際索引
      List<int> realIndexes = new List<int>();
      foreach (int virtualIndex in this.lvIndexInfo.SelectedIndices)
      {
        if (this._FilteredIndexes != null && virtualIndex < this._FilteredIndexes.Count)
        {
          realIndexes.Add(this._FilteredIndexes[virtualIndex]);
        }
      }

      int[] DeleteID = realIndexes.ToArray();
      this.RebuildAll(DeleteID);
      this.ShowRecords(this._IndexRecords);
    }

    private void mnuRebuild_Click(object sender, EventArgs e)
    {
      this.RebuildAll(new int[0]);
    }

    private void RebuildAll(int[] DeleteID)
    {
      int length = this._IndexRecords.Length - DeleteID.Length;
      L1PakTools.IndexRecord[] items = new L1PakTools.IndexRecord[length];
      int[] keys = new int[length];
      int index1 = 0;
      int index2 = 0;
      for (; index1 < this._IndexRecords.Length; ++index1)
      {
        if (Array.IndexOf<int>(DeleteID, index1) == -1)
        {
          items[index2] = this._IndexRecords[index1];
          keys[index2] = this._IndexRecords[index1].Offset;
          ++index2;
        }
        else
        {
          // Console.WriteLine("found deleted item:" + index1);
        }
      }
      //Array.Sort<int, L1PakTools.IndexRecord>(keys, items);
      this._IndexRecords = items;
      this.tssProgressName.Text = "正在建立新 PAK 檔案...";
      this.tssProgress.Maximum = length;
      string str1 = this._PackFileName.Replace(".idx", ".pak");
      string str2 = this._PackFileName.Replace(".idx", ".pa_");
      if (File.Exists(str2))
        File.Delete(str2);
      File.Move(str1, str2);
      FileStream fileStream1 = File.OpenRead(str2);
      FileStream fileStream2 = File.OpenWrite(str1);

      // ExtB 格式需要追蹤新的 offset 列表
      var newSortedOffsets = new List<int>();

      for (int index3 = 0; index3 < length; ++index3)
      {
        byte[] buffer;

        if (this._IsExtBFormat)
        {
          // ExtB 格式：讀取壓縮資料
          int compressedSize = GetExtBCompressedSize(this._IndexRecords[index3].Offset);
          if (compressedSize > 0)
          {
            buffer = new byte[compressedSize];
            fileStream1.Seek((long) this._IndexRecords[index3].Offset, SeekOrigin.Begin);
            fileStream1.Read(buffer, 0, compressedSize);
          }
          else
          {
            // fallback: 使用 FileSize
            buffer = new byte[this._IndexRecords[index3].FileSize];
            fileStream1.Seek((long) this._IndexRecords[index3].Offset, SeekOrigin.Begin);
            fileStream1.Read(buffer, 0, this._IndexRecords[index3].FileSize);
          }
        }
        else
        {
          // 一般格式：讀取未壓縮資料
          buffer = new byte[this._IndexRecords[index3].FileSize];
          fileStream1.Seek((long) this._IndexRecords[index3].Offset, SeekOrigin.Begin);
          fileStream1.Read(buffer, 0, this._IndexRecords[index3].FileSize);
        }

        this._IndexRecords[index3].Offset = (int) fileStream2.Position;
        newSortedOffsets.Add(this._IndexRecords[index3].Offset);
        fileStream2.Write(buffer, 0, buffer.Length);
        this.tssProgress.Increment(1);
      }
      fileStream1.Close();
      fileStream2.Close();

      // 更新 ExtB offset 列表
      if (this._IsExtBFormat)
      {
        this._ExtBSortedOffsets = newSortedOffsets;
        this._ExtBSortedOffsets.Sort();
        this._ExtBPakFileSize = new FileInfo(str1).Length;
      }

      this.RebuildIndex();
    }

    private void RebuildIndex()
    {
      string str = this._PackFileName.Replace(".idx", ".id_");
      if (File.Exists(str))
        File.Delete(str);
      File.Move(this._PackFileName, str);

      this.tssProgressName.Text = "正在建立新 IDX 檔案...";
      this.tssProgress.Maximum = this._IndexRecords.Length;

      if (this._IsExtBFormat)
      {
        // ExtB 格式：16 bytes header + 128 bytes per entry
        const int headerSize = 0x10;
        const int entrySize = 0x80;
        byte[] numArray = new byte[headerSize + this._IndexRecords.Length * entrySize];

        // Header: "_EXTB$" + padding
        numArray[0] = (byte)'_';
        numArray[1] = (byte)'E';
        numArray[2] = (byte)'X';
        numArray[3] = (byte)'T';
        numArray[4] = (byte)'B';
        numArray[5] = (byte)'$';
        // bytes 6-15: padding (zeros)

        for (int index = 0; index < this._IndexRecords.Length; ++index)
        {
          int entryOffset = headerSize + index * entrySize;
          var rec = this._IndexRecords[index];

          // Bytes 0-3: Previous compressed size (set to 0, will be recalculated on load)
          int prevCompressedSize = 0;
          if (index > 0)
          {
            // 計算前一個 entry 的壓縮大小
            prevCompressedSize = rec.Offset - this._IndexRecords[index - 1].Offset;
            if (prevCompressedSize < 0) prevCompressedSize = 0;
          }
          Array.Copy(BitConverter.GetBytes(prevCompressedSize), 0, numArray, entryOffset, 4);

          // Bytes 4-7: Compression type
          int compType = 1; // 預設 zlib
          if (this._ExtBCompressionTypes != null && this._ExtBCompressionTypes.TryGetValue(rec.FileName, out int ct))
            compType = ct;
          Array.Copy(BitConverter.GetBytes(compType), 0, numArray, entryOffset + 4, 4);

          // Bytes 8-119: Filename (112 bytes, null-padded)
          byte[] nameBytes = Encoding.ASCII.GetBytes(rec.FileName);
          int nameLen = Math.Min(nameBytes.Length, 112);
          Array.Copy(nameBytes, 0, numArray, entryOffset + 8, nameLen);

          // Bytes 120-123: PAK Offset
          Array.Copy(BitConverter.GetBytes(rec.Offset), 0, numArray, entryOffset + 120, 4);

          // Bytes 124-127: Uncompressed Size
          Array.Copy(BitConverter.GetBytes(rec.FileSize), 0, numArray, entryOffset + 124, 4);

          this.tssProgress.Increment(1);
        }

        File.WriteAllBytes(this._PackFileName, numArray);

        // 更新 _ExtBRawData
        this._ExtBRawData = numArray;
      }
      else
      {
        // 一般格式
        byte[] numArray = new byte[4 + this._IndexRecords.Length * 28];
        Array.Copy((Array) BitConverter.GetBytes(this._IndexRecords.Length), 0, (Array) numArray, 0, 4);

        for (int index = 0; index < this._IndexRecords.Length; ++index)
        {
          int destinationIndex = 4 + index * 28;
          Array.Copy((Array) BitConverter.GetBytes(this._IndexRecords[index].Offset), 0, (Array) numArray, destinationIndex, 4);
          Encoding.Default.GetBytes(this._IndexRecords[index].FileName, 0, this._IndexRecords[index].FileName.Length, numArray, destinationIndex + 4);
          Array.Copy((Array) BitConverter.GetBytes(this._IndexRecords[index].FileSize), 0, (Array) numArray, destinationIndex + 24, 4);
          this.tssProgress.Increment(1);
        }

        if (this._IsPackFileProtected)
        {
          this.tssProgressName.Text = "編碼中...";
          Array.Copy((Array) L1PakTools.Encode(numArray, 4), 0, (Array) numArray, 4, numArray.Length - 4);
        }

        File.WriteAllBytes(this._PackFileName, numArray);
      }

      this.tssProgressName.Text = "";
    }

    private void mnuTools_Update_Click(object sender, EventArgs e)
    {
      if (this._InviewData != frmMain.InviewDataType.Text || this.lvIndexInfo.SelectedItems.Count != 1)
        return;
      byte[] numArray = Encoding.Default.GetBytes(this.TextViewer.Text);
      int uncompressedSize = numArray.Length;

      if (this._IsPackFileProtected)
        numArray = L1PakTools.Encode(numArray, 0);

      IEnumerator enumerator = this.lvIndexInfo.SelectedItems.GetEnumerator();
      try
      {
        if (!enumerator.MoveNext())
          return;
        ListViewItem current = (ListViewItem) enumerator.Current;
        int ID = int.Parse(current.Text) - 1;
        string fileName = this._IndexRecords[ID].FileName;

        // ExtB 格式：壓縮資料
        byte[] dataToWrite = numArray;
        if (this._IsExtBFormat)
        {
          int compType = 1; // 預設 zlib
          if (this._ExtBCompressionTypes != null && this._ExtBCompressionTypes.TryGetValue(fileName, out int ct))
            compType = ct;
          dataToWrite = CompressExtBData(numArray, compType);
        }

        FileStream fileStream1 = File.OpenWrite(this._PackFileName.Replace(".idx", ".pak"));
        this._IndexRecords[ID].Offset = (int) fileStream1.Seek(0L, SeekOrigin.End);
        this._IndexRecords[ID].FileSize = uncompressedSize;
        fileStream1.Write(dataToWrite, 0, dataToWrite.Length);
        fileStream1.Close();

        // 更新 ExtB offset 列表
        if (this._IsExtBFormat)
        {
          this._ExtBSortedOffsets.Add(this._IndexRecords[ID].Offset);
          this._ExtBSortedOffsets.Sort();
          this._ExtBPakFileSize = new FileInfo(this._PackFileName.Replace(".idx", ".pak")).Length;
        }

        if (this._IsPackFileProtected || this._IsExtBFormat)
        {
          this.RebuildIndex();
        }
        else
        {
          FileStream fileStream2 = File.OpenWrite(this._PackFileName);
          fileStream2.Seek((long) (4 + ID * 28), SeekOrigin.Begin);
          fileStream2.Write(BitConverter.GetBytes(this._IndexRecords[ID].Offset), 0, 4);
          fileStream2.Close();
        }
        this.lvIndexInfo.Items.Insert(current.Index, this.CreatListViewItem(ID, this._IndexRecords[ID]));
        this.lvIndexInfo.Items.Remove(current);
      }
      finally
      {
        IDisposable disposable = enumerator as IDisposable;
        if (disposable != null)
          disposable.Dispose();
      }
    }

    private void tssProgressName_TextChanged(object sender, EventArgs e)
    {
      if (this.tssProgressName.Text != "")
      {
        this.tssProgressName.Visible = true;
        this.tssProgress.Visible = true;
        this.tssProgress.Value = 0;
      }
      else
      {
        this.tssProgressName.Visible = false;
        this.tssProgress.Visible = false;
      }
      this.statusStrip1.Refresh();
    }

    private void mnuTools_Add_Click(object sender, EventArgs e)
    {
      this.dlgAddFiles.Filter = "All files (*.*)|*.*";
      this.dlgAddFiles.FileName = "";
      if (this.dlgAddFiles.ShowDialog((IWin32Window) this) != DialogResult.OK)
        return;

      // 警示：將會重建索引檔案
      if (MessageBox.Show(
        "這將會新增檔案到 PAK 並重建索引檔案。\n\n" +
        "請先備份您的原始 PAK 檔案！\n\n" +
        "要繼續嗎？",
        "新增檔案 - 警告",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning) == DialogResult.No)
        return;

      // 檢查重複檔案
      List<string> duplicateFiles = new List<string>();
      List<string> newFiles = new List<string>();
      foreach (string fileName in this.dlgAddFiles.FileNames)
      {
        string filename = fileName.Substring(fileName.LastIndexOf('\\') + 1);
        bool exists = false;
        for (int i = 0; i < this._IndexRecords.Length; ++i)
        {
          if (this._IndexRecords[i].FileName.Equals(filename, StringComparison.OrdinalIgnoreCase))
          {
            exists = true;
            duplicateFiles.Add(filename);
            break;
          }
        }
        if (!exists)
          newFiles.Add(filename);
      }

      // 顯示重複檔案資訊
      if (duplicateFiles.Count > 0)
      {
        string message = string.Format(
          "發現 {0} 個重複檔案將被取代：\n\n{1}\n\n" +
          "新增檔案數：{2}\n\n" +
          "注意：舊檔案資料將保留在 PAK 中（浪費空間）。\n" +
          "建議新增後使用「重建」功能清理。\n\n" +
          "要繼續嗎？",
          duplicateFiles.Count,
          string.Join("\n", duplicateFiles.Take(5)) + (duplicateFiles.Count > 5 ? "\n..." : ""),
          newFiles.Count);

        if (MessageBox.Show(message, "發現重複檔案", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
        {
          return;
        }
      }

      FileStream fileStream1 = File.OpenWrite(this._PackFileName.Replace(".idx", ".pak"));
      FileStream fileStream2 = File.OpenWrite(this._PackFileName);
      int addedCount = 0;
      int replacedCount = 0;
      foreach (string fileName in this.dlgAddFiles.FileNames)
      {
        byte[] numArray = File.ReadAllBytes(fileName);
        int uncompressedSize = numArray.Length;
        string filename = Path.GetFileName(fileName);

        if (this._IsPackFileProtected)
        {
          this.tssProgressName.Text = string.Format("{0} Encoding...", filename);
          numArray = L1PakTools.Encode(numArray, 0);
          this.tssProgressName.Text = "";
        }

        // ExtB 格式：壓縮資料
        byte[] dataToWrite = numArray;
        if (this._IsExtBFormat)
        {
          // 新增檔案時決定壓縮類型
          int compType = 1; // 預設 zlib
          // 如果是取代檔案，使用原本的壓縮類型
          if (this._ExtBCompressionTypes != null && this._ExtBCompressionTypes.TryGetValue(filename, out int ct))
            compType = ct;
          else
          {
            // 新檔案：根據檔名決定壓縮類型
            // .til 檔案通常使用 zlib 或 brotli，這裡預設 zlib
            if (this._ExtBCompressionTypes == null)
              this._ExtBCompressionTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this._ExtBCompressionTypes[filename] = compType;
          }
          dataToWrite = CompressExtBData(numArray, compType);
        }

        int offset = (int) fileStream1.Seek(0L, SeekOrigin.End);
        fileStream1.Write(dataToWrite, 0, dataToWrite.Length);

        bool replace = false;
        int index = -1;
        for (int i = 0; i < this._IndexRecords.Length; ++i)
        {
            L1PakTools.IndexRecord record = this._IndexRecords[i];
            if (record.FileName.Equals(filename, StringComparison.OrdinalIgnoreCase))
            {
                replace = true;
                index = i;
            }
        }

        L1PakTools.IndexRecord record2 = new L1PakTools.IndexRecord(filename, uncompressedSize, offset);

        if (replace)
        {
             this._IndexRecords[index] = record2;
             replacedCount++;
        }
        else
        {
            Array.Resize<L1PakTools.IndexRecord>(ref this._IndexRecords, this._IndexRecords.Length + 1);
            this._IndexRecords[this._IndexRecords.Length -1] = record2;
            addedCount++;
        }
         this.ShowRecords(this._IndexRecords);
      }

      fileStream1.Close();

      // 更新 ExtB offset 列表
      if (this._IsExtBFormat)
      {
        this._ExtBSortedOffsets = new List<int>();
        foreach (var rec in this._IndexRecords)
          this._ExtBSortedOffsets.Add(rec.Offset);
        this._ExtBSortedOffsets.Sort();
        this._ExtBPakFileSize = new FileInfo(this._PackFileName.Replace(".idx", ".pak")).Length;
      }

      if (this._IsPackFileProtected || this._IsExtBFormat)
      {
        fileStream2.Close();
      }
      else
      {
        fileStream2.Seek(0L, SeekOrigin.Begin);
        fileStream2.Write(BitConverter.GetBytes(this._IndexRecords.Length), 0, 4);
        fileStream2.Close();
      }
      this.RebuildIndex();

      // 顯示完成訊息
      string summary = string.Format(
        "新增檔案完成！\n\n" +
        "新增檔案數：{0}\n" +
        "取代檔案數：{1}\n" +
        "PAK 總檔案數：{2}\n\n" +
        "{3}",
        addedCount,
        replacedCount,
        this._IndexRecords.Length,
        replacedCount > 0 ? "提示：使用「工具 > 重建」來清除被取代檔案的浪費空間。" : "");

      MessageBox.Show(summary, "新增檔案完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void txtSearch_KeyPress(object sender, KeyPressEventArgs e)
    {
      if ((int) e.KeyChar != 13)
        return;
      TextBox textBox = (TextBox) sender;

      // Sprite 模式下篩選 SpriteId
      if (this._IsSpriteMode && this._SpriteGroups != null)
      {
        FilterSpriteGroups(textBox.Text);
        return;
      }

      // DAT 模式下篩選
      if (this._IsDatMode && this._AllDatEntries != null)
      {
        FilterDatEntries(textBox.Text);
        return;
      }

      if (this._IndexRecords == null || this._IndexRecords.Length <= 0)
        return;

      // 使用 VirtualMode
      this._FilteredIndexes = new List<int>(this._IndexRecords.Length);
      for (int ID = 0; ID < this._IndexRecords.Length; ++ID)
      {
        L1PakTools.IndexRecord indexRecord = this._IndexRecords[ID];
        if ((textBox.Text != "") )
        {
            if (textBox.Text.StartsWith("^")) {
                if(indexRecord.FileName.IndexOf(textBox.Text.Substring(1), StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    this._FilteredIndexes.Add(ID);
                }
            }
            else if (indexRecord.FileName.IndexOf(textBox.Text, StringComparison.CurrentCultureIgnoreCase) != -1){
                this._FilteredIndexes.Add(ID);
            }
        }
        else
        {
            this._FilteredIndexes.Add(ID);
        }
      }
      this.lvIndexInfo.VirtualListSize = this._FilteredIndexes.Count;
      this.lvIndexInfo.Invalidate();
      if (this._FilteredIndexes.Count > 0)
      {
        this.lvIndexInfo.SelectedIndices.Clear();
        this.lvIndexInfo.SelectedIndices.Add(0);
      }
      this.lvIndexInfo.Focus();
      this.tssRecordCount.Text = string.Format("全部：{0}", (object) this._IndexRecords.Length);
      this.tssShowInListView.Text = string.Format("顯示：{0}", (object) this._FilteredIndexes.Count);
    }

    private void FilterSpriteGroups(string searchText)
    {
      // 重建顯示列表，只包含符合條件的群組
      this._SpriteDisplayItems = new List<object>();

      foreach (var group in this._SpriteGroups)
      {
        // 先檢查類型過濾
        if (!MatchesTypeFilter(group))
          continue;

        // SpriteId 篩選
        string prefix = group.Prefix.TrimEnd('-');
        if (!string.IsNullOrEmpty(searchText))
        {
          // 支援 ^ 前綴匹配
          if (searchText.StartsWith("^"))
          {
            string pattern = searchText.Substring(1);
            if (!prefix.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
              continue;
          }
          else
          {
            // 一般包含匹配
            if (prefix.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) == -1)
              continue;
          }
        }

        this._SpriteDisplayItems.Add(group);
      }

      // 更新 ListView
      this.lvIndexInfo.VirtualListSize = this._SpriteDisplayItems.Count;
      this.lvIndexInfo.Invalidate();

      if (this._SpriteDisplayItems.Count > 0)
      {
        this.lvIndexInfo.SelectedIndices.Clear();
        this.lvIndexInfo.SelectedIndices.Add(0);
      }

      this.lvIndexInfo.Focus();

      // 更新狀態列
      int totalGroups = this._SpriteGroups?.Count ?? 0;
      this.tssRecordCount.Text = $"全部：{totalGroups} 組";
      this.tssShowInListView.Text = $"顯示：{this._SpriteDisplayItems.Count} 組";
    }

    private void btnContentSearch_Click(object sender, EventArgs e)
    {
      this.SearchContent();
    }

    private void txtContentSearch_KeyPress(object sender, KeyPressEventArgs e)
    {
      if ((int) e.KeyChar == 13)
      {
        e.Handled = true;
        this.SearchContent();
      }
    }

    private void SearchContent()
    {
      string searchText = this.txtContentSearch.Text;
      if (string.IsNullOrEmpty(searchText) || this._IndexRecords == null || this._IndexRecords.Length == 0)
        return;

      // 如果正在搜尋，先取消
      if (this.bgSearchWorker.IsBusy)
      {
        this.bgSearchWorker.CancelAsync();
        return;
      }

      string pakFile = this._PackFileName.Replace(".idx", ".pak");
      if (!File.Exists(pakFile))
      {
        this.tssMessage.Text = "找不到 PAK 檔案";
        return;
      }

      // 禁用搜尋按鈕，改變文字
      this.btnContentSearch.Text = "停止";
      this.btnClearSearch.Enabled = false;
      this.txtContentSearch.Enabled = false;
      this.lvIndexInfo.Enabled = false;  // 禁用清單避免選取衝突
      this.tssMessage.Text = "搜尋中...";

      // 清空左邊清單
      this._FilteredIndexes = new List<int>();
      this._CheckedIndexes.Clear();
      this.lvIndexInfo.VirtualListSize = 0;
      this.lvIndexInfo.Invalidate();
      this.tssShowInListView.Text = "顯示：0";
      this.tssCheckedCount.Text = "已選：0";

      // 傳遞搜尋參數給背景執行緒
      var searchParams = new SearchParams
      {
        SearchText = searchText,
        PakFile = pakFile,
        IndexRecords = this._IndexRecords,
        IsProtected = this._IsPackFileProtected
      };

      this.bgSearchWorker.RunWorkerAsync(searchParams);
    }

    private class SearchParams
    {
      public string SearchText;
      public string PakFile;
      public L1PakTools.IndexRecord[] IndexRecords;
      public bool IsProtected;
    }

    private void bgSearchWorker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
    {
      var worker = (System.ComponentModel.BackgroundWorker)sender;
      var searchParams = (SearchParams)e.Argument;

      // 暫時停用 progressbar，避免多線程衝突
      L1PakTools.ShowProgress(null);

      // 篩選出需要搜尋的文字類檔案
      var textFileIndexes = new List<int>();
      for (int i = 0; i < searchParams.IndexRecords.Length; i++)
      {
        string ext = Path.GetExtension(searchParams.IndexRecords[i].FileName).ToLower();
        if (ext == ".html" || ext == ".tbl" || ext == ".h" || ext == ".ht" || ext == ".htm" || ext == ".txt" || ext == ".def" || ext == ".til" || ext == ".xml")
        {
          textFileIndexes.Add(i);
        }
      }

      int totalItems = textFileIndexes.Count;
      int processed = 0;
      var foundResults = new ConcurrentBag<int>();
      var encoding = Encoding.GetEncoding("big5");

      // 使用 SafeFileHandle 進行 RandomAccess 平行讀取
      using (var fileHandle = File.OpenHandle(searchParams.PakFile, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous))
      {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        Parallel.ForEach(textFileIndexes, parallelOptions, (realIndex, state) =>
        {
          if (worker.CancellationPending)
          {
            state.Stop();
            return;
          }

          L1PakTools.IndexRecord record = searchParams.IndexRecords[realIndex];

          // 使用 RandomAccess 讀取指定位置
          byte[] data = new byte[record.FileSize];
          RandomAccess.Read(fileHandle, data, record.Offset);

          if (searchParams.IsProtected)
            data = L1PakTools.Decode(data, 0);

          // XML 檔案需要解密
          string ext = Path.GetExtension(record.FileName).ToLower();
          if (ext == ".xml" && XmlCracker.IsEncrypted(data))
          {
            data = XmlCracker.Decrypt(data);
          }

          // 對 XML 使用正確的編碼，其他檔案使用 big5
          Encoding fileEncoding;
          if (ext == ".xml")
          {
            fileEncoding = XmlCracker.GetXmlEncoding(data, record.FileName);
          }
          else
          {
            fileEncoding = encoding;
          }

          string content = fileEncoding.GetString(data);
          if (content.IndexOf(searchParams.SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
          {
            foundResults.Add(realIndex);
            // 找到就回報
            worker.ReportProgress(realIndex, new SearchProgress { FoundIndex = realIndex, Current = processed, Total = totalItems, FoundCount = foundResults.Count, Phase = "Searching" });
          }

          int currentProcessed = System.Threading.Interlocked.Increment(ref processed);

          // 每處理一定數量回報進度
          if (currentProcessed % 50 == 0)
          {
            worker.ReportProgress(-1, new SearchProgress { FoundIndex = -1, Current = currentProcessed, Total = totalItems, FoundCount = foundResults.Count, Phase = "Searching" });
          }
        });
      }

      e.Result = new SearchResult { SearchText = searchParams.SearchText, FoundCount = foundResults.Count };
    }

    private class SearchProgress
    {
      public int FoundIndex;
      public int Current;
      public int Total;
      public int FoundCount;
      public string Phase;
    }

    private class SearchResult
    {
      public string SearchText;
      public int FoundCount;
    }

    private void bgSearchWorker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
    {
      var progress = (SearchProgress)e.UserState;

      // 如果有找到新結果，加入清單
      if (progress.FoundIndex >= 0)
      {
        this._FilteredIndexes.Add(progress.FoundIndex);
        // 先更新 VirtualListSize 再讓 ListView 知道
        int count = this._FilteredIndexes.Count;
        if (this.lvIndexInfo.VirtualListSize != count)
        {
          this.lvIndexInfo.VirtualListSize = count;
        }
        this.tssShowInListView.Text = string.Format("顯示：{0}", count);
      }

      this.tssMessage.Text = string.Format("{0}... {1}/{2} (Found: {3})", progress.Phase ?? "Searching", progress.Current, progress.Total, progress.FoundCount);
    }

    private void bgSearchWorker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
    {
      // 還原 UI 狀態
      this.btnContentSearch.Text = "搜尋";
      this.btnClearSearch.Enabled = true;
      this.txtContentSearch.Enabled = true;
      this.lvIndexInfo.Enabled = true;  // 重新啟用清單

      if (e.Cancelled)
      {
        this.tssMessage.Text = string.Format("Search cancelled (Found: {0})", this._FilteredIndexes.Count);
        return;
      }

      if (e.Error != null)
      {
        this.tssMessage.Text = "Search error: " + e.Error.Message;
        return;
      }

      var result = (SearchResult)e.Result;

      this.tssRecordCount.Text = string.Format("全部：{0}", (object) this._IndexRecords.Length);
      this.tssShowInListView.Text = string.Format("顯示：{0}", (object) this._FilteredIndexes.Count);
      this.tssCheckedCount.Text = string.Format("已選：{0}", (object) this._CheckedIndexes.Count);

      if (this._FilteredIndexes.Count > 0)
      {
        // 搜尋完成後選取第一筆結果
        this.lvIndexInfo.SelectedIndices.Clear();
        this.lvIndexInfo.SelectedIndices.Add(0);
        this.lvIndexInfo.EnsureVisible(0);
        this.tssMessage.Text = string.Format("Found {0} files containing \"{1}\"", result.FoundCount, result.SearchText);
      }
      else
      {
        this.tssMessage.Text = "找不到符合的項目";
      }
    }

    private void btnClearSearch_Click(object sender, EventArgs e)
    {
      this.txtContentSearch.Text = "";
      if (this._IndexRecords != null)
      {
        this.ShowRecords(this._IndexRecords);
        this.tssMessage.Text = "已清除搜尋";
      }
    }

    private void cmbExtFilter_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this._IndexRecords != null)
      {
        this.ShowRecords(this._IndexRecords);
      }
    }

    private void cmbLangFilter_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this._IndexRecords != null)
      {
        this.ShowRecords(this._IndexRecords);
      }
    }

    private void chkSkipSaveConfirm_CheckedChanged(object sender, EventArgs e)
    {
      Settings.Default.SkipSaveConfirmation = this.chkSkipSaveConfirm.Checked;
      Settings.Default.Save();
    }

    private void TextViewer_TextChanged(object sender, EventArgs e)
    {
      if (this._CurrentEditingRealIndex >= 0 && this._InviewData == frmMain.InviewDataType.Text)
      {
        this._TextModified = true;
        this.btnSaveText.Enabled = true;
        this.btnCancelEdit.Enabled = true;
      }
    }

    private void TextViewer_KeyDown(object sender, KeyEventArgs e)
    {
      // Ctrl+F: 開始搜尋
      if (e.Control && e.KeyCode == Keys.F)
      {
        e.SuppressKeyPress = true;
        ShowTextSearchDialog();
      }
      // F3: 找下一個
      else if (e.KeyCode == Keys.F3)
      {
        e.SuppressKeyPress = true;
        if (e.Shift)
          FindTextPrevious();
        else
          FindTextNext();
      }
      // Esc: 取消搜尋高亮
      else if (e.KeyCode == Keys.Escape)
      {
        this._TextSearchKeyword = "";
        this._TextSearchLastIndex = 0;
      }
    }

    private void ShowTextSearchDialog()
    {
      string input = Microsoft.VisualBasic.Interaction.InputBox(
        "請輸入要搜尋的文字：",
        "搜尋 (F3=下一個, Shift+F3=上一個)",
        this._TextSearchKeyword);

      if (!string.IsNullOrEmpty(input))
      {
        this._TextSearchKeyword = input;
        this._TextSearchLastIndex = this.TextViewer.SelectionStart;
        FindTextNext();
      }
    }

    private void FindTextNext()
    {
      if (string.IsNullOrEmpty(this._TextSearchKeyword))
      {
        ShowTextSearchDialog();
        return;
      }

      int startIndex = this._TextSearchLastIndex;
      int foundIndex = this.TextViewer.Text.IndexOf(
        this._TextSearchKeyword,
        startIndex,
        StringComparison.CurrentCultureIgnoreCase);

      if (foundIndex < 0 && startIndex > 0)
      {
        // 從頭開始搜尋
        foundIndex = this.TextViewer.Text.IndexOf(
          this._TextSearchKeyword,
          0,
          StringComparison.CurrentCultureIgnoreCase);
        if (foundIndex >= 0)
          this.tssMessage.Text = "搜尋已從頭開始";
      }

      if (foundIndex >= 0)
      {
        this.TextViewer.Select(foundIndex, this._TextSearchKeyword.Length);
        this.TextViewer.ScrollToCaret();
        this._TextSearchLastIndex = foundIndex + 1;
        this.tssMessage.Text = $"找到: \"{this._TextSearchKeyword}\" (F3=下一個)";
      }
      else
      {
        this.tssMessage.Text = $"找不到: \"{this._TextSearchKeyword}\"";
        this._TextSearchLastIndex = 0;
      }
    }

    private void FindTextPrevious()
    {
      if (string.IsNullOrEmpty(this._TextSearchKeyword))
      {
        ShowTextSearchDialog();
        return;
      }

      int endIndex = this._TextSearchLastIndex - 1;
      if (endIndex <= 0) endIndex = this.TextViewer.Text.Length;

      int foundIndex = this.TextViewer.Text.LastIndexOf(
        this._TextSearchKeyword,
        endIndex,
        StringComparison.CurrentCultureIgnoreCase);

      if (foundIndex < 0 && endIndex < this.TextViewer.Text.Length)
      {
        // 從尾端開始搜尋
        foundIndex = this.TextViewer.Text.LastIndexOf(
          this._TextSearchKeyword,
          this.TextViewer.Text.Length - 1,
          StringComparison.CurrentCultureIgnoreCase);
        if (foundIndex >= 0)
          this.tssMessage.Text = "搜尋已從尾端開始";
      }

      if (foundIndex >= 0)
      {
        this.TextViewer.Select(foundIndex, this._TextSearchKeyword.Length);
        this.TextViewer.ScrollToCaret();
        this._TextSearchLastIndex = foundIndex;
        this.tssMessage.Text = $"找到: \"{this._TextSearchKeyword}\" (Shift+F3=上一個)";
      }
      else
      {
        this.tssMessage.Text = $"找不到: \"{this._TextSearchKeyword}\"";
        this._TextSearchLastIndex = this.TextViewer.Text.Length;
      }
    }

    private Encoding GetEncodingForFile(string fileName)
    {
      string fileNameLower = fileName.ToLower();
      if (fileNameLower.IndexOf("-k.") >= 0)
        return Encoding.GetEncoding("euc-kr");
      else if (fileNameLower.IndexOf("-j.") >= 0)
        return Encoding.GetEncoding("shift_jis");
      else if (fileNameLower.IndexOf("-h.") >= 0)
        return Encoding.GetEncoding("gb2312");
      else
        return Encoding.GetEncoding("big5");
    }

    private void btnSaveText_Click(object sender, EventArgs e)
    {
      if (this._CurrentEditingRealIndex < 0 || !this._TextModified)
        return;

      L1PakTools.IndexRecord record = this._IndexRecords[this._CurrentEditingRealIndex];

      // 確認對話框
      if (!Settings.Default.SkipSaveConfirmation)
      {
        string encryptionNote = this._IsCurrentFileXmlEncrypted ? "\n（將以 XML 加密方式儲存）" : "";
        DialogResult result = MessageBox.Show(
          "儲存對 \"" + record.FileName + "\" 的變更嗎？\n\n這將會直接修改 PAK 檔案。" + encryptionNote,
          "確認儲存",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
          return;
      }

      try
      {
        string pakFile = this._PackFileName.Replace(".idx", ".pak");

        // 對 XML 檔案使用儲存時的 encoding（從 XML 聲明取得），其他檔案使用檔名判斷
        Encoding encoding;
        bool isXmlFile = Path.GetExtension(record.FileName).ToLower() == ".xml";
        if (isXmlFile && this._CurrentXmlEncoding != null)
        {
          encoding = this._CurrentXmlEncoding;
        }
        else
        {
          encoding = this.GetEncodingForFile(record.FileName);
        }

        // TextBox 會把 \r\n 轉成 \n，需要轉回 \r\n 以保持原始格式
        string textToSave = this.TextViewer.Text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        byte[] newData = encoding.GetBytes(textToSave);

        // 如果是 XML 加密的檔案，需要先加密
        if (this._IsCurrentFileXmlEncrypted)
        {
          newData = XmlCracker.Encrypt(newData);
        }

        // 如果是 PAK 加密的檔案，需要先編碼
        if (this._IsPackFileProtected)
        {
          newData = L1PakTools.Encode(newData, 0);
        }

        // 檢查新資料大小是否與原始大小相同
        if (newData.Length != record.FileSize)
        {
          // 大小改變，需要重建 PAK 和 IDX
          DialogResult confirmRebuild = MessageBox.Show(
            "檔案大小已變更（" + record.FileSize + " -> " + newData.Length + " 位元組）。\n\n" +
            "這需要重建 PAK 和 IDX 檔案。\n" +
            "修改前將會建立備份。\n\n" +
            "要繼續嗎？",
            "大小變更 - 需要重建",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

          if (confirmRebuild != DialogResult.Yes)
            return;

          // 呼叫重建功能
          string error = PakReader.RebuildPakWithNewSizeCore(
            this._PackFileName,
            pakFile,
            this._IndexRecords,
            this._CurrentEditingRealIndex,
            newData,
            this._IsPackFileProtected);

          if (error != null)
          {
            MessageBox.Show("重建 PAK 時發生錯誤：" + error, "儲存錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
          }

          this._TextModified = false;
          this.btnSaveText.Enabled = false;
          this.btnCancelEdit.Enabled = false;
          this.tssMessage.Text = "Saved (rebuilt): " + record.FileName + " [Size: " + record.FileSize + " -> " + newData.Length + "]";
        }
        else
        {
          // 大小相同，直接寫入
          using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Write, FileShare.Read))
          {
            fs.Seek(record.Offset, SeekOrigin.Begin);
            fs.Write(newData, 0, newData.Length);
          }

          this._TextModified = false;
          this.btnSaveText.Enabled = false;
          this.btnCancelEdit.Enabled = false;
          this.tssMessage.Text = "Saved: " + record.FileName + (this._IsCurrentFileXmlEncrypted ? " [XML Encrypted]" : "");
        }
      }
      catch (IOException ex)
      {
        MessageBox.Show(
          "無法寫入檔案，檔案可能正被其他程式使用中。\n\n" +
          "請先關閉天堂遊戲或其他編輯器後再試一次。\n\n" +
          "Error: " + ex.Message,
          "檔案鎖定 - Save Error",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      }
      catch (Exception ex)
      {
        MessageBox.Show("儲存檔案時發生錯誤：" + ex.Message, "儲存錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void btnCancelEdit_Click(object sender, EventArgs e)
    {
      if (this._CurrentEditingRealIndex < 0)
        return;

      L1PakTools.IndexRecord record = this._IndexRecords[this._CurrentEditingRealIndex];

      // 重新載入檔案內容
      try
      {
        string pakFile = this._PackFileName.Replace(".idx", ".pak");
        using (FileStream fs = File.Open(pakFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
          // 暫時移除 TextChanged 事件避免觸發
          this.TextViewer.TextChanged -= new EventHandler(this.TextViewer_TextChanged);

          object obj = this.LoadPakData_(fs, record);
          if (obj is string)
            this.TextViewer.Text = (string) obj;
          else if (obj is byte[])
            this.TextViewer.Text = Encoding.GetEncoding("big5").GetString((byte[]) obj);

          // 重新綁定 TextChanged 事件
          this.TextViewer.TextChanged += new EventHandler(this.TextViewer_TextChanged);
        }

        this._TextModified = false;
        this.btnSaveText.Enabled = false;
        this.btnCancelEdit.Enabled = false;
        this.tssMessage.Text = "已取消編輯: " + record.FileName;
      }
      catch (Exception ex)
      {
        MessageBox.Show("重新載入檔案時發生錯誤：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void tsmCompare_Click(object sender, EventArgs e)
    {
      ToolStripMenuItem toolStripMenuItem = (ToolStripMenuItem) sender;
      this.TextCompViewer.Visible = true;
      this.TextCompViewer.SourceText = this.TextViewer.Text;
      FileStream fs = File.Open(this._PackFileName.Replace(".idx", ".pak"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      this.TextCompViewer.TargetText = (string) this.LoadPakData_(fs, (L1PakTools.IndexRecord) toolStripMenuItem.Tag);
      fs.Close();
    }

    private void tsmCompare_DropDownOpening(object sender, EventArgs e)
    {
      string fileName = this._IndexRecords[int.Parse(this.TextViewer.Tag.ToString()) - 1].FileName;
      int num1 = fileName.LastIndexOf('-');
      this.tsmCompTW.Enabled = false;
      this.tsmCompHK.Enabled = false;
      this.tsmCompJP.Enabled = false;
      this.tsmCompKO.Enabled = false;
      if (num1 < 0)
        return;
      string str1 = fileName.Substring(num1, 2);
      foreach (L1PakTools.IndexRecord indexRecord in this._IndexRecords)
      {
        int num2 = indexRecord.FileName.LastIndexOf('-');
        if (num2 >= 0)
        {
          string str2 = indexRecord.FileName.Substring(num2, 2);
          if (indexRecord.FileName.Substring(0, num2) == fileName.Substring(0, num1))
          {
            ToolStripMenuItem toolStripMenuItem = (ToolStripMenuItem) null;
            if (str2 == "-c" && str1 != str2)
              toolStripMenuItem = this.tsmCompTW;
            if (str2 == "-h" && str1 != str2)
              toolStripMenuItem = this.tsmCompHK;
            if (str2 == "-j" && str1 != str2)
              toolStripMenuItem = this.tsmCompJP;
            if (str2 == "-k" && str1 != str2)
              toolStripMenuItem = this.tsmCompKO;
            if (toolStripMenuItem != null)
            {
              toolStripMenuItem.Enabled = true;
              toolStripMenuItem.Tag = (object) indexRecord;
            }
          }
        }
      }
    }

    private void ctxMenu_Opening(object sender, CancelEventArgs e)
    {
      this.tsmCompare.Enabled = this._InviewData == frmMain.InviewDataType.Text;
      // Sprite 模式下也允許刪除功能
      this.tsmDelete.Enabled = true;

      // 壓縮 PNG 選單項目：只在有選取 .png 檔案時顯示
      bool hasPngSelected = false;
      if (this._IndexRecords != null && this.lvIndexInfo.SelectedIndices.Count > 0)
      {
        foreach (int idx in this.lvIndexInfo.SelectedIndices)
        {
          int realIndex = this._IsSpriteMode && this._SpriteDisplayItems != null
            ? (idx < this._SpriteDisplayItems.Count && this._SpriteDisplayItems[idx] is int ri ? ri : -1)
            : (this._FilteredIndexes != null && idx < this._FilteredIndexes.Count ? this._FilteredIndexes[idx] : -1);

          if (realIndex >= 0 && realIndex < this._IndexRecords.Length)
          {
            string fileName = this._IndexRecords[realIndex].FileName;
            if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
              hasPngSelected = true;
              break;
            }
          }
        }
      }
      this.tsmOptimizePng.Visible = hasPngSelected;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.components = (IContainer) new Container();
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (frmMain));
      this.menuStrip1 = new MenuStrip();
      this.mnuFile = new ToolStripMenuItem();
      this.mnuOpen = new ToolStripMenuItem();
      this.mnuOpenSprList = new ToolStripMenuItem();
      this.toolStripSeparator1 = new ToolStripSeparator();
      this.mnuCreatResource = new ToolStripMenuItem();
      this.mnuRebuild = new ToolStripMenuItem();
      this.toolStripSeparator2 = new ToolStripSeparator();
      this.mnuQuit = new ToolStripMenuItem();
      this.mnuEdit = new ToolStripMenuItem();
      this.mnuFiller = new ToolStripMenuItem();
      this.mnuFiller_Text_html = new ToolStripMenuItem();
      this.mnuFiller_Sprite_spr = new ToolStripMenuItem();
      this.mnuFiller_Tile_til = new ToolStripMenuItem();
      this.toolStripSeparator5 = new ToolStripSeparator();
      this.mnuFiller_Text_C = new ToolStripMenuItem();
      this.mnuFiller_Text_H = new ToolStripMenuItem();
      this.mnuFiller_Text_J = new ToolStripMenuItem();
      this.mnuFiller_Text_K = new ToolStripMenuItem();
      this.toolStripSeparator7 = new ToolStripSeparator();
      this.mnuFiller_Sprite_img = new ToolStripMenuItem();
      this.mnuFiller_Sprite_png = new ToolStripMenuItem();
      this.mnuFiller_Sprite_tbt = new ToolStripMenuItem();
      this.toolStripSeparator4 = new ToolStripSeparator();
      this.mnuLanguage = new ToolStripMenuItem();
      this.mnuLanguage_TW = new ToolStripMenuItem();
      this.mnuLanguage_EN = new ToolStripMenuItem();
      this.mnuTools = new ToolStripMenuItem();
      this.mnuTools_Export = new ToolStripMenuItem();
      this.mnuTools_ExportTo = new ToolStripMenuItem();
      this.mnuTools_Delete = new ToolStripMenuItem();
      this.toolStripSeparator8 = new ToolStripSeparator();
      this.mnuTools_Add = new ToolStripMenuItem();
      this.mnuTools_Update = new ToolStripMenuItem();
      this.toolStripSeparator9 = new ToolStripSeparator();
      this.mnuTools_ClearSelect = new ToolStripMenuItem();
      this.mnuTools_SelectAll = new ToolStripMenuItem();
      this.dlgOpenFile = new OpenFileDialog();
      this.splitContainer1 = new SplitContainer();
      this.splitContainer2 = new SplitContainer();
      this.palSearch = new Panel();
      this.label1 = new Label();
      this.txtSearch = new TextBox();
      this.lvIndexInfo = new ListView();
      this.TextCompViewer = new ucTextCompare();
      this.TextViewer = new RichTextBox();
      this.ImageViewer = new ucImgViewer();
      this.SprViewer = new ucSprViewer();
      this.SprListViewer = new ucSprListViewer();
      this.dlgOpenFolder = new FolderBrowserDialog();
      this.ctxMenu = new ContextMenuStrip(this.components);
      this.tsmExport = new ToolStripMenuItem();
      this.tsmExportTo = new ToolStripMenuItem();
      this.tsmCopyFileName = new ToolStripMenuItem();
      this.toolStripSeparator6 = new ToolStripSeparator();
      this.tsmUnselectAll = new ToolStripMenuItem();
      this.tsmSelectAll = new ToolStripMenuItem();
      this.toolStripSeparator10 = new ToolStripSeparator();
      this.tsmDelete = new ToolStripMenuItem();
      this.toolStripSeparator3 = new ToolStripSeparator();
      this.tsmCompare = new ToolStripMenuItem();
      this.tsmCompTW = new ToolStripMenuItem();
      this.tsmCompHK = new ToolStripMenuItem();
      this.tsmCompJP = new ToolStripMenuItem();
      this.tsmCompKO = new ToolStripMenuItem();
      this.statusStrip1 = new StatusStrip();
      this.tssLocker = new ToolStripStatusLabel();
      this.tssRecordCount = new ToolStripStatusLabel();
      this.tssShowInListView = new ToolStripStatusLabel();
      this.tssCheckedCount = new ToolStripStatusLabel();
      this.tssMessage = new ToolStripStatusLabel();
      this.tssProgressName = new ToolStripStatusLabel();
      this.tssProgress = new ToolStripProgressBar();
      this.dlgAddFiles = new OpenFileDialog();
      this.cmbIdxFiles = new ComboBox();
      this.chkSpriteMode = new CheckBox();
      this.chkSprListMode = new CheckBox();
      this.lblFolder = new Label();
      this.palToolbar = new Panel();
      this.palContentSearch = new Panel();
      this.lblContentSearch = new Label();
      this.txtContentSearch = new TextBox();
      this.btnContentSearch = new Button();
      this.btnClearSearch = new Button();
      this.lblExtFilter = new Label();
      this.cmbExtFilter = new ComboBox();
      this.lblLangFilter = new Label();
      this.cmbLangFilter = new ComboBox();
      this.btnSaveText = new Button();
      this.btnCancelEdit = new Button();
      this.chkSkipSaveConfirm = new CheckBox();
      this.palToolbar.SuspendLayout();
      this.palContentSearch.SuspendLayout();
      this.menuStrip1.SuspendLayout();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      this.splitContainer2.Panel1.SuspendLayout();
      this.splitContainer2.Panel2.SuspendLayout();
      this.splitContainer2.SuspendLayout();
      this.palSearch.SuspendLayout();
      this.ctxMenu.SuspendLayout();
      this.statusStrip1.SuspendLayout();
      this.SuspendLayout();
      this.menuStrip1.Items.AddRange(new ToolStripItem[3]
      {
        (ToolStripItem) this.mnuFile,
        (ToolStripItem) this.mnuEdit,
        (ToolStripItem) this.mnuTools
      });
      this.menuStrip1.Location = new Point(0, 0);
      this.menuStrip1.Name = "menuStrip1";
      this.menuStrip1.Size = new Size(792, 24);
      this.menuStrip1.TabIndex = 2;
      this.menuStrip1.Text = "menuStrip1";
      // 建立開啟 DAT 檔案選單項
      this.mnuOpenDat = new ToolStripMenuItem();
      this.mnuOpenDat.Name = "mnuOpenDat";
      this.mnuOpenDat.Size = new Size(180, 22);
      this.mnuOpenDat.Text = "開啟天M DAT檔(&D)...";
      this.mnuOpenDat.Click += new EventHandler(this.mnuOpenDat_Click);

      this.mnuFile.DropDownItems.AddRange(new ToolStripItem[8]
      {
        (ToolStripItem) this.mnuOpen,
        (ToolStripItem) this.mnuOpenSprList,
        (ToolStripItem) this.mnuOpenDat,
        (ToolStripItem) this.toolStripSeparator1,
        (ToolStripItem) this.mnuCreatResource,
        (ToolStripItem) this.mnuRebuild,
        (ToolStripItem) this.toolStripSeparator2,
        (ToolStripItem) this.mnuQuit
      });
      this.mnuFile.Name = "mnuFile";
      this.mnuFile.Size = new Size(34, 20);
      this.mnuFile.Text = "檔案(&F)";
      this.mnuOpen.Image = (Image) Resources.My_Documents;
      this.mnuOpen.Name = "mnuOpen";
      this.mnuOpen.Size = new Size(180, 22);
      this.mnuOpen.Text = "開啟資料夾(&O)";
      this.mnuOpen.Click += new EventHandler(this.mnuOpen_Click);
      this.mnuOpenSprList.Name = "mnuOpenSprList";
      this.mnuOpenSprList.Size = new Size(180, 22);
      this.mnuOpenSprList.Text = "開啟 SPR 列表檔(&S)...";
      this.mnuOpenSprList.Click += new EventHandler(this.mnuOpenSprList_Click);
      this.toolStripSeparator1.Name = "toolStripSeparator1";
      this.toolStripSeparator1.Size = new Size((int) sbyte.MaxValue, 6);
      this.mnuCreatResource.Name = "mnuCreatResource";
      this.mnuCreatResource.Size = new Size(130, 22);
      this.mnuCreatResource.Text = "建立新資源檔";
      this.mnuCreatResource.Visible = false;
      this.mnuRebuild.Enabled = false;
      this.mnuRebuild.Name = "mnuRebuild";
      this.mnuRebuild.Size = new Size(130, 22);
      this.mnuRebuild.Text = "重建(&R)";
      this.mnuRebuild.Click += new EventHandler(this.mnuRebuild_Click);
      this.toolStripSeparator2.Name = "toolStripSeparator2";
      this.toolStripSeparator2.Size = new Size((int) sbyte.MaxValue, 6);
      this.mnuQuit.Image = (Image) Resources.ArreterSZ;
      this.mnuQuit.Name = "mnuQuit";
      this.mnuQuit.Size = new Size(130, 22);
      this.mnuQuit.Text = "結束(&Q)";
      this.mnuEdit.DropDownItems.AddRange(new ToolStripItem[3]
      {
        (ToolStripItem) this.mnuFiller,
        (ToolStripItem) this.toolStripSeparator4,
        (ToolStripItem) this.mnuLanguage
      });
      this.mnuEdit.Name = "mnuEdit";
      this.mnuEdit.Size = new Size(36, 20);
      this.mnuEdit.Text = "編輯(&E)";
      this.mnuFiller.DropDownItems.AddRange(new ToolStripItem[12]
      {
        (ToolStripItem) this.mnuFiller_Text_html,
        (ToolStripItem) this.mnuFiller_Sprite_spr,
        (ToolStripItem) this.mnuFiller_Tile_til,
        (ToolStripItem) this.toolStripSeparator5,
        (ToolStripItem) this.mnuFiller_Text_C,
        (ToolStripItem) this.mnuFiller_Text_H,
        (ToolStripItem) this.mnuFiller_Text_J,
        (ToolStripItem) this.mnuFiller_Text_K,
        (ToolStripItem) this.toolStripSeparator7,
        (ToolStripItem) this.mnuFiller_Sprite_img,
        (ToolStripItem) this.mnuFiller_Sprite_png,
        (ToolStripItem) this.mnuFiller_Sprite_tbt
      });
      this.mnuFiller.Enabled = false;
      this.mnuFiller.Name = "mnuFiller";
      this.mnuFiller.Size = new Size(116, 22);
      this.mnuFiller.Text = "篩選器(&F)";
      this.mnuFiller_Text_html.Checked = true;
      this.mnuFiller_Text_html.CheckOnClick = true;
      this.mnuFiller_Text_html.CheckState = CheckState.Checked;
      this.mnuFiller_Text_html.Name = "mnuFiller_Text_html";
      this.mnuFiller_Text_html.Size = new Size(129, 22);
      this.mnuFiller_Text_html.Text = "*.HTML";
      this.mnuFiller_Text_html.CheckedChanged += new EventHandler(this.mnuFiller_FileStyle);
      this.mnuFiller_Sprite_spr.Checked = true;
      this.mnuFiller_Sprite_spr.CheckOnClick = true;
      this.mnuFiller_Sprite_spr.CheckState = CheckState.Checked;
      this.mnuFiller_Sprite_spr.Name = "mnuFiller_Sprite_spr";
      this.mnuFiller_Sprite_spr.Size = new Size(129, 22);
      this.mnuFiller_Sprite_spr.Text = "*.SPR";
      this.mnuFiller_Sprite_spr.Click += new EventHandler(this.mnuFiller_FileStyle);
      this.mnuFiller_Tile_til.Checked = true;
      this.mnuFiller_Tile_til.CheckOnClick = true;
      this.mnuFiller_Tile_til.CheckState = CheckState.Checked;
      this.mnuFiller_Tile_til.Name = "mnuFiller_Tile_til";
      this.mnuFiller_Tile_til.Size = new Size(129, 22);
      this.mnuFiller_Tile_til.Text = "*.TIL";
      this.mnuFiller_Tile_til.Click += new EventHandler(this.mnuFiller_FileStyle);
      this.toolStripSeparator5.Name = "toolStripSeparator5";
      this.toolStripSeparator5.Size = new Size(126, 6);
      this.mnuFiller_Text_C.CheckOnClick = true;
      this.mnuFiller_Text_C.Name = "mnuFiller_Text_C";
      this.mnuFiller_Text_C.Size = new Size(129, 22);
      this.mnuFiller_Text_C.Text = "台灣";
      this.mnuFiller_Text_C.CheckedChanged += new EventHandler(this.mnuFiller_Text_Language);
      this.mnuFiller_Text_H.CheckOnClick = true;
      this.mnuFiller_Text_H.Name = "mnuFiller_Text_H";
      this.mnuFiller_Text_H.Size = new Size(129, 22);
      this.mnuFiller_Text_H.Text = "中國 && 香港";
      this.mnuFiller_Text_H.CheckedChanged += new EventHandler(this.mnuFiller_Text_Language);
      this.mnuFiller_Text_J.CheckOnClick = true;
      this.mnuFiller_Text_J.Name = "mnuFiller_Text_J";
      this.mnuFiller_Text_J.Size = new Size(129, 22);
      this.mnuFiller_Text_J.Text = "日本";
      this.mnuFiller_Text_J.CheckedChanged += new EventHandler(this.mnuFiller_Text_Language);
      this.mnuFiller_Text_K.CheckOnClick = true;
      this.mnuFiller_Text_K.Name = "mnuFiller_Text_K";
      this.mnuFiller_Text_K.Size = new Size(129, 22);
      this.mnuFiller_Text_K.Text = "韓國";
      this.mnuFiller_Text_K.CheckedChanged += new EventHandler(this.mnuFiller_Text_Language);
      this.toolStripSeparator7.Name = "toolStripSeparator7";
      this.toolStripSeparator7.Size = new Size(126, 6);
      this.mnuFiller_Sprite_img.Checked = true;
      this.mnuFiller_Sprite_img.CheckOnClick = true;
      this.mnuFiller_Sprite_img.CheckState = CheckState.Checked;
      this.mnuFiller_Sprite_img.Name = "mnuFiller_Sprite_img";
      this.mnuFiller_Sprite_img.Size = new Size(129, 22);
      this.mnuFiller_Sprite_img.Text = "*.IMG";
      this.mnuFiller_Sprite_img.Click += new EventHandler(this.mnuFiller_FileStyle);
      this.mnuFiller_Sprite_png.Checked = true;
      this.mnuFiller_Sprite_png.CheckOnClick = true;
      this.mnuFiller_Sprite_png.CheckState = CheckState.Checked;
      this.mnuFiller_Sprite_png.Name = "mnuFiller_Sprite_png";
      this.mnuFiller_Sprite_png.Size = new Size(129, 22);
      this.mnuFiller_Sprite_png.Text = "*.PNG";
      this.mnuFiller_Sprite_png.Click += new EventHandler(this.mnuFiller_FileStyle);
      this.mnuFiller_Sprite_tbt.Checked = true;
      this.mnuFiller_Sprite_tbt.CheckOnClick = true;
      this.mnuFiller_Sprite_tbt.CheckState = CheckState.Checked;
      this.mnuFiller_Sprite_tbt.Name = "mnuFiller_Sprite_tbt";
      this.mnuFiller_Sprite_tbt.Size = new Size(129, 22);
      this.mnuFiller_Sprite_tbt.Text = "*.TBT";
      this.mnuFiller_Sprite_tbt.Click += new EventHandler(this.mnuFiller_FileStyle);
      this.toolStripSeparator4.Name = "toolStripSeparator4";
      this.toolStripSeparator4.Size = new Size(113, 6);
      this.mnuLanguage.DropDownItems.AddRange(new ToolStripItem[2]
      {
        (ToolStripItem) this.mnuLanguage_TW,
        (ToolStripItem) this.mnuLanguage_EN
      });
      this.mnuLanguage.Enabled = false;
      this.mnuLanguage.Image = (Image) Resources.Settings1;
      this.mnuLanguage.Name = "mnuLanguage";
      this.mnuLanguage.Size = new Size(116, 22);
      this.mnuLanguage.Text = "語言(&L)";
      this.mnuLanguage_TW.Checked = true;
      this.mnuLanguage_TW.CheckState = CheckState.Checked;
      this.mnuLanguage_TW.Name = "mnuLanguage_TW";
      this.mnuLanguage_TW.Size = new Size(133, 22);
      this.mnuLanguage_TW.Text = "繁體中文(zh_tw)";
      this.mnuLanguage_EN.Name = "mnuLanguage_EN";
      this.mnuLanguage_EN.Size = new Size(133, 22);
      this.mnuLanguage_EN.Text = "&English";
      this.mnuTools_OptimizePng = new ToolStripMenuItem();
      this.mnuTools_OptimizePng.Name = "mnuTools_OptimizePng";
      this.mnuTools_OptimizePng.Size = new Size(160, 22);
      this.mnuTools_OptimizePng.Text = "批次壓縮 PNG(&P)...";
      this.mnuTools_OptimizePng.ToolTipText = "壓縮 PAK 中所有 PNG 檔案";
      this.mnuTools_OptimizePng.Click += new EventHandler(this.mnuTools_OptimizePng_Click);
      this.mnuTools.DropDownItems.AddRange(new ToolStripItem[10]
      {
        (ToolStripItem) this.mnuTools_Export,
        (ToolStripItem) this.mnuTools_ExportTo,
        (ToolStripItem) this.mnuTools_Delete,
        (ToolStripItem) this.mnuTools_OptimizePng,
        (ToolStripItem) this.toolStripSeparator8,
        (ToolStripItem) this.mnuTools_Add,
        (ToolStripItem) this.mnuTools_Update,
        (ToolStripItem) this.toolStripSeparator9,
        (ToolStripItem) this.mnuTools_ClearSelect,
        (ToolStripItem) this.mnuTools_SelectAll
      });
      this.mnuTools.Name = "mnuTools";
      this.mnuTools.Size = new Size(43, 20);
      this.mnuTools.Text = "工具(&T)";
      this.mnuTools_Export.Enabled = false;
      this.mnuTools_Export.Image = (Image) Resources.Save;
      this.mnuTools_Export.Name = "mnuTools_Export";
      this.mnuTools_Export.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_Export.Text = "匯出(&E)";
      this.mnuTools_Export.ToolTipText = "匯出選取的檔案到 PAK 資料夾";
      this.mnuTools_Export.Click += new EventHandler(this.mnuTools_Export_Click);
      this.mnuTools_ExportTo.Enabled = false;
      this.mnuTools_ExportTo.Name = "mnuTools_ExportTo";
      this.mnuTools_ExportTo.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_ExportTo.Text = "匯出到(&T)...";
      this.mnuTools_ExportTo.ToolTipText = "匯出選取的檔案到指定資料夾";
      this.mnuTools_ExportTo.Click += new EventHandler(this.mnuTools_ExportTo_Click);
      this.mnuTools_Delete.Enabled = false;
      this.mnuTools_Delete.Image = (Image) Resources.Trashcan_empty;
      this.mnuTools_Delete.Name = "mnuTools_Delete";
      this.mnuTools_Delete.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_Delete.Text = "刪除(&D)";
      this.mnuTools_Delete.Click += new EventHandler(this.mnuTools_Delete_Click);
      this.toolStripSeparator8.Name = "toolStripSeparator8";
      this.toolStripSeparator8.Size = new Size(124, 6);
      this.mnuTools_Add.Enabled = false;
      this.mnuTools_Add.Name = "mnuTools_Add";
      this.mnuTools_Add.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_Add.Text = "新增(&A)...";
      this.mnuTools_Add.Click += new EventHandler(this.mnuTools_Add_Click);
      this.mnuTools_Update.Enabled = false;
      this.mnuTools_Update.Name = "mnuTools_Update";
      this.mnuTools_Update.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_Update.Text = "更新(&U)";
      this.mnuTools_Update.ToolTipText = "此功能僅適用於文字檔案";
      this.mnuTools_Update.Click += new EventHandler(this.mnuTools_Update_Click);
      this.toolStripSeparator9.Name = "toolStripSeparator9";
      this.toolStripSeparator9.Size = new Size(124, 6);
      this.mnuTools_ClearSelect.Name = "mnuTools_ClearSelect";
      this.mnuTools_ClearSelect.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_ClearSelect.Text = "取消全選";
      this.mnuTools_ClearSelect.Click += new EventHandler(this.tsmUnselectAll_Click);
      this.mnuTools_SelectAll.Name = "mnuTools_SelectAll";
      this.mnuTools_SelectAll.Size = new Size((int) sbyte.MaxValue, 22);
      this.mnuTools_SelectAll.Text = "全選";
      this.mnuTools_SelectAll.Click += new EventHandler(this.tsmSelectAll_Click);
      this.dlgOpenFile.FileName = "openFileDialog1";
      this.splitContainer1.Dock = DockStyle.Fill;
      this.splitContainer1.FixedPanel = FixedPanel.Panel1;
      this.splitContainer1.Location = new Point(0, 24);
      this.splitContainer1.Name = "splitContainer1";
      this.splitContainer1.Panel1.Controls.Add((Control) this.splitContainer2);
      this.splitContainer1.Panel2.Controls.Add((Control) this.TextCompViewer);
      this.splitContainer1.Panel2.Controls.Add((Control) this.TextViewer);
      this.splitContainer1.Panel2.Controls.Add((Control) this.ImageViewer);
      this.splitContainer1.Panel2.Controls.Add((Control) this.SprViewer);
      this.splitContainer1.Panel2.Controls.Add((Control) this.SprListViewer);
      this.splitContainer1.Panel2.Controls.Add((Control) this.palContentSearch);
      this.splitContainer1.Size = new Size(792, 520);
      this.splitContainer1.SplitterDistance = 297;
      this.splitContainer1.TabIndex = 2;
      this.splitContainer2.Dock = DockStyle.Fill;
      this.splitContainer2.FixedPanel = FixedPanel.Panel1;
      this.splitContainer2.Location = new Point(0, 0);
      this.splitContainer2.Name = "splitContainer2";
      this.splitContainer2.Orientation = Orientation.Horizontal;
      this.splitContainer2.Panel1.Controls.Add((Control) this.palSearch);
      this.splitContainer2.Panel2.Controls.Add((Control) this.lvIndexInfo);
      this.splitContainer2.Size = new Size(297, 520);
      this.splitContainer2.SplitterDistance = 65;
      this.splitContainer2.TabIndex = 2;
      this.palSearch.AutoSize = false;
      this.palSearch.Controls.Add((Control) this.label1);
      this.palSearch.Controls.Add((Control) this.txtSearch);
      this.palSearch.Dock = DockStyle.Top;
      this.palSearch.Location = new Point(0, 0);
      this.palSearch.Name = "palSearch";
      this.palSearch.Size = new Size(297, 60);
      this.palSearch.TabIndex = 1;
      this.label1.Location = new Point(3, 7);
      this.label1.Name = "label1";
      this.label1.Size = new Size(40, 20);
      this.label1.TabIndex = 0;
      this.label1.Text = "篩選：";
      this.label1.TextAlign = ContentAlignment.MiddleLeft;
      this.txtSearch.Location = new Point(45, 7);
      this.txtSearch.Name = "txtSearch";
      this.txtSearch.Size = new Size(245, 22);
      this.txtSearch.TabIndex = 1;
      this.txtSearch.KeyPress += new KeyPressEventHandler(this.txtSearch_KeyPress);
      this.lvIndexInfo.Dock = DockStyle.Fill;
      this.lvIndexInfo.Font = new Font("細明體", 9f);
      this.lvIndexInfo.FullRowSelect = true;
      this.lvIndexInfo.HideSelection = false;
      this.lvIndexInfo.Location = new Point(0, 0);
      this.lvIndexInfo.Name = "lvIndexInfo";
      this.lvIndexInfo.Size = new Size(297, 491);
      this.lvIndexInfo.TabIndex = 0;
      this.lvIndexInfo.UseCompatibleStateImageBehavior = false;
      this.lvIndexInfo.View = View.Details;
      this.lvIndexInfo.VirtualMode = true;
      this.lvIndexInfo.CheckBoxes = true;
      this.lvIndexInfo.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(this.lvIndexInfo_RetrieveVirtualItem);
      this.lvIndexInfo.ItemCheck += new ItemCheckEventHandler(this.lvIndexInfo_ItemCheck);
      this.lvIndexInfo.MouseClick += new MouseEventHandler(this.lvIndexInfo_MouseClick);
      this.lvIndexInfo.SelectedIndexChanged += new EventHandler(this.lvIndexInfo_SelectedIndexChanged);
      this.lvIndexInfo.ColumnClick += new ColumnClickEventHandler(this.lvIndexInfo_ColumnClick);
      this.TextCompViewer.Location = new Point(0, 0);
      this.TextCompViewer.Name = "TextCompViewer";
      this.TextCompViewer.Size = new Size(184, 162);
      this.TextCompViewer.TabIndex = 5;
      this.TextCompViewer.Visible = false;
      this.TextViewer.Font = new Font("新細明體", 12f, FontStyle.Regular, GraphicsUnit.Point, (byte) 136);
      this.TextViewer.Location = new Point(0, 0);
      this.TextViewer.Multiline = true;
      this.TextViewer.Name = "TextViewer";
      this.TextViewer.ScrollBars = RichTextBoxScrollBars.Both;
      this.TextViewer.Size = new Size(228, 200);
      this.TextViewer.TabIndex = 1;
      this.TextViewer.WordWrap = false;
      this.TextViewer.ReadOnly = false;
      this.TextViewer.AcceptsTab = true;
      this.TextViewer.DetectUrls = false;
      this.TextViewer.KeyDown += new KeyEventHandler(this.TextViewer_KeyDown);
      this.ImageViewer.AutoScroll = true;
      this.ImageViewer.BackColor = Color.Black;
      this.ImageViewer.BorderStyle = BorderStyle.Fixed3D;
      this.ImageViewer.Image = (Image) null;
      this.ImageViewer.Location = new Point(0, 0);
      this.ImageViewer.Name = "ImageViewer";
      this.ImageViewer.Size = new Size(267, 240);
      this.ImageViewer.TabIndex = 4;
      this.SprViewer.AutoScroll = true;
      this.SprViewer.BackColor = Color.Red;
      this.SprViewer.BorderStyle = BorderStyle.FixedSingle;
      this.SprViewer.Location = new Point(0, 0);
      this.SprViewer.Name = "SprViewer";
      this.SprViewer.Size = new Size(324, 277);
      this.SprViewer.SprFrames = (L1Spr.Frame[]) null;
      this.SprViewer.TabIndex = 3;
      this.SprViewer.Visible = false;
      this.SprListViewer.AutoScroll = true;
      this.SprListViewer.BackColor = Color.White;
      this.SprListViewer.Location = new Point(0, 0);
      this.SprListViewer.Name = "SprListViewer";
      this.SprListViewer.Size = new Size(324, 277);
      this.SprListViewer.TabIndex = 6;
      this.SprListViewer.Visible = false;
      this.tsmOptimizePng = new ToolStripMenuItem();
      this.tsmOptimizePng.Name = "tsmOptimizePng";
      this.tsmOptimizePng.Size = new Size(136, 22);
      this.tsmOptimizePng.Text = "壓縮 PNG(&O)";
      this.tsmOptimizePng.ToolTipText = "無損壓縮選取的 PNG 檔案";
      this.tsmOptimizePng.Click += new EventHandler(this.tsmOptimizePng_Click);
      this.ctxMenu.Items.AddRange(new ToolStripItem[11]
      {
        (ToolStripItem) this.tsmCopyFileName,
        (ToolStripItem) this.tsmExport,
        (ToolStripItem) this.tsmExportTo,
        (ToolStripItem) this.tsmOptimizePng,
        (ToolStripItem) this.toolStripSeparator6,
        (ToolStripItem) this.tsmUnselectAll,
        (ToolStripItem) this.tsmSelectAll,
        (ToolStripItem) this.toolStripSeparator10,
        (ToolStripItem) this.tsmDelete,
        (ToolStripItem) this.toolStripSeparator3,
        (ToolStripItem) this.tsmCompare
      });
      this.ctxMenu.Name = "ctxMenu";
      this.ctxMenu.Size = new Size(137, 126);
      this.ctxMenu.Opening += new CancelEventHandler(this.ctxMenu_Opening);
      this.tsmCopyFileName.Name = "tsmCopyFileName";
      this.tsmCopyFileName.Size = new Size(136, 22);
      this.tsmCopyFileName.Text = "複製檔名(&C)";
      this.tsmCopyFileName.Click += new EventHandler(this.tsmCopyFileName_Click);
      this.tsmExport.Image = (Image) Resources.Save;
      this.tsmExport.Name = "tsmExport";
      this.tsmExport.Size = new Size(136, 22);
      this.tsmExport.Text = "匯出(&E)";
      this.tsmExport.ToolTipText = "匯出檔案到 PAK 資料夾";
      this.tsmExport.Click += new EventHandler(this.tsmExport_Click);
      this.tsmExportTo.Name = "tsmExportTo";
      this.tsmExportTo.Size = new Size(136, 22);
      this.tsmExportTo.Text = "匯出到(&T)...";
      this.tsmExportTo.ToolTipText = "匯出檔案到指定資料夾";
      this.tsmExportTo.Click += new EventHandler(this.tsmExportTo_Click);
      this.toolStripSeparator6.Name = "toolStripSeparator6";
      this.toolStripSeparator6.Size = new Size(133, 6);
      this.tsmUnselectAll.Name = "tsmUnselectAll";
      this.tsmUnselectAll.Size = new Size(136, 22);
      this.tsmUnselectAll.Text = "取消全選";
      this.tsmUnselectAll.Click += new EventHandler(this.tsmUnselectAll_Click);
      this.tsmSelectAll.Name = "tsmSelectAll";
      this.tsmSelectAll.Size = new Size(136, 22);
      this.tsmSelectAll.Text = "全選";
      this.tsmSelectAll.Click += new EventHandler(this.tsmSelectAll_Click);
      this.toolStripSeparator10.Name = "toolStripSeparator10";
      this.toolStripSeparator10.Size = new Size(133, 6);
      this.tsmDelete.Name = "tsmDelete";
      this.tsmDelete.Size = new Size(136, 22);
      this.tsmDelete.Text = "刪除(&D)";
      this.tsmDelete.Click += new EventHandler(this.tsmDelete_Click);
      this.toolStripSeparator3.Name = "toolStripSeparator3";
      this.toolStripSeparator3.Size = new Size(133, 6);
      this.tsmCompare.DropDownItems.AddRange(new ToolStripItem[4]
      {
        (ToolStripItem) this.tsmCompTW,
        (ToolStripItem) this.tsmCompHK,
        (ToolStripItem) this.tsmCompJP,
        (ToolStripItem) this.tsmCompKO
      });
      this.tsmCompare.Name = "tsmCompare";
      this.tsmCompare.Size = new Size(136, 22);
      this.tsmCompare.Text = "比較與";
      this.tsmCompare.DropDownOpening += new EventHandler(this.tsmCompare_DropDownOpening);
      this.tsmCompTW.Name = "tsmCompTW";
      this.tsmCompTW.Size = new Size(129, 22);
      this.tsmCompTW.Text = "台灣";
      this.tsmCompTW.Click += new EventHandler(this.tsmCompare_Click);
      this.tsmCompHK.Name = "tsmCompHK";
      this.tsmCompHK.Size = new Size(129, 22);
      this.tsmCompHK.Text = "中國 && 香港";
      this.tsmCompHK.Click += new EventHandler(this.tsmCompare_Click);
      this.tsmCompJP.Name = "tsmCompJP";
      this.tsmCompJP.Size = new Size(129, 22);
      this.tsmCompJP.Text = "日本";
      this.tsmCompJP.Click += new EventHandler(this.tsmCompare_Click);
      this.tsmCompKO.Name = "tsmCompKO";
      this.tsmCompKO.Size = new Size(129, 22);
      this.tsmCompKO.Text = "韓國";
      this.tsmCompKO.Click += new EventHandler(this.tsmCompare_Click);
      this.statusStrip1.Items.AddRange(new ToolStripItem[7]
      {
        (ToolStripItem) this.tssLocker,
        (ToolStripItem) this.tssRecordCount,
        (ToolStripItem) this.tssShowInListView,
        (ToolStripItem) this.tssCheckedCount,
        (ToolStripItem) this.tssMessage,
        (ToolStripItem) this.tssProgressName,
        (ToolStripItem) this.tssProgress
      });
      this.statusStrip1.Location = new Point(0, 544);
      this.statusStrip1.Name = "statusStrip1";
      this.statusStrip1.Size = new Size(792, 22);
      this.statusStrip1.TabIndex = 5;
      this.statusStrip1.Text = "statusStrip1";
      this.tssLocker.BorderSides = ToolStripStatusLabelBorderSides.All;
      this.tssLocker.BorderStyle = Border3DStyle.SunkenOuter;
      this.tssLocker.DisplayStyle = ToolStripItemDisplayStyle.Image;
      this.tssLocker.Image = (Image) Resources.Locker;
      this.tssLocker.Name = "tssLocker";
      this.tssLocker.Size = new Size(20, 20);
      this.tssLocker.Visible = false;
      this.tssRecordCount.BorderSides = ToolStripStatusLabelBorderSides.Right;
      this.tssRecordCount.Name = "tssRecordCount";
      this.tssRecordCount.Size = new Size(56, 17);
      this.tssRecordCount.Text = "Records:0";
      this.tssShowInListView.BorderSides = ToolStripStatusLabelBorderSides.Right;
      this.tssShowInListView.Name = "tssShowInListView";
      this.tssShowInListView.Size = new Size(50, 17);
      this.tssShowInListView.Text = "Shown:0";
      this.tssCheckedCount.BorderSides = ToolStripStatusLabelBorderSides.Right;
      this.tssCheckedCount.Name = "tssCheckedCount";
      this.tssCheckedCount.Size = new Size(54, 20);
      this.tssCheckedCount.Text = "已選：0";
      this.tssCheckedCount.Visible = true;
      this.tssMessage.Name = "tssMessage";
      this.tssMessage.Size = new Size(671, 17);
      this.tssMessage.Spring = true;
      this.tssMessage.TextAlign = ContentAlignment.MiddleLeft;
      this.tssProgressName.BorderSides = ToolStripStatusLabelBorderSides.Left;
      this.tssProgressName.Name = "tssProgressName";
      this.tssProgressName.Size = new Size(57, 20);
      this.tssProgressName.Text = "Loading...";
      this.tssProgressName.Visible = false;
      this.tssProgressName.TextChanged += new EventHandler(this.tssProgressName_TextChanged);
      this.tssProgress.Name = "tssProgress";
      this.tssProgress.Size = new Size(200, 19);
      this.tssProgress.Visible = false;
      this.dlgAddFiles.FileName = "openFileDialog1";
      this.dlgAddFiles.Multiselect = true;
      // palToolbar
      this.palToolbar.Controls.Add((Control) this.lblFolder);
      this.palToolbar.Controls.Add((Control) this.cmbIdxFiles);
      this.palToolbar.Controls.Add((Control) this.chkSpriteMode);
      this.palToolbar.Controls.Add((Control) this.chkSprListMode);
      // Sprite Mode 分類控件
      this.btnLoadListSpr = new Button();
      this.btnLoadListSpr.Location = new Point(500, 24);
      this.btnLoadListSpr.Size = new Size(80, 22);
      this.btnLoadListSpr.Text = "載入list.spr";
      this.btnLoadListSpr.Font = new Font("Microsoft JhengHei UI", 8);
      this.btnLoadListSpr.Visible = false;
      this.btnLoadListSpr.Click += new EventHandler(this.btnLoadListSpr_Click);
      this.palToolbar.Controls.Add((Control) this.btnLoadListSpr);
      this.cmbSpriteTypeFilter = new ComboBox();
      this.cmbSpriteTypeFilter.Location = new Point(585, 24);
      this.cmbSpriteTypeFilter.Size = new Size(100, 22);
      this.cmbSpriteTypeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
      this.cmbSpriteTypeFilter.Visible = false;
      this.cmbSpriteTypeFilter.SelectedIndexChanged += new EventHandler(this.cmbSpriteTypeFilter_SelectedIndexChanged);
      this.palToolbar.Controls.Add((Control) this.cmbSpriteTypeFilter);
      // List SPR 模式的類型過濾下拉選單
      this.cmbSprListTypeFilter = new ComboBox();
      this.cmbSprListTypeFilter.Location = new Point(500, 24);
      this.cmbSprListTypeFilter.Size = new Size(120, 22);
      this.cmbSprListTypeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
      this.cmbSprListTypeFilter.Visible = false;
      this.cmbSprListTypeFilter.SelectedIndexChanged += new EventHandler(this.cmbSprListTypeFilter_SelectedIndexChanged);
      this.palToolbar.Controls.Add((Control) this.cmbSprListTypeFilter);
      this.palToolbar.Dock = DockStyle.Top;
      this.palToolbar.Location = new Point(0, 24);
      this.palToolbar.Name = "palToolbar";
      this.palToolbar.Size = new Size(792, 50);
      this.palToolbar.TabIndex = 6;
      // lblFolder
      this.lblFolder.AutoSize = true;
      this.lblFolder.Location = new Point(5, 5);
      this.lblFolder.Name = "lblFolder";
      this.lblFolder.Size = new Size(100, 12);
      this.lblFolder.TabIndex = 0;
      this.lblFolder.Text = "Folder: (not selected)";
      // cmbIdxFiles
      this.cmbIdxFiles.DropDownStyle = ComboBoxStyle.DropDownList;
      this.cmbIdxFiles.FormattingEnabled = true;
      this.cmbIdxFiles.Location = new Point(5, 25);
      this.cmbIdxFiles.Name = "cmbIdxFiles";
      this.cmbIdxFiles.Size = new Size(280, 20);
      this.cmbIdxFiles.TabIndex = 1;
      this.cmbIdxFiles.SelectedIndexChanged += new EventHandler(this.cmbIdxFiles_SelectedIndexChanged);
      // chkSpriteMode
      this.chkSpriteMode.AutoSize = true;
      this.chkSpriteMode.Location = new Point(295, 27);
      this.chkSpriteMode.Name = "chkSpriteMode";
      this.chkSpriteMode.Size = new Size(90, 16);
      this.chkSpriteMode.TabIndex = 2;
      this.chkSpriteMode.Text = "Sprite 模式";
      this.chkSpriteMode.CheckedChanged += new EventHandler(this.chkSpriteMode_CheckedChanged);
      // chkSprListMode
      this.chkSprListMode.AutoSize = true;
      this.chkSprListMode.Location = new Point(395, 27);
      this.chkSprListMode.Name = "chkSprListMode";
      this.chkSprListMode.Size = new Size(100, 16);
      this.chkSprListMode.TabIndex = 3;
      this.chkSprListMode.Text = "List SPR 模式";
      this.chkSprListMode.CheckedChanged += new EventHandler(this.chkSprListMode_CheckedChanged);
      // chkGalleryMode (在搜尋那排右邊)
      this.chkGalleryMode = new CheckBox();
      this.chkGalleryMode.AutoSize = true;
      this.chkGalleryMode.Location = new Point(295, 8);
      this.chkGalleryMode.Name = "chkGalleryMode";
      this.chkGalleryMode.Size = new Size(80, 16);
      this.chkGalleryMode.TabIndex = 10;
      this.chkGalleryMode.Text = "相簿模式";
      this.chkGalleryMode.CheckedChanged += new EventHandler(this.chkGalleryMode_CheckedChanged);
      this.palContentSearch.Controls.Add((Control) this.chkGalleryMode);
      // palContentSearch
      this.palContentSearch.Controls.Add((Control) this.lblContentSearch);
      this.palContentSearch.Controls.Add((Control) this.txtContentSearch);
      this.palContentSearch.Controls.Add((Control) this.btnContentSearch);
      this.palContentSearch.Controls.Add((Control) this.btnClearSearch);
      this.palContentSearch.Controls.Add((Control) this.lblExtFilter);
      this.palContentSearch.Controls.Add((Control) this.cmbExtFilter);
      this.palContentSearch.Controls.Add((Control) this.lblLangFilter);
      this.palContentSearch.Controls.Add((Control) this.cmbLangFilter);
      this.palContentSearch.Controls.Add((Control) this.btnSaveText);
      this.palContentSearch.Controls.Add((Control) this.btnCancelEdit);
      this.palContentSearch.Controls.Add((Control) this.chkSkipSaveConfirm);
      this.palContentSearch.Dock = DockStyle.Top;
      this.palContentSearch.Location = new Point(0, 0);
      this.palContentSearch.Name = "palContentSearch";
      this.palContentSearch.Size = new Size(491, 60);
      this.palContentSearch.TabIndex = 10;
      // 第一排：內容搜尋 (Y=7 對齊左邊)
      // lblContentSearch
      this.lblContentSearch.AutoSize = false;
      this.lblContentSearch.Location = new Point(3, 7);
      this.lblContentSearch.Name = "lblContentSearch";
      this.lblContentSearch.Size = new Size(50, 20);
      this.lblContentSearch.TabIndex = 0;
      this.lblContentSearch.Text = "搜尋：";
      this.lblContentSearch.TextAlign = ContentAlignment.MiddleLeft;
      // txtContentSearch
      this.txtContentSearch.Location = new Point(55, 7);
      this.txtContentSearch.Name = "txtContentSearch";
      this.txtContentSearch.Size = new Size(120, 22);
      this.txtContentSearch.TabIndex = 1;
      this.txtContentSearch.KeyPress += new KeyPressEventHandler(this.txtContentSearch_KeyPress);
      // btnContentSearch
      this.btnContentSearch.Location = new Point(180, 5);
      this.btnContentSearch.Name = "btnContentSearch";
      this.btnContentSearch.Size = new Size(50, 25);
      this.btnContentSearch.TabIndex = 2;
      this.btnContentSearch.Text = "搜尋";
      this.btnContentSearch.UseVisualStyleBackColor = true;
      this.btnContentSearch.Click += new EventHandler(this.btnContentSearch_Click);
      // btnClearSearch
      this.btnClearSearch.Location = new Point(235, 5);
      this.btnClearSearch.Name = "btnClearSearch";
      this.btnClearSearch.Size = new Size(50, 25);
      this.btnClearSearch.TabIndex = 3;
      this.btnClearSearch.Text = "清除";
      this.btnClearSearch.UseVisualStyleBackColor = true;
      this.btnClearSearch.Click += new EventHandler(this.btnClearSearch_Click);
      // 第二排：過濾與儲存 (Y=35)
      // lblExtFilter
      this.lblExtFilter.AutoSize = false;
      this.lblExtFilter.Location = new Point(3, 35);
      this.lblExtFilter.Name = "lblExtFilter";
      this.lblExtFilter.Size = new Size(55, 20);
      this.lblExtFilter.TabIndex = 4;
      this.lblExtFilter.Text = "副檔名：";
      this.lblExtFilter.TextAlign = ContentAlignment.MiddleLeft;
      // cmbExtFilter
      this.cmbExtFilter.DropDownStyle = ComboBoxStyle.DropDownList;
      this.cmbExtFilter.FormattingEnabled = true;
      this.cmbExtFilter.Items.AddRange(new object[] { "全部", ".html", ".tbl", ".txt", ".h", ".ht", ".htm", ".def", ".til", ".spr", ".img", ".png", ".tbt" });
      this.cmbExtFilter.Location = new Point(60, 35);
      this.cmbExtFilter.Name = "cmbExtFilter";
      this.cmbExtFilter.Size = new Size(60, 20);
      this.cmbExtFilter.TabIndex = 5;
      this.cmbExtFilter.SelectedIndex = 0;
      this.cmbExtFilter.SelectedIndexChanged += new EventHandler(this.cmbExtFilter_SelectedIndexChanged);
      // lblLangFilter
      this.lblLangFilter.AutoSize = false;
      this.lblLangFilter.Location = new Point(125, 35);
      this.lblLangFilter.Name = "lblLangFilter";
      this.lblLangFilter.Size = new Size(50, 20);
      this.lblLangFilter.TabIndex = 6;
      this.lblLangFilter.Text = "語言：";
      this.lblLangFilter.TextAlign = ContentAlignment.MiddleLeft;
      // cmbLangFilter
      this.cmbLangFilter.DropDownStyle = ComboBoxStyle.DropDownList;
      this.cmbLangFilter.FormattingEnabled = true;
      this.cmbLangFilter.Items.AddRange(new object[] { "全部", "-c (台灣)", "-h (中國)", "-j (日本)", "-k (韓國)" });
      this.cmbLangFilter.Location = new Point(178, 35);
      this.cmbLangFilter.Name = "cmbLangFilter";
      this.cmbLangFilter.Size = new Size(70, 20);
      this.cmbLangFilter.TabIndex = 7;
      this.cmbLangFilter.SelectedIndex = 1;
      this.cmbLangFilter.SelectedIndexChanged += new EventHandler(this.cmbLangFilter_SelectedIndexChanged);
      // btnSaveText (第二排右側)
      this.btnSaveText.Location = new Point(263, 33);
      this.btnSaveText.Name = "btnSaveText";
      this.btnSaveText.Size = new Size(50, 25);
      this.btnSaveText.TabIndex = 8;
      this.btnSaveText.Text = "儲存";
      this.btnSaveText.UseVisualStyleBackColor = true;
      this.btnSaveText.Enabled = false;
      this.btnSaveText.Click += new EventHandler(this.btnSaveText_Click);
      // btnCancelEdit (第二排右側，儲存按鈕旁邊)
      this.btnCancelEdit.Location = new Point(318, 33);
      this.btnCancelEdit.Name = "btnCancelEdit";
      this.btnCancelEdit.Size = new Size(50, 25);
      this.btnCancelEdit.TabIndex = 9;
      this.btnCancelEdit.Text = "取消";
      this.btnCancelEdit.UseVisualStyleBackColor = true;
      this.btnCancelEdit.Enabled = false;
      this.btnCancelEdit.Click += new EventHandler(this.btnCancelEdit_Click);
      // chkSkipSaveConfirm (第二排右側)
      this.chkSkipSaveConfirm.AutoSize = true;
      this.chkSkipSaveConfirm.Location = new Point(373, 38);
      this.chkSkipSaveConfirm.Name = "chkSkipSaveConfirm";
      this.chkSkipSaveConfirm.Size = new Size(100, 16);
      this.chkSkipSaveConfirm.TabIndex = 9;
      this.chkSkipSaveConfirm.Text = "略過確認";
      this.chkSkipSaveConfirm.Checked = Settings.Default.SkipSaveConfirmation;
      this.chkSkipSaveConfirm.CheckedChanged += new EventHandler(this.chkSkipSaveConfirm_CheckedChanged);
      this.AutoScaleDimensions = new SizeF(6f, 12f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.ClientSize = new Size(792, 616);
      this.Controls.Add((Control) this.splitContainer1);
      this.Controls.Add((Control) this.palToolbar);
      this.Controls.Add((Control) this.statusStrip1);
      this.Controls.Add((Control) this.menuStrip1);
      this.Icon = (Icon) componentResourceManager.GetObject("$this.Icon");
      this.MainMenuStrip = this.menuStrip1;
      this.Name = "frmMain";
      this.Text = "Lineage I Pack File Viewer";
      this.WindowState = FormWindowState.Normal;
      this.menuStrip1.ResumeLayout(false);
      this.menuStrip1.PerformLayout();
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      this.splitContainer1.Panel2.PerformLayout();
      this.splitContainer1.ResumeLayout(false);
      this.splitContainer2.Panel1.ResumeLayout(false);
      this.splitContainer2.Panel1.PerformLayout();
      this.splitContainer2.Panel2.ResumeLayout(false);
      this.splitContainer2.ResumeLayout(false);
      this.palSearch.ResumeLayout(false);
      this.palSearch.PerformLayout();
      this.ctxMenu.ResumeLayout(false);
      this.statusStrip1.ResumeLayout(false);
      this.statusStrip1.PerformLayout();
      this.palToolbar.ResumeLayout(false);
      this.palToolbar.PerformLayout();
      this.palContentSearch.ResumeLayout(false);
      this.palContentSearch.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    private class ListViewItemComparer : IComparer
    {
      private int col;
      private int sorting;

      public ListViewItemComparer()
      {
        this.col = 0;
        this.sorting = 1;
      }

      public ListViewItemComparer(int column)
      {
        this.sorting = column >= 0 ? 1 : -1;
        this.col = column * this.sorting;
      }

      public int Compare(object x, object y)
      {
        string text1 = ((ListViewItem) x).SubItems[this.col].Text;
        string text2 = ((ListViewItem) y).SubItems[this.col].Text;
        if (this.col == 1 || this.col == 3)
          return this.sorting * string.Compare(text1, text2);
        // Size column (col 2) now contains decimal KB values
        if (this.col == 2)
        {
          double val1, val2;
          if (double.TryParse(text1, out val1) && double.TryParse(text2, out val2))
            return this.sorting * val1.CompareTo(val2);
        }
        return this.sorting * (int.Parse(text1.Replace(",", "")) - int.Parse(text2.Replace(",", "")));
      }
    }

    // ============================================================================
    // DAT 模式相關方法
    // ============================================================================

    /// <summary>
    /// 開啟天M DAT檔案選單點擊事件
    /// </summary>
    private void mnuOpenDat_Click(object sender, EventArgs e)
    {
      using (var dlg = new OpenFileDialog())
      {
        dlg.Title = "選擇天M DAT檔案 (可複選)";
        dlg.Filter = "DAT 檔案 (*.dat)|*.dat|所有檔案 (*.*)|*.*";
        dlg.Multiselect = true;

        // 使用上次的資料夾
        if (!string.IsNullOrEmpty(this._SelectedFolder))
          dlg.InitialDirectory = this._SelectedFolder;

        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        // 退出其他模式
        if (this._IsSprListMode)
          this.ExitSprListMode();
        if (this._IsSpriteMode)
          this.ExitSpriteMode();
        if (this._IsDatMode)
          this.ExitDatMode();

        this.Cursor = Cursors.WaitCursor;
        try
        {
          LoadDatFiles(dlg.FileNames);
        }
        finally
        {
          this.Cursor = Cursors.Default;
        }
      }
    }

    /// <summary>
    /// 載入 DAT 檔案
    /// </summary>
    private void LoadDatFiles(string[] filePaths)
    {
      this._DatFiles = new List<string>(filePaths);
      this._DatFileObjects = new List<DatTools.DatFile>();
      this._AllDatEntries = new List<DatTools.DatIndexEntry>();
      this._DatGroups = new Dictionary<string, List<DatTools.DatIndexEntry>>();

      this.tssProgressName.Text = "載入 DAT 檔案...";
      this.tssProgress.Value = 0;
      this.tssProgress.Maximum = filePaths.Length;

      foreach (string filePath in filePaths)
      {
        try
        {
          var datFile = new DatTools.DatFile(filePath);
          datFile.ReadFooter();
          datFile.DecryptIndex();
          datFile.ParseEntries();

          this._DatFileObjects.Add(datFile);
          this._AllDatEntries.AddRange(datFile.Entries);

          this.tssProgress.Value++;
          Application.DoEvents();
        }
        catch (Exception ex)
        {
          MessageBox.Show($"載入 {Path.GetFileName(filePath)} 失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }

      // 建立目錄分組
      BuildDatGroups();

      // 設定 DAT 模式
      this._IsDatMode = true;
      SetupDatModeUI();

      // 更新狀態列
      this.tssRecordCount.Text = $"DAT: {this._DatFileObjects.Count} 個檔案";
      this.tssShowInListView.Text = $"項目: {this._AllDatEntries.Count}";
      this.tssProgressName.Text = "";
      this.tssProgress.Value = 0;
    }

    /// <summary>
    /// 建立 DAT 目錄分組
    /// </summary>
    private void BuildDatGroups()
    {
      this._DatGroups = new Dictionary<string, List<DatTools.DatIndexEntry>>();

      foreach (var entry in this._AllDatEntries)
      {
        // 取得目錄路徑 (第一層或第二層)
        string path = entry.Path.TrimStart('/', '\\');
        string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        string groupKey;
        if (parts.Length >= 2)
        {
          // 使用前兩層目錄作為分組鍵 (e.g. "Image/BM")
          groupKey = parts[0] + "/" + parts[1];
        }
        else if (parts.Length == 1)
        {
          // 只有檔名，使用根目錄
          groupKey = "(root)";
        }
        else
        {
          groupKey = "(unknown)";
        }

        if (!this._DatGroups.ContainsKey(groupKey))
        {
          this._DatGroups[groupKey] = new List<DatTools.DatIndexEntry>();
        }
        this._DatGroups[groupKey].Add(entry);
      }

      // 排序分組鍵
      this._DatGroupKeys = this._DatGroups.Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// 設定 DAT 模式 UI (簡化版 - 直接顯示檔案列表)
    /// </summary>
    private void SetupDatModeUI()
    {
      if (this.lvDatFiles != null)
        return;

      // 隱藏其他檢視器
      this.TextViewer.Visible = false;
      this.ImageViewer.Visible = false;
      this.SprViewer.Visible = false;
      this.TextCompViewer.Visible = false;
      if (this.SprListViewer != null) this.SprListViewer.Visible = false;
      if (this.SprDetailViewer != null) this.SprDetailViewer.Visible = false;

      // 建立檔案列表 ListView
      this.lvDatFiles = new ListView();
      this.lvDatFiles.Dock = DockStyle.Fill;
      this.lvDatFiles.View = View.Details;
      this.lvDatFiles.FullRowSelect = true;
      this.lvDatFiles.GridLines = true;
      this.lvDatFiles.VirtualMode = true;
      this.lvDatFiles.Columns.Add("檔名", 250, HorizontalAlignment.Left);
      this.lvDatFiles.Columns.Add("大小", 80, HorizontalAlignment.Right);
      this.lvDatFiles.Columns.Add("來源 DAT", 120, HorizontalAlignment.Left);
      this.lvDatFiles.RetrieveVirtualItem += lvDatFiles_RetrieveVirtualItem;
      this.lvDatFiles.SelectedIndexChanged += lvDatFiles_SelectedIndexChanged;
      this.lvDatFiles.ContextMenuStrip = CreateDatFileContextMenu();

      // 替換 splitContainer2.Panel2 的內容
      this.splitContainer2.Panel2.Controls.Clear();
      this.splitContainer2.Panel2.Controls.Add(this.lvDatFiles);

      // 建立右側圖片檢視器
      this.DatImageViewer = new ucImgViewer();
      this.DatImageViewer.Dock = DockStyle.Fill;
      this.splitContainer1.Panel2.Controls.Clear();
      this.splitContainer1.Panel2.Controls.Add(this.DatImageViewer);

      // 初始化篩選後的條目為全部
      this._FilteredDatEntries = new List<DatTools.DatIndexEntry>(this._AllDatEntries);
      this.lvDatFiles.VirtualListSize = this._FilteredDatEntries.Count;
    }

    /// <summary>
    /// 建立 DAT 檔案列表右鍵選單
    /// </summary>
    private ContextMenuStrip CreateDatFileContextMenu()
    {
      var menu = new ContextMenuStrip();

      var exportPng = new ToolStripMenuItem("匯出為 PNG...");
      exportPng.Click += (s, e) => ExportDatFilesAsPng();
      menu.Items.Add(exportPng);

      var exportOriginal = new ToolStripMenuItem("匯出原始檔案...");
      exportOriginal.Click += (s, e) => ExportDatFilesOriginal();
      menu.Items.Add(exportOriginal);

      menu.Items.Add(new ToolStripSeparator());

      var exportAllZip = new ToolStripMenuItem("匯出全部為 ZIP...");
      exportAllZip.Click += (s, e) => ExportAllDatToZip();
      menu.Items.Add(exportAllZip);

      menu.Items.Add(new ToolStripSeparator());

      var selectAll = new ToolStripMenuItem("全選");
      selectAll.Click += (s, e) => {
        for (int i = 0; i < this.lvDatFiles.VirtualListSize; i++)
          this.lvDatFiles.SelectedIndices.Add(i);
      };
      menu.Items.Add(selectAll);

      return menu;
    }

    /// <summary>
    /// 退出 DAT 模式
    /// </summary>
    private void ExitDatMode()
    {
      if (!this._IsDatMode)
        return;

      this._IsDatMode = false;

      // 清理 UI
      if (this.lvDatFiles != null)
      {
        this.splitContainer2.Panel2.Controls.Clear();
        this.lvDatFiles.Dispose();
        this.lvDatFiles = null;
      }

      if (this.DatImageViewer != null)
      {
        this.splitContainer1.Panel2.Controls.Clear();
        this.DatImageViewer.Dispose();
        this.DatImageViewer = null;
      }

      // 清理資料
      this._DatFiles = null;
      this._DatFileObjects = null;
      this._AllDatEntries = null;
      this._FilteredDatEntries = null;
      this._DatGroups = null;
      this._DatGroupKeys = null;

      // 還原原始 UI
      this.splitContainer2.Panel2.Controls.Add(this.lvIndexInfo);
      this.lvIndexInfo.Dock = DockStyle.Fill;

      this.splitContainer1.Panel2.Controls.Add(this.TextViewer);
      this.splitContainer1.Panel2.Controls.Add(this.ImageViewer);
      this.splitContainer1.Panel2.Controls.Add(this.SprViewer);
      this.splitContainer1.Panel2.Controls.Add(this.TextCompViewer);
    }

    /// <summary>
    /// 退出 Sprite 模式 (如果存在的話)
    /// </summary>
    private void ExitSpriteMode()
    {
      if (!this._IsSpriteMode)
        return;

      this._IsSpriteMode = false;
      this.chkSpriteMode.Checked = false;
      RemoveSpriteModeTab();
    }

    /// <summary>
    /// DAT 模式篩選
    /// </summary>
    private void FilterDatEntries(string searchText)
    {
      if (this._AllDatEntries == null)
        return;

      if (string.IsNullOrEmpty(searchText))
      {
        // 顯示全部
        this._FilteredDatEntries = new List<DatTools.DatIndexEntry>(this._AllDatEntries);
      }
      else if (searchText.StartsWith("^"))
      {
        // 開頭比對
        string pattern = searchText.Substring(1);
        this._FilteredDatEntries = this._AllDatEntries
          .Where(e => e.Path.StartsWith(pattern, StringComparison.CurrentCultureIgnoreCase))
          .ToList();
      }
      else
      {
        // 包含比對
        this._FilteredDatEntries = this._AllDatEntries
          .Where(e => e.Path.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0)
          .ToList();
      }

      this.lvDatFiles.VirtualListSize = this._FilteredDatEntries.Count;
      this.lvDatFiles.Invalidate();

      if (this._FilteredDatEntries.Count > 0)
      {
        this.lvDatFiles.SelectedIndices.Clear();
        this.lvDatFiles.SelectedIndices.Add(0);
      }

      this.lvDatFiles.Focus();
      this.tssRecordCount.Text = $"全部：{this._AllDatEntries.Count}";
      this.tssShowInListView.Text = $"顯示：{this._FilteredDatEntries.Count}";
    }

    /// <summary>
    /// DAT 檔案列表虛擬項目檢索
    /// </summary>
    private void lvDatFiles_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
      // 建立空白項目的輔助方法
      ListViewItem createEmptyItem()
      {
        var listItem = new ListViewItem("");
        listItem.SubItems.Add("");
        listItem.SubItems.Add("");
        return listItem;
      }

      if (this._FilteredDatEntries == null || e.ItemIndex < 0 || e.ItemIndex >= this._FilteredDatEntries.Count)
      {
        e.Item = createEmptyItem();
        return;
      }

      try
      {
        var entry = this._FilteredDatEntries[e.ItemIndex];
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

    /// <summary>
    /// DAT 檔案列表選擇變更
    /// </summary>
    private void lvDatFiles_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lvDatFiles.SelectedIndices.Count == 0)
        return;

      int selectedIndex = this.lvDatFiles.SelectedIndices[0];
      if (this._FilteredDatEntries == null || selectedIndex >= this._FilteredDatEntries.Count)
        return;

      var entry = this._FilteredDatEntries[selectedIndex];
      DisplayDatEntry(entry);
    }

    /// <summary>
    /// 顯示 DAT 條目內容
    /// </summary>
    private void DisplayDatEntry(DatTools.DatIndexEntry entry)
    {
      try
      {
        // 找到對應的 DatFile
        var datFile = this._DatFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
        if (datFile == null)
          return;

        byte[] data = datFile.ExtractFile(entry);

        string ext = Path.GetExtension(entry.Path).ToLower();
        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
        {
          // 顯示圖片
          using (var ms = new MemoryStream(data))
          {
            var img = Image.FromStream(ms);
            this.DatImageViewer.Image = new Bitmap(img);
          }
        }
        else
        {
          // 非圖片檔案，清空顯示
          this.DatImageViewer.Image = null;
        }

        this.tssMessage.Text = $"{entry.Path} ({FormatFileSize(entry.Size)})";
      }
      catch (Exception ex)
      {
        this.tssMessage.Text = $"載入失敗: {ex.Message}";
        this.DatImageViewer.Image = null;
      }
    }

    /// <summary>
    /// 匯出所有 DAT 為 ZIP
    /// </summary>
    private void ExportAllDatToZip()
    {
      if (this._AllDatEntries == null || this._AllDatEntries.Count == 0)
      {
        MessageBox.Show("沒有可匯出的檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      using (var dlg = new SaveFileDialog())
      {
        dlg.Title = "匯出所有 DAT 為 ZIP";
        dlg.Filter = "ZIP 檔案 (*.zip)|*.zip";
        dlg.FileName = "LineageM_Export.zip";

        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        this.Cursor = Cursors.WaitCursor;
        this.tssProgressName.Text = "匯出中...";
        this.tssProgress.Value = 0;
        this.tssProgress.Maximum = this._AllDatEntries.Count;

        try
        {
          using (var zipStream = new FileStream(dlg.FileName, FileMode.Create))
          using (var archive = new System.IO.Compression.ZipArchive(zipStream, ZipArchiveMode.Create))
          {
            foreach (var entry in this._AllDatEntries)
            {
              var datFile = this._DatFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
              if (datFile == null) continue;

              try
              {
                byte[] data = datFile.ExtractFile(entry);
                // 使用 DAT 檔名 + 內部路徑避免衝突
                string entryPath = entry.SourceDatName + "/" + entry.Path.TrimStart('/', '\\').Replace('\\', '/');
                var zipEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

                using (var entryStream = zipEntry.Open())
                {
                  entryStream.Write(data, 0, data.Length);
                }
              }
              catch { }

              this.tssProgress.Value++;
              if (this.tssProgress.Value % 100 == 0)
                Application.DoEvents();
            }
          }

          MessageBox.Show($"匯出完成！\n共 {this._AllDatEntries.Count} 個檔案。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
          this.Cursor = Cursors.Default;
          this.tssProgressName.Text = "";
          this.tssProgress.Value = 0;
        }
      }
    }

    /// <summary>
    /// 匯出選中的 DAT 檔案為 PNG
    /// </summary>
    private void ExportDatFilesAsPng()
    {
      if (this.lvDatFiles.SelectedIndices.Count == 0)
      {
        MessageBox.Show("請先選擇要匯出的檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      // 收集選中的條目
      var selectedEntries = new List<DatTools.DatIndexEntry>();
      foreach (int idx in this.lvDatFiles.SelectedIndices)
      {
        if (idx >= 0 && idx < this._FilteredDatEntries.Count)
          selectedEntries.Add(this._FilteredDatEntries[idx]);
      }

      if (selectedEntries.Count == 0)
        return;

      using (var dlg = new FolderBrowserDialog())
      {
        dlg.Description = "選擇匯出目錄";
        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        this.Cursor = Cursors.WaitCursor;
        this.tssProgressName.Text = "匯出中...";
        this.tssProgress.Value = 0;
        this.tssProgress.Maximum = selectedEntries.Count;

        int success = 0;
        try
        {
          foreach (var entry in selectedEntries)
          {
            var datFile = this._DatFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
            if (datFile == null) continue;

            try
            {
              byte[] data = datFile.ExtractFile(entry);
              string ext = Path.GetExtension(entry.Path).ToLower();

              // 只處理圖片檔案
              if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
              {
                string fileName = Path.GetFileNameWithoutExtension(entry.Path) + ".png";
                string destPath = Path.Combine(dlg.SelectedPath, fileName);

                // 避免檔名衝突
                int counter = 1;
                while (File.Exists(destPath))
                {
                  fileName = Path.GetFileNameWithoutExtension(entry.Path) + $"_{counter}.png";
                  destPath = Path.Combine(dlg.SelectedPath, fileName);
                  counter++;
                }

                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms))
                {
                  img.Save(destPath, ImageFormat.Png);
                }
                success++;
              }
              else
              {
                // 非圖片檔案，直接儲存原始格式
                string fileName = Path.GetFileName(entry.Path);
                string destPath = Path.Combine(dlg.SelectedPath, fileName);

                int counter = 1;
                while (File.Exists(destPath))
                {
                  fileName = Path.GetFileNameWithoutExtension(entry.Path) + $"_{counter}" + ext;
                  destPath = Path.Combine(dlg.SelectedPath, fileName);
                  counter++;
                }

                File.WriteAllBytes(destPath, data);
                success++;
              }
            }
            catch { }

            this.tssProgress.Value++;
            Application.DoEvents();
          }

          MessageBox.Show($"匯出完成！\n成功：{success} 個檔案。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
          this.Cursor = Cursors.Default;
          this.tssProgressName.Text = "";
          this.tssProgress.Value = 0;
        }
      }
    }

    /// <summary>
    /// 匯出選中的 DAT 檔案 (原始格式)
    /// </summary>
    private void ExportDatFilesOriginal()
    {
      if (this.lvDatFiles.SelectedIndices.Count == 0)
      {
        MessageBox.Show("請先選擇要匯出的檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      // 收集選中的條目
      var selectedEntries = new List<DatTools.DatIndexEntry>();
      foreach (int idx in this.lvDatFiles.SelectedIndices)
      {
        if (idx >= 0 && idx < this._FilteredDatEntries.Count)
          selectedEntries.Add(this._FilteredDatEntries[idx]);
      }

      if (selectedEntries.Count == 0)
        return;

      using (var dlg = new FolderBrowserDialog())
      {
        dlg.Description = "選擇匯出目錄";
        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        this.Cursor = Cursors.WaitCursor;
        this.tssProgressName.Text = "匯出中...";
        this.tssProgress.Value = 0;
        this.tssProgress.Maximum = selectedEntries.Count;

        int success = 0;
        try
        {
          foreach (var entry in selectedEntries)
          {
            var datFile = this._DatFileObjects.FirstOrDefault(d => d.FilePath == entry.SourceDatFile);
            if (datFile == null) continue;

            try
            {
              byte[] data = datFile.ExtractFile(entry);
              string safePath = entry.Path.TrimStart('/', '\\');
              string destPath = Path.Combine(dlg.SelectedPath, safePath);
              Directory.CreateDirectory(Path.GetDirectoryName(destPath));
              File.WriteAllBytes(destPath, data);
              success++;
            }
            catch { }

            this.tssProgress.Value++;
            Application.DoEvents();
          }

          MessageBox.Show($"匯出完成！\n成功：{success} 個檔案。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
          this.Cursor = Cursors.Default;
          this.tssProgressName.Text = "";
          this.tssProgress.Value = 0;
        }
      }
    }

    /// <summary>
    /// 格式化檔案大小
    /// </summary>
    private string FormatFileSize(long bytes)
    {
      if (bytes < 1024)
        return $"{bytes} B";
      else if (bytes < 1024 * 1024)
        return $"{bytes / 1024.0:F1} KB";
      else
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    // ============================================================================
    // 結束 DAT 模式相關方法
    // ============================================================================

    private enum InviewDataType
    {
      Empty,
      Text,
      IMG,
      BMP,
      SPR,
      TIL,
      TBT,
      SprList,  // SPR 列表模式
    }

    // Sprite 分組類別
    private class SpriteGroup
    {
      public string Prefix { get; set; }  // 例如 "dragon-"
      public List<int> ItemIndexes { get; set; }  // 對應 _IndexRecords 的索引
      public long TotalSize { get; set; }
      public bool IsExpanded { get; set; }

      public SpriteGroup(string prefix)
      {
        this.Prefix = prefix;
        this.ItemIndexes = new List<int>();
        this.TotalSize = 0;
        this.IsExpanded = false;
      }

      // 從檔名取得後綴數字用於排序
      public static int GetSuffixNumber(string fileName)
      {
        // 檔名格式: prefix-suffix.spr, 例如 dragon-001.spr
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        int lastDash = nameWithoutExt.LastIndexOf('-');
        if (lastDash >= 0 && lastDash < nameWithoutExt.Length - 1)
        {
          string suffix = nameWithoutExt.Substring(lastDash + 1);
          if (int.TryParse(suffix, out int num))
            return num;
        }
        return int.MaxValue; // 無法解析的放最後
      }

      // 從前綴取得數字用於群組排序 (例如 "1234-" -> 1234)
      public static int GetPrefixNumber(string prefix)
      {
        // 移除結尾的 dash
        string cleanPrefix = prefix.TrimEnd('-');
        if (int.TryParse(cleanPrefix, out int num))
          return num;
        return -1; // 無法解析的返回 -1
      }

      // 比較兩個前綴，數字優先排序
      public static int ComparePrefixes(string prefixA, string prefixB)
      {
        int numA = GetPrefixNumber(prefixA);
        int numB = GetPrefixNumber(prefixB);

        // 兩個都是數字，用數字比較
        if (numA >= 0 && numB >= 0)
          return numA.CompareTo(numB);

        // 只有 A 是數字，A 排前面
        if (numA >= 0)
          return -1;

        // 只有 B 是數字，B 排前面
        if (numB >= 0)
          return 1;

        // 都不是數字，用字串比較
        return string.Compare(prefixA, prefixB, StringComparison.OrdinalIgnoreCase);
      }
    }
  }
}
