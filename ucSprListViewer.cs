using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using PakViewer.Models;
using PakViewer.Utility;

namespace PakViewer
{
    /// <summary>
    /// SPR List 檢視器 - 顯示 list.spr 的內容
    /// </summary>
    public class ucSprListViewer : UserControl
    {
        private SprListFile _sprListFile;
        private SprListEntry _selectedEntry;
        private SprAction _playingAction;
        private int _playingFrameIndex;
        private List<SprListEntry> _filteredEntries;

        // UI 元件
        private SplitContainer splitMain;
        private Panel panelLeft;
        private Panel panelRight;
        private Panel panelToolbar;
        private TextBox txtSearch;
        private ComboBox cmbTypeFilter;
        private Label lblSearch;
        private Label lblTypeFilter;
        private ListView lvEntries;
        private Panel panelEntryInfo;
        private Label lblEntryInfo;
        private FlowLayoutPanel flowActions;
        private Panel panelPreview;
        private PictureBox picPreview;
        private Label lblPreviewInfo;
        private Timer timerAnimation;
        private Button btnStopAll;

        // 動畫相關
        private L1Spr.Frame[] _currentFrames;
        private Func<int, byte[]> _getSpriteDataFunc;

        public ucSprListViewer()
        {
            InitializeComponent();
            _filteredEntries = new List<SprListEntry>();
        }

        /// <summary>
        /// 設定取得 SPR 資料的函數
        /// </summary>
        public void SetSpriteDataProvider(Func<int, byte[]> getSpriteDataFunc)
        {
            _getSpriteDataFunc = getSpriteDataFunc;
        }

        /// <summary>
        /// 載入 SPR List 檔案
        /// </summary>
        public void LoadSprList(SprListFile sprListFile)
        {
            _sprListFile = sprListFile;
            _filteredEntries = sprListFile?.Entries ?? new List<SprListEntry>();
            RefreshEntryList();
        }

        /// <summary>
        /// 從檔案載入
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            var sprListFile = SprListParser.LoadFromFile(filePath);
            LoadSprList(sprListFile);
        }

        /// <summary>
        /// 從 byte[] 載入
        /// </summary>
        public void LoadFromBytes(byte[] data)
        {
            var sprListFile = SprListParser.LoadFromBytes(data);
            LoadSprList(sprListFile);
        }

        private void InitializeComponent()
        {
            this.splitMain = new SplitContainer();
            this.panelLeft = new Panel();
            this.panelRight = new Panel();
            this.panelToolbar = new Panel();
            this.txtSearch = new TextBox();
            this.cmbTypeFilter = new ComboBox();
            this.lblSearch = new Label();
            this.lblTypeFilter = new Label();
            this.lvEntries = new ListView();
            this.panelEntryInfo = new Panel();
            this.lblEntryInfo = new Label();
            this.flowActions = new FlowLayoutPanel();
            this.panelPreview = new Panel();
            this.picPreview = new PictureBox();
            this.lblPreviewInfo = new Label();
            this.timerAnimation = new Timer();
            this.btnStopAll = new Button();

            ((ISupportInitialize)this.splitMain).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.panelToolbar.SuspendLayout();
            this.panelEntryInfo.SuspendLayout();
            this.panelPreview.SuspendLayout();
            ((ISupportInitialize)this.picPreview).BeginInit();
            this.SuspendLayout();

            // splitMain
            this.splitMain.Dock = DockStyle.Fill;
            this.splitMain.Location = new Point(0, 0);
            this.splitMain.Name = "splitMain";
            this.splitMain.Panel1.Controls.Add(this.panelLeft);
            this.splitMain.Panel2.Controls.Add(this.panelRight);
            this.splitMain.Size = new Size(1000, 600);
            this.splitMain.SplitterDistance = 300;
            this.splitMain.TabIndex = 0;

            // panelLeft
            this.panelLeft.Controls.Add(this.lvEntries);
            this.panelLeft.Controls.Add(this.panelEntryInfo);
            this.panelLeft.Controls.Add(this.panelToolbar);
            this.panelLeft.Dock = DockStyle.Fill;
            this.panelLeft.Name = "panelLeft";

            // panelToolbar
            this.panelToolbar.Dock = DockStyle.Top;
            this.panelToolbar.Height = 70;
            this.panelToolbar.Controls.Add(this.lblSearch);
            this.panelToolbar.Controls.Add(this.txtSearch);
            this.panelToolbar.Controls.Add(this.lblTypeFilter);
            this.panelToolbar.Controls.Add(this.cmbTypeFilter);
            this.panelToolbar.Padding = new Padding(5);

            // lblSearch
            this.lblSearch.Text = "搜尋:";
            this.lblSearch.Location = new Point(8, 10);
            this.lblSearch.AutoSize = true;

            // txtSearch
            this.txtSearch.Location = new Point(50, 7);
            this.txtSearch.Size = new Size(200, 23);
            this.txtSearch.TextChanged += TxtSearch_TextChanged;

            // lblTypeFilter
            this.lblTypeFilter.Text = "類型:";
            this.lblTypeFilter.Location = new Point(8, 40);
            this.lblTypeFilter.AutoSize = true;

            // cmbTypeFilter
            this.cmbTypeFilter.Location = new Point(50, 37);
            this.cmbTypeFilter.Size = new Size(120, 23);
            this.cmbTypeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbTypeFilter.Items.AddRange(new object[] {
                "全部",
                "玩家角色 (5)",
                "怪物/NPC (10)",
                "物品 (9)",
                "特效/影子 (0)",
                "門 (8)",
                "告示牌 (4)",
                "載具 (6)",
                "女僕 (12)"
            });
            this.cmbTypeFilter.SelectedIndex = 0;
            this.cmbTypeFilter.SelectedIndexChanged += CmbTypeFilter_SelectedIndexChanged;

            // lvEntries
            this.lvEntries.Dock = DockStyle.Fill;
            this.lvEntries.View = View.Details;
            this.lvEntries.FullRowSelect = true;
            this.lvEntries.GridLines = true;
            this.lvEntries.Columns.Add("ID", 50, HorizontalAlignment.Right);
            this.lvEntries.Columns.Add("名稱", 130, HorizontalAlignment.Left);
            this.lvEntries.Columns.Add("圖數", 50, HorizontalAlignment.Right);
            this.lvEntries.Columns.Add("類型", 60, HorizontalAlignment.Left);
            this.lvEntries.SelectedIndexChanged += LvEntries_SelectedIndexChanged;
            // 啟用虛擬模式以提升效能
            this.lvEntries.VirtualMode = true;
            this.lvEntries.RetrieveVirtualItem += LvEntries_RetrieveVirtualItem;

            // panelEntryInfo
            this.panelEntryInfo.Dock = DockStyle.Bottom;
            this.panelEntryInfo.Height = 100;
            this.panelEntryInfo.BorderStyle = BorderStyle.FixedSingle;
            this.panelEntryInfo.Controls.Add(this.lblEntryInfo);
            this.panelEntryInfo.BackColor = Color.FromArgb(240, 240, 245);

            // lblEntryInfo
            this.lblEntryInfo.Dock = DockStyle.Fill;
            this.lblEntryInfo.Padding = new Padding(5);
            this.lblEntryInfo.Text = "選擇一個條目以檢視詳細資訊";

            // panelRight
            this.panelRight.Dock = DockStyle.Fill;
            this.panelRight.Controls.Add(this.flowActions);
            this.panelRight.Controls.Add(this.panelPreview);

            // panelPreview
            this.panelPreview.Dock = DockStyle.Bottom;
            this.panelPreview.Height = 250;
            this.panelPreview.BorderStyle = BorderStyle.FixedSingle;
            this.panelPreview.BackColor = Color.FromArgb(80, 80, 80);
            this.panelPreview.Controls.Add(this.picPreview);
            this.panelPreview.Controls.Add(this.lblPreviewInfo);
            this.panelPreview.Controls.Add(this.btnStopAll);

            // picPreview
            this.picPreview.Location = new Point(100, 20);
            this.picPreview.Size = new Size(200, 200);
            this.picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            this.picPreview.BackColor = Color.Transparent;

            // lblPreviewInfo
            this.lblPreviewInfo.AutoSize = true;
            this.lblPreviewInfo.Location = new Point(10, 10);
            this.lblPreviewInfo.ForeColor = Color.White;
            this.lblPreviewInfo.Text = "動畫預覽";

            // btnStopAll
            this.btnStopAll.Text = "停止播放";
            this.btnStopAll.Location = new Point(10, 35);
            this.btnStopAll.Size = new Size(80, 25);
            this.btnStopAll.Click += BtnStopAll_Click;

            // flowActions
            this.flowActions.Dock = DockStyle.Fill;
            this.flowActions.AutoScroll = true;
            this.flowActions.FlowDirection = FlowDirection.TopDown;
            this.flowActions.WrapContents = false;
            this.flowActions.BackColor = Color.FromArgb(250, 250, 252);

            // timerAnimation
            this.timerAnimation.Interval = 100;
            this.timerAnimation.Tick += TimerAnimation_Tick;

            // ucSprListViewer
            this.Controls.Add(this.splitMain);
            this.Name = "ucSprListViewer";
            this.Size = new Size(1000, 600);

            ((ISupportInitialize)this.splitMain).EndInit();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            this.splitMain.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelToolbar.ResumeLayout(false);
            this.panelToolbar.PerformLayout();
            this.panelEntryInfo.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            this.panelPreview.PerformLayout();
            ((ISupportInitialize)this.picPreview).EndInit();
            this.ResumeLayout(false);
        }

        private void RefreshEntryList()
        {
            if (_sprListFile == null)
            {
                lvEntries.VirtualListSize = 0;
                return;
            }

            // 套用篩選
            var searchText = txtSearch.Text.ToLower();
            var typeFilter = GetSelectedTypeFilter();

            _filteredEntries = _sprListFile.Entries.Where(e =>
            {
                // 搜尋篩選
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!e.Name.ToLower().Contains(searchText) &&
                        !e.Id.ToString().Contains(searchText))
                        return false;
                }

                // 類型篩選
                if (typeFilter.HasValue && e.TypeId != typeFilter.Value)
                    return false;

                return true;
            }).ToList();

            lvEntries.VirtualListSize = _filteredEntries.Count;
            lvEntries.Invalidate();
        }

        private int? GetSelectedTypeFilter()
        {
            switch (cmbTypeFilter.SelectedIndex)
            {
                case 1: return 5;   // 玩家角色
                case 2: return 10;  // 怪物/NPC
                case 3: return 9;   // 物品
                case 4: return 0;   // 特效/影子
                case 5: return 8;   // 門
                case 6: return 4;   // 告示牌
                case 7: return 6;   // 載具
                case 8: return 12;  // 女僕
                default: return null;
            }
        }

        private void LvEntries_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _filteredEntries.Count)
            {
                e.Item = new ListViewItem();
                return;
            }

            var entry = _filteredEntries[e.ItemIndex];
            var item = new ListViewItem(entry.Id.ToString());
            item.SubItems.Add(entry.Name);
            item.SubItems.Add(entry.ImageCount.ToString());
            item.SubItems.Add(entry.TypeName);
            item.Tag = entry;
            e.Item = item;
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            RefreshEntryList();
        }

        private void CmbTypeFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshEntryList();
        }

        private void LvEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvEntries.SelectedIndices.Count == 0)
            {
                _selectedEntry = null;
                lblEntryInfo.Text = "選擇一個條目以檢視詳細資訊";
                flowActions.Controls.Clear();
                return;
            }

            var index = lvEntries.SelectedIndices[0];
            if (index < 0 || index >= _filteredEntries.Count)
                return;

            _selectedEntry = _filteredEntries[index];
            ShowEntryDetails(_selectedEntry);
        }

        private void ShowEntryDetails(SprListEntry entry)
        {
            // 更新條目資訊
            var info = $"#{entry.Id} {entry.Name}\n";
            info += $"圖片數量: {entry.ImageCount}\n";
            info += $"類型: {entry.TypeName}\n";
            if (entry.LinkedId.HasValue)
                info += $"關聯ID: #{entry.LinkedId}\n";
            if (entry.ShadowId.HasValue)
                info += $"影子: #{entry.ShadowId}\n";
            info += $"動作數: {entry.Actions.Count}";

            lblEntryInfo.Text = info;

            // 顯示動作列表
            ShowActions(entry);
        }

        private void ShowActions(SprListEntry entry)
        {
            flowActions.SuspendLayout();
            flowActions.Controls.Clear();

            foreach (var action in entry.Actions)
            {
                var actionPanel = CreateActionPanel(entry, action);
                flowActions.Controls.Add(actionPanel);
            }

            flowActions.ResumeLayout();
        }

        private Panel CreateActionPanel(SprListEntry entry, SprAction action)
        {
            var panel = new Panel
            {
                Width = flowActions.Width - 30,
                Height = 90,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(5),
                BackColor = Color.White,
                Tag = action
            };

            // 標題列
            var lblTitle = new Label
            {
                Text = $"{action.DisplayName} - {action.ActionTypeName}",
                Location = new Point(5, 5),
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);

            // 資訊
            var lblInfo = new Label
            {
                Text = $"{(action.IsDirectional ? "有向" : "無向")} | {action.FrameCount}幀 | 總時長:{action.TotalDuration}",
                Location = new Point(5, 25),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font(this.Font.FontFamily, 8)
            };
            panel.Controls.Add(lblInfo);

            // 播放按鈕
            var btnPlay = new Button
            {
                Text = "▶",
                Location = new Point(panel.Width - 45, 5),
                Size = new Size(35, 35),
                Tag = new object[] { entry, action }
            };
            btnPlay.Click += BtnPlay_Click;
            panel.Controls.Add(btnPlay);

            // 幀序列面板
            var framesPanel = new Panel
            {
                Location = new Point(5, 45),
                Size = new Size(panel.Width - 60, 40),
                AutoScroll = true
            };

            int x = 0;
            foreach (var frame in action.Frames)
            {
                var frameBox = CreateFrameBox(frame);
                frameBox.Location = new Point(x, 0);
                framesPanel.Controls.Add(frameBox);
                x += frameBox.Width + 2;
            }

            panel.Controls.Add(framesPanel);

            // 調整面板大小以適應內容
            panel.Resize += (s, e) =>
            {
                btnPlay.Location = new Point(panel.Width - 45, 5);
                framesPanel.Width = panel.Width - 60;
            };

            return panel;
        }

        private Panel CreateFrameBox(SprFrame frame)
        {
            var box = new Panel
            {
                Size = new Size(50, 38),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = GetFrameColor(frame),
                Tag = frame
            };

            // 圖片.幀:時間
            var lblMain = new Label
            {
                Text = $"{frame.ImageId}.{frame.FrameIndex}",
                Location = new Point(2, 2),
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 7)
            };
            box.Controls.Add(lblMain);

            var lblDuration = new Label
            {
                Text = $":{frame.Duration}",
                Location = new Point(2, 14),
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 7)
            };
            box.Controls.Add(lblDuration);

            // 修飾符
            if (frame.HasModifiers)
            {
                var lblMod = new Label
                {
                    Text = GetShortModifiers(frame),
                    Location = new Point(2, 26),
                    AutoSize = true,
                    Font = new Font(this.Font.FontFamily, 6),
                    ForeColor = Color.DarkRed
                };
                box.Controls.Add(lblMod);
            }

            // Tooltip
            var toolTip = new ToolTip();
            toolTip.SetToolTip(box, frame.ToString());

            return box;
        }

        private Color GetFrameColor(SprFrame frame)
        {
            if (frame.TriggerHit)
                return Color.FromArgb(255, 200, 200);  // 淡紅色 - 命中
            if (frame.SoundIds.Count > 0)
                return Color.FromArgb(200, 255, 200);  // 淡綠色 - 聲音
            if (frame.OverlayIds.Count > 0)
                return Color.FromArgb(200, 200, 255);  // 淡藍色 - 疊圖
            if (frame.EffectIds.Count > 0)
                return Color.FromArgb(255, 255, 200);  // 淡黃色 - 特效
            return Color.FromArgb(245, 245, 245);     // 淡灰色 - 一般
        }

        private string GetShortModifiers(SprFrame frame)
        {
            var parts = new List<string>();
            if (frame.TriggerHit) parts.Add("!");
            if (frame.SoundIds.Count > 0) parts.Add($"[{frame.SoundIds.Count}");
            if (frame.OverlayIds.Count > 0) parts.Add($"]{frame.OverlayIds.Count}");
            if (frame.EffectIds.Count > 0) parts.Add($"<{frame.EffectIds.Count}");
            return string.Join(" ", parts);
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var data = (object[])btn.Tag;
            var entry = (SprListEntry)data[0];
            var action = (SprAction)data[1];

            PlayAction(entry, action);
        }

        private void PlayAction(SprListEntry entry, SprAction action)
        {
            // 停止當前播放
            timerAnimation.Stop();

            _playingAction = action;
            _playingFrameIndex = 0;

            // 嘗試載入 SPR 資料
            if (_getSpriteDataFunc != null)
            {
                try
                {
                    var sprData = _getSpriteDataFunc(entry.Id);
                    if (sprData != null)
                    {
                        _currentFrames = L1Spr.Load(sprData);
                    }
                }
                catch (Exception ex)
                {
                    lblPreviewInfo.Text = $"載入錯誤: {ex.Message}";
                }
            }

            if (_currentFrames == null || _currentFrames.Length == 0)
            {
                lblPreviewInfo.Text = $"播放: {action.DisplayName} (無圖片資料)";
                // 仍然播放，只是沒有圖片
            }
            else
            {
                lblPreviewInfo.Text = $"播放: {action.DisplayName}";
            }

            // 開始播放
            timerAnimation.Interval = 100; // 基本間隔
            timerAnimation.Start();
            UpdateAnimationFrame();
        }

        private void TimerAnimation_Tick(object sender, EventArgs e)
        {
            if (_playingAction == null || _playingAction.Frames.Count == 0)
            {
                timerAnimation.Stop();
                return;
            }

            _playingFrameIndex++;
            if (_playingFrameIndex >= _playingAction.Frames.Count)
                _playingFrameIndex = 0;

            UpdateAnimationFrame();
        }

        private void UpdateAnimationFrame()
        {
            if (_playingAction == null || _playingAction.Frames.Count == 0)
                return;

            var frame = _playingAction.Frames[_playingFrameIndex];

            // 更新預覽資訊
            lblPreviewInfo.Text = $"播放: {_playingAction.DisplayName} [{_playingFrameIndex + 1}/{_playingAction.Frames.Count}]\n" +
                                  $"圖片: {frame.ImageId}.{frame.FrameIndex} 持續:{frame.Duration}";

            // 設定下一幀的間隔時間
            timerAnimation.Interval = Math.Max(50, frame.Duration * 30);

            // 顯示圖片
            if (_currentFrames != null)
            {
                // 計算實際的幀索引
                // 根據 list.spr 的定義，ImageId 是圖片組，FrameIndex 是該組中的幀
                int actualIndex = frame.ImageId + frame.FrameIndex;
                if (actualIndex >= 0 && actualIndex < _currentFrames.Length)
                {
                    picPreview.Image = _currentFrames[actualIndex].image;
                }
                else if (frame.ImageId >= 0 && frame.ImageId < _currentFrames.Length)
                {
                    picPreview.Image = _currentFrames[frame.ImageId].image;
                }
            }
        }

        private void BtnStopAll_Click(object sender, EventArgs e)
        {
            timerAnimation.Stop();
            _playingAction = null;
            picPreview.Image = null;
            lblPreviewInfo.Text = "動畫預覽";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerAnimation?.Stop();
                timerAnimation?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
