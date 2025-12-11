using PakViewer.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PakViewer
{
  /// <summary>
  /// SPR 模式的 detail 檢視器
  /// 左側顯示檔案清單，右側顯示預覽圖
  /// </summary>
  public class ucSprDetailViewer : UserControl
  {
    private SplitContainer splitMain;
    private ListView lvFiles;
    private Panel panelPreview;
    private Panel panelCanvas;  // 繪製區外框
    private PictureBox pictureBox;
    private Label lblInfo;
    private TrackBar tbScale;
    private Timer timerAnim;
    private IContainer components;

    private L1Spr.Frame[] _Frames;
    private int _CurrentFrameIndex;
    private int _MinXOffset;
    private int _MinYOffset;

    // 資料提供者 delegate
    private Func<string, byte[]> _SpriteDataProvider;

    // 目前選中的群組
    private string _CurrentGroupPrefix;
    private List<SprFileInfo> _CurrentFiles;

    public class SprFileInfo
    {
      public int RealIndex { get; set; }
      public string FileName { get; set; }
      public long FileSize { get; set; }
      public long Offset { get; set; }
      public string SourcePak { get; set; }
    }

    public ucSprDetailViewer()
    {
      InitializeComponent();
      this._CurrentFiles = new List<SprFileInfo>();
    }

    private void InitializeComponent()
    {
      this.components = new Container();

      // Main split container (左邊檔案列表，右邊預覽)
      this.splitMain = new SplitContainer();
      this.splitMain.Dock = DockStyle.Fill;
      this.splitMain.Orientation = Orientation.Vertical;
      this.splitMain.SplitterDistance = 120;  // 檔案列表寬度 (縮小)
      this.splitMain.FixedPanel = FixedPanel.Panel1;  // 固定左側寬度

      // Left: File list
      this.lvFiles = new ListView();
      this.lvFiles.Dock = DockStyle.Fill;
      this.lvFiles.View = View.Details;
      this.lvFiles.FullRowSelect = true;
      this.lvFiles.GridLines = true;
      this.lvFiles.HideSelection = false;
      this.lvFiles.Columns.Add("檔案", 80, HorizontalAlignment.Left);
      this.lvFiles.Columns.Add("大小", 50, HorizontalAlignment.Right);
      this.lvFiles.SelectedIndexChanged += lvFiles_SelectedIndexChanged;

      // Right: Preview panel (外層容器)
      this.panelPreview = new Panel();
      this.panelPreview.Dock = DockStyle.Fill;
      this.panelPreview.BackColor = Color.FromArgb(50, 50, 50);
      this.panelPreview.Padding = new Padding(5);

      // Canvas panel (繪製區，帶灰色外框)
      this.panelCanvas = new Panel();
      this.panelCanvas.Location = new Point(50, 5);
      this.panelCanvas.Size = new Size(300, 300);  // 200 * 1.5 = 300
      this.panelCanvas.BackColor = Color.FromArgb(30, 30, 30);
      this.panelCanvas.BorderStyle = BorderStyle.FixedSingle;  // 灰色外框

      // Scale trackbar (放在左側)
      this.tbScale = new TrackBar();
      this.tbScale.Orientation = Orientation.Vertical;
      this.tbScale.Minimum = 1;
      this.tbScale.Maximum = 8;
      this.tbScale.Value = 2;
      this.tbScale.LargeChange = 2;
      this.tbScale.Location = new Point(5, 5);
      this.tbScale.Size = new Size(40, 120);
      this.tbScale.BackColor = Color.FromArgb(60, 60, 60);
      this.tbScale.ValueChanged += tbScale_ValueChanged;

      // Picture box (在 canvas 內)
      this.pictureBox = new PictureBox();
      this.pictureBox.Location = new Point(10, 10);
      this.pictureBox.Size = new Size(100, 100);
      this.pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
      this.pictureBox.BackColor = Color.Transparent;

      // Info label (在 canvas 下方)
      this.lblInfo = new Label();
      this.lblInfo.AutoSize = true;
      this.lblInfo.ForeColor = Color.LightGray;
      this.lblInfo.BackColor = Color.Transparent;
      this.lblInfo.Location = new Point(50, 310);  // 300 + 5 + 5
      this.lblInfo.Font = new Font("Consolas", 9f);

      // Animation timer
      this.timerAnim = new Timer(this.components);
      this.timerAnim.Interval = 150;
      this.timerAnim.Tick += timerAnim_Tick;

      // Assemble
      this.panelCanvas.Controls.Add(this.pictureBox);

      this.panelPreview.Controls.Add(this.panelCanvas);
      this.panelPreview.Controls.Add(this.tbScale);
      this.panelPreview.Controls.Add(this.lblInfo);

      this.splitMain.Panel1.Controls.Add(this.lvFiles);
      this.splitMain.Panel2.Controls.Add(this.panelPreview);

      this.Controls.Add(this.splitMain);

      // 監聽大小變化以調整 canvas 位置
      this.panelPreview.Resize += panelPreview_Resize;

      this.Name = "ucSprDetailViewer";
      this.Size = new Size(400, 300);
    }

    private void panelPreview_Resize(object sender, EventArgs e)
    {
      // 調整 lblInfo 位置 (在 canvas 下方)
      this.lblInfo.Top = this.panelCanvas.Bottom + 5;
    }

    public void SetSpriteDataProvider(Func<string, byte[]> provider)
    {
      this._SpriteDataProvider = provider;
    }

    /// <summary>
    /// 設定要顯示的群組
    /// </summary>
    public void ShowGroup(string prefix, List<SprFileInfo> files)
    {
      this.timerAnim.Stop();
      this._CurrentGroupPrefix = prefix;
      this._CurrentFiles = files ?? new List<SprFileInfo>();
      this._Frames = null;

      // 更新檔案清單
      this.lvFiles.BeginUpdate();
      this.lvFiles.Items.Clear();
      foreach (var file in this._CurrentFiles)
      {
        var item = new ListViewItem(file.FileName);
        item.SubItems.Add(string.Format("{0:F1}", file.FileSize / 1024.0));
        item.Tag = file;
        this.lvFiles.Items.Add(item);
      }
      this.lvFiles.EndUpdate();

      // 預設選第一個
      if (this.lvFiles.Items.Count > 0)
      {
        this.lvFiles.Items[0].Selected = true;
        this.lvFiles.Items[0].Focused = true;
      }
      else
      {
        ClearPreview();
      }
    }

    public void Clear()
    {
      this.timerAnim.Stop();
      this._CurrentGroupPrefix = null;
      this._CurrentFiles.Clear();
      this._Frames = null;
      this.lvFiles.Items.Clear();
      ClearPreview();
    }

    private void ClearPreview()
    {
      this.pictureBox.Image = null;
      this.lblInfo.Text = "";
    }

    private void lvFiles_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lvFiles.SelectedItems.Count == 0)
      {
        ClearPreview();
        return;
      }

      var selected = this.lvFiles.SelectedItems[0];
      var fileInfo = selected.Tag as SprFileInfo;
      if (fileInfo == null || this._SpriteDataProvider == null)
      {
        ClearPreview();
        return;
      }

      LoadAndShowSpr(fileInfo);
    }

    private void LoadAndShowSpr(SprFileInfo fileInfo)
    {
      this.timerAnim.Stop();

      try
      {
        // 取得 sprite key
        string key = fileInfo.SourcePak + "|" + fileInfo.FileName;
        byte[] data = this._SpriteDataProvider(key);
        if (data == null || data.Length == 0)
        {
          this.lblInfo.Text = "無法載入資料";
          ClearPreview();
          return;
        }

        // 解析 SPR
        this._Frames = L1Spr.Load(data);
        if (this._Frames == null || this._Frames.Length == 0)
        {
          this.lblInfo.Text = "SPR 解析失敗";
          ClearPreview();
          return;
        }

        // 計算偏移
        this._MinXOffset = int.MaxValue;
        this._MinYOffset = int.MaxValue;
        this._CurrentFrameIndex = 0;

        for (int i = 0; i < this._Frames.Length; i++)
        {
          var frame = this._Frames[i];
          if (frame.image != null)
          {
            if (this._CurrentFrameIndex == 0 && this.pictureBox.Image == null)
              this._CurrentFrameIndex = i;
            if (frame.x_offset < this._MinXOffset)
              this._MinXOffset = frame.x_offset;
            if (frame.y_offset < this._MinYOffset)
              this._MinYOffset = frame.y_offset;
          }
        }

        // 顯示第一幀
        ShowFrame(this._Frames[this._CurrentFrameIndex]);

        // 更新資訊
        this.lblInfo.Text = string.Format("{0}\n{1} frames, Mask: 0x{2:X4}",
          fileInfo.FileName,
          this._Frames.Length,
          this._Frames[0].maskcolor);

        // 啟動動畫
        if (this._Frames.Length > 1)
          this.timerAnim.Start();
      }
      catch (Exception ex)
      {
        this.lblInfo.Text = "錯誤: " + ex.Message;
        ClearPreview();
      }
    }

    private void ShowFrame(L1Spr.Frame frame)
    {
      if (frame.image == null)
        return;

      int scale = this.tbScale.Value;
      int padding = 10;  // 內邊距

      this.pictureBox.Width = frame.width * scale / 2;
      this.pictureBox.Height = frame.height * scale / 2;
      this.pictureBox.Top = padding + (frame.y_offset - this._MinYOffset) * scale / 2;
      this.pictureBox.Left = padding + (frame.x_offset - this._MinXOffset) * scale / 2;
      this.pictureBox.Image = frame.image;

      // 計算需要的畫布大小
      int requiredWidth = this.pictureBox.Right + padding;
      int requiredHeight = this.pictureBox.Bottom + padding;

      // 最小尺寸 300x300，如果需要更大則自動擴展
      int minSize = 300;
      int newWidth = Math.Max(minSize, requiredWidth);
      int newHeight = Math.Max(minSize, requiredHeight);

      // 如果需要擴展，更新畫布大小
      if (newWidth != this.panelCanvas.Width || newHeight != this.panelCanvas.Height)
      {
        this.panelCanvas.Size = new Size(newWidth, newHeight);
        // 更新 lblInfo 位置
        this.lblInfo.Top = this.panelCanvas.Bottom + 5;
      }
    }

    private void timerAnim_Tick(object sender, EventArgs e)
    {
      if (this._Frames == null || this._Frames.Length == 0)
        return;

      this._CurrentFrameIndex = (this._CurrentFrameIndex + 1) % this._Frames.Length;
      ShowFrame(this._Frames[this._CurrentFrameIndex]);
    }

    private void tbScale_ValueChanged(object sender, EventArgs e)
    {
      if (this._Frames != null && this._Frames.Length > 0)
        ShowFrame(this._Frames[this._CurrentFrameIndex]);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        this.timerAnim?.Stop();
        this.components?.Dispose();
      }
      base.Dispose(disposing);
    }
  }
}
