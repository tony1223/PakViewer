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
    /// SPR 動作檢視器 - 顯示單一條目的所有動作
    /// 點擊動作可展開視覺化 - 一長條顯示每個幀格子
    /// </summary>
    public class ucSprActionViewer : UserControl
    {
        private SprListEntry _currentEntry;
        private Panel _expandedPanel;
        private SprAction _expandedAction;

        // UI 元件
        private Panel panelHeader;
        private Label lblEntryInfo;
        private FlowLayoutPanel flowActions;
        private Timer timerAnimation;

        // 動畫相關
        private L1Spr.Frame[] _currentFrames;
        private Func<string, byte[]> _getSpriteDataFunc;  // 參數是 "spriteId-direction" 格式

        // 展開面板的動畫狀態
        private int _currentFrameIndex;
        private List<Panel> _frameBoxes = new List<Panel>();
        private Panel _highlightBox;
        private int _selectedDirection = 5; // 預設向下 (0=左上, 1=上, 2=右上, 3=右, 4=右下, 5=下, 6=左下, 7=左)
        private List<Button> _directionButtons = new List<Button>();

        // 圖片預覽區
        private PictureBox _previewPictureBox;
        private Panel _previewPanel;
        private Label _previewInfoLabel;
        private const int PREVIEW_HEIGHT = 140;  // 增加高度以容納資訊標籤

        // 常數
        private const int FRAME_BOX_WIDTH = 50;
        private const int FRAME_BOX_HEIGHT = 36;
        private const int FRAME_BOX_MARGIN = 1;

        // 動畫時間參數：1 單位時間 = 1 秒 (可調整)
        private const double UNIT_TIME_MS = 200.0;  // 1 單位 = 1000ms = 1秒

        public ucSprActionViewer()
        {
            InitializeComponent();
        }

        public void SetSpriteDataProvider(Func<string, byte[]> getSpriteDataFunc)
        {
            _getSpriteDataFunc = getSpriteDataFunc;
        }

        public void ShowEntry(SprListEntry entry)
        {
            _currentEntry = entry;
            StopAnimation();
            CollapseExpanded();

            if (entry == null)
            {
                lblEntryInfo.Text = "選擇一個條目以檢視動作";
                flowActions.Controls.Clear();
                return;
            }

            var info = $"#{entry.Id} {entry.Name} | 圖檔:{entry.SpriteId} | 圖數:{entry.ImageCount} | 類型:{entry.TypeName} | 動作:{entry.Actions.Count}";
            if (entry.ShadowId.HasValue) info += $" | 影子:#{entry.ShadowId}";
            lblEntryInfo.Text = info;

            // sprite 資料會在展開動作時根據方向載入
            _currentFrames = null;
            _loadedSpriteKey = null;
            ShowActions(entry);
        }

        public void Clear()
        {
            ShowEntry(null);
        }

        private void InitializeComponent()
        {
            this.panelHeader = new Panel();
            this.lblEntryInfo = new Label();
            this.flowActions = new FlowLayoutPanel();
            this.timerAnimation = new Timer();

            this.panelHeader.SuspendLayout();
            this.SuspendLayout();

            // panelHeader
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Height = 28;
            this.panelHeader.BackColor = Color.FromArgb(60, 60, 65);
            this.panelHeader.Controls.Add(this.lblEntryInfo);
            this.panelHeader.Padding = new Padding(5, 0, 5, 0);

            // lblEntryInfo
            this.lblEntryInfo.Dock = DockStyle.Fill;
            this.lblEntryInfo.ForeColor = Color.White;
            this.lblEntryInfo.Text = "選擇一個條目以檢視動作";
            this.lblEntryInfo.TextAlign = ContentAlignment.MiddleLeft;

            // flowActions
            this.flowActions.Dock = DockStyle.Fill;
            this.flowActions.AutoScroll = true;
            this.flowActions.FlowDirection = FlowDirection.TopDown;
            this.flowActions.WrapContents = false;
            this.flowActions.BackColor = Color.FromArgb(45, 45, 48);
            this.flowActions.Padding = new Padding(3);

            // timerAnimation
            this.timerAnimation.Interval = 100;
            this.timerAnimation.Tick += TimerAnimation_Tick;

            // ucSprActionViewer
            this.Controls.Add(this.flowActions);
            this.Controls.Add(this.panelHeader);
            this.Name = "ucSprActionViewer";
            this.Size = new Size(400, 500);
            this.BackColor = Color.FromArgb(45, 45, 48);

            this.panelHeader.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private string _lastLoadError;  // Debug 用
        private string _loadedSpriteKey;  // 目前載入的 sprite key

        private void LoadSpriteData(int spriteId, int imageId, int direction)
        {
            // sprite 檔案名稱 = spriteId-{imageId + direction}
            // 例如：spriteId=3225, imageId=16, direction=4 → 3225-20.spr
            int subId = imageId + direction;
            string spriteKey = $"{spriteId}-{subId}";

            // 如果已經載入同一個 sprite，不需要重新載入
            if (_loadedSpriteKey == spriteKey && _currentFrames != null)
                return;

            _currentFrames = null;
            _lastLoadError = null;
            _loadedSpriteKey = null;

            if (_getSpriteDataFunc == null)
            {
                _lastLoadError = $"無資料提供者 ({spriteKey})";
                return;
            }

            try
            {
                var sprData = _getSpriteDataFunc(spriteKey);
                if (sprData == null)
                {
                    _lastLoadError = $"找不到 {spriteKey}.spr";
                    return;
                }
                _currentFrames = L1Spr.Load(sprData);
                if (_currentFrames == null || _currentFrames.Length == 0)
                {
                    _lastLoadError = $"{spriteKey}.spr 解析失敗";
                }
                else
                {
                    _loadedSpriteKey = spriteKey;
                }
            }
            catch (Exception ex)
            {
                _lastLoadError = $"載入錯誤: {ex.Message}";
            }
        }

        private void ShowActions(SprListEntry entry)
        {
            flowActions.SuspendLayout();
            flowActions.Controls.Clear();

            int panelWidth = Math.Max(flowActions.ClientSize.Width - 25, 250);

            foreach (var action in entry.Actions)
            {
                var actionPanel = CreateActionPanel(entry, action, panelWidth);
                flowActions.Controls.Add(actionPanel);
            }

            foreach (var attr in entry.Attributes)
            {
                var attrPanel = CreateAttributePanel(attr, panelWidth);
                flowActions.Controls.Add(attrPanel);
            }

            flowActions.ResumeLayout();
        }

        private Panel CreateActionPanel(SprListEntry entry, SprAction action, int width)
        {
            var panel = new Panel
            {
                Width = width,
                Height = 38,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2),
                BackColor = Color.FromArgb(55, 55, 58),
                Tag = new object[] { entry, action, false }
            };

            // 展開/收合按鈕
            var btnExpand = new Button
            {
                Text = "▶",
                Location = new Point(2, 2),
                Size = new Size(24, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.LightGreen,
                Tag = panel
            };
            btnExpand.FlatAppearance.BorderSize = 0;
            btnExpand.Click += BtnExpand_Click;
            panel.Controls.Add(btnExpand);

            // 標題
            var lblTitle = new Label
            {
                Text = $"{action.DisplayName} - {action.ActionTypeName}",
                Location = new Point(28, 2),
                Size = new Size(width - 35, 16),
                Font = new Font(this.Font.FontFamily, 8, FontStyle.Bold),
                ForeColor = Color.White,
                Tag = panel
            };
            lblTitle.Click += LblTitle_Click;
            panel.Controls.Add(lblTitle);

            // 原始文字
            var rawText = action.RawText ?? $"{action.ActionId}.{action.ActionName}(...)";
            var lblRaw = new Label
            {
                Text = rawText.Length > 80 ? rawText.Substring(0, 80) + "..." : rawText,
                Location = new Point(28, 18),
                Size = new Size(width - 35, 16),
                Font = new Font("Consolas", 8),
                ForeColor = Color.LightGreen,
                Tag = panel
            };
            lblRaw.Click += LblTitle_Click;
            panel.Controls.Add(lblRaw);

            var tip = new ToolTip();
            tip.SetToolTip(lblRaw, rawText);

            return panel;
        }

        private Panel CreateAttributePanel(SprAttribute attr, int width)
        {
            var panel = new Panel
            {
                Width = width,
                Height = 24,
                Margin = new Padding(2),
                BackColor = Color.FromArgb(45, 45, 48),
            };

            var lblRaw = new Label
            {
                Text = attr.ToString(),
                Location = new Point(28, 4),
                Size = new Size(width - 35, 16),
                Font = new Font("Consolas", 8),
                ForeColor = Color.Gray
            };
            panel.Controls.Add(lblRaw);

            return panel;
        }

        private void LblTitle_Click(object sender, EventArgs e)
        {
            var lbl = (Label)sender;
            var panel = (Panel)lbl.Tag;
            ToggleExpand(panel);
        }

        private void BtnExpand_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var panel = (Panel)btn.Tag;
            ToggleExpand(panel);
        }

        private void ToggleExpand(Panel panel)
        {
            var data = (object[])panel.Tag;
            var entry = (SprListEntry)data[0];
            var action = (SprAction)data[1];
            var isExpanded = (bool)data[2];

            if (isExpanded)
            {
                CollapsePanel(panel);
            }
            else
            {
                CollapseExpanded();
                ExpandPanel(panel, entry, action);
            }
        }

        private void CollapseExpanded()
        {
            if (_expandedPanel != null)
            {
                CollapsePanel(_expandedPanel);
            }
        }

        private void CollapsePanel(Panel panel)
        {
            StopAnimation();

            var data = (object[])panel.Tag;
            data[2] = false;
            panel.Tag = data;
            panel.Height = 38;

            foreach (Control c in panel.Controls)
            {
                if (c is Button btn && btn.Text == "▼")
                {
                    btn.Text = "▶";
                    break;
                }
            }

            var toRemove = new List<Control>();
            foreach (Control c in panel.Controls)
            {
                if (c.Top >= 38) toRemove.Add(c);
            }
            foreach (var c in toRemove)
            {
                panel.Controls.Remove(c);
                c.Dispose();
            }

            _expandedPanel = null;
            _expandedAction = null;
            _frameBoxes.Clear();
            _highlightBox = null;
            _directionButtons.Clear();
            _previewPictureBox = null;
            _previewPanel = null;
            _previewInfoLabel = null;
        }

        private void ExpandPanel(Panel panel, SprListEntry entry, SprAction action)
        {
            var data = (object[])panel.Tag;
            data[2] = true;
            panel.Tag = data;

            foreach (Control c in panel.Controls)
            {
                if (c is Button btn && btn.Text == "▶")
                {
                    btn.Text = "▼";
                    break;
                }
            }

            _expandedPanel = panel;
            _expandedAction = action;
            _frameBoxes.Clear();
            _directionButtons.Clear();
            _currentFrameIndex = 0;

            int width = panel.Width - 4;
            int startY = 42;

            // 如果是有向動作，顯示方向選擇器 + 選定方向的幀
            // 注意：有向動作的 Frames 是所有方向共用的幀定義，不是每方向分開
            if (action.IsDirectional && action.Frames.Count > 0)
            {
                // 所有方向共用同一組幀
                int framesPerDir = action.Frames.Count;

                // 方向選擇器 (3x3 九宮格) - 只顯示數字 0-7
                int btnSize = 24;
                int gridStartX = 4;
                // 方向: 0=左上, 1=上, 2=右上, 3=右, 4=右下, 5=下, 6=左下, 7=左 (從左上順時針)
                var dirPositions = new (int dir, int col, int row)[]
                {
                    (0, 0, 0), (1, 1, 0), (2, 2, 0),   // 左上, 上, 右上
                    (7, 0, 1), (-1, 1, 1), (3, 2, 1),  // 左, 中間空白, 右
                    (6, 0, 2), (5, 1, 2), (4, 2, 2),   // 左下, 下, 右下
                };

                foreach (var (dir, col, row) in dirPositions)
                {
                    if (dir == -1) continue; // 跳過中間

                    var btn = new Button
                    {
                        Text = dir.ToString(),
                        Location = new Point(gridStartX + col * (btnSize + 1), startY + row * (btnSize + 1)),
                        Size = new Size(btnSize, btnSize),
                        FlatStyle = FlatStyle.Flat,
                        ForeColor = dir == _selectedDirection ? Color.Yellow : Color.White,
                        BackColor = dir == _selectedDirection ? Color.FromArgb(80, 80, 100) : Color.FromArgb(60, 60, 65),
                        Font = new Font(this.Font.FontFamily, 8),
                        Tag = dir
                    };
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Click += DirectionButton_Click;
                    panel.Controls.Add(btn);
                    _directionButtons.Add(btn);
                }

                // 幀格子容器 (在方向選擇器下面)
                int containerY = startY + btnSize * 3 + 6;
                int containerHeight = FRAME_BOX_HEIGHT + 8;

                var frameContainer = new Panel
                {
                    Location = new Point(4, containerY),
                    Size = new Size(width - 8, containerHeight),
                    AutoScroll = true,
                    BackColor = Color.FromArgb(35, 35, 38),
                    Tag = "frameContainer"
                };
                panel.Controls.Add(frameContainer);

                // 建立選定方向的幀格子 (所有方向共用同一組幀)
                int xPos = 2;
                for (int i = 0; i < action.Frames.Count; i++)
                {
                    var frame = action.Frames[i];
                    var frameBox = CreateFrameBox(frame, i, _selectedDirection);  // 傳入當前方向
                    frameBox.Location = new Point(xPos, 2);
                    frameContainer.Controls.Add(frameBox);
                    _frameBoxes.Add(frameBox);
                    xPos += FRAME_BOX_WIDTH + FRAME_BOX_MARGIN;
                }

                // 圖片預覽區 (在幀格子下方)
                int previewY = containerY + containerHeight + 4;
                CreatePreviewPanel(panel, 4, previewY, width - 8);

                panel.Height = previewY + PREVIEW_HEIGHT + 8;
            }
            else
            {
                // 無向動作 - 單排
                int containerHeight = FRAME_BOX_HEIGHT + 8;

                var frameContainer = new Panel
                {
                    Location = new Point(4, startY),
                    Size = new Size(width - 8, containerHeight),
                    AutoScroll = true,
                    BackColor = Color.FromArgb(35, 35, 38),
                    Tag = "frameContainer"
                };

                int xPos = 2;
                for (int i = 0; i < action.Frames.Count; i++)
                {
                    var frame = action.Frames[i];
                    var frameBox = CreateFrameBox(frame, i);
                    frameBox.Location = new Point(xPos, 2);
                    frameContainer.Controls.Add(frameBox);
                    _frameBoxes.Add(frameBox);
                    xPos += FRAME_BOX_WIDTH + FRAME_BOX_MARGIN;
                }

                panel.Controls.Add(frameContainer);

                // 圖片預覽區 (在幀格子下方)
                int previewY = startY + containerHeight + 4;
                CreatePreviewPanel(panel, 4, previewY, width - 8);

                panel.Height = previewY + PREVIEW_HEIGHT + 8;
            }

            // 開始動畫
            timerAnimation.Start();
        }

        private Panel CreateFrameBox(SprFrame frame, int index, int direction = -1)
        {
            var box = new Panel
            {
                Size = new Size(FRAME_BOX_WIDTH, FRAME_BOX_HEIGHT),
                Margin = new Padding(FRAME_BOX_MARGIN),
                BackColor = Color.FromArgb(50, 50, 54),
                BorderStyle = BorderStyle.FixedSingle,
                Tag = index
            };

            // 上層：幀編號
            // 顯示 "{ImageId+direction}.{FrameIndex}" (實際的 sprite 子編號)
            // 有向動作: ImageId + direction (例如 ImageId=32, direction=4 → 36.0)
            // 無向動作: ImageId (例如 32.0)
            int subId = direction >= 0 ? frame.ImageId + direction : frame.ImageId;
            string topText = $"{subId}.{frame.FrameIndex}";

            // 特殊符號：
            // [XXX = 播放音效 XXX
            // ]XXX = 疊加顯示圖檔 XXX
            // ! = 觸發對方被打動作/聲音
            if (frame.SoundIds.Count > 0)
            {
                topText += $"[{frame.SoundIds[0]}";
            }
            if (frame.OverlayIds.Count > 0)
            {
                topText += $"]{frame.OverlayIds[0]}";
            }
            if (frame.TriggerHit) topText += "!";

            var lblTop = new Label
            {
                Text = topText,
                Location = new Point(1, 1),
                Size = new Size(FRAME_BOX_WIDTH - 4, 16),
                ForeColor = frame.SoundIds.Count > 0 ? Color.Orange : Color.Cyan,
                Font = new Font("Consolas", 7),
                TextAlign = ContentAlignment.MiddleCenter
            };
            box.Controls.Add(lblTop);

            // 下層：時間 (例如 ":4" 並顯示實際秒數)
            double durationMs = frame.Duration * UNIT_TIME_MS;
            double seconds = durationMs / 1000.0;
            var lblBottom = new Label
            {
                Text = $":{frame.Duration}",
                Location = new Point(1, 18),
                Size = new Size(FRAME_BOX_WIDTH - 4, 14),
                ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            box.Controls.Add(lblBottom);

            // ToolTip 顯示完整資訊
            var tip = new ToolTip();
            string tipText;
            if (direction >= 0)
            {
                tipText = $"幀 {index}: 方向{direction}.{frame.FrameIndex}\n";
            }
            else
            {
                tipText = $"幀 {index}: {frame.ImageId}.{frame.FrameIndex}\n";
            }
            tipText += $"持續: {frame.Duration} 單位 = {durationMs:F0}ms ({seconds:F3}秒)";
            if (frame.SoundIds.Count > 0) tipText += $"\n聲音: [{string.Join(", ", frame.SoundIds)}]";
            if (frame.TriggerHit) tipText += "\n觸發攻擊 (!)";
            if (frame.OverlayIds.Count > 0) tipText += $"\n疊加: ]{string.Join(", ", frame.OverlayIds)}";
            if (frame.EffectIds.Count > 0) tipText += $"\n特效: <{string.Join(", ", frame.EffectIds)}";
            tip.SetToolTip(box, tipText);

            return box;
        }

        private void CreatePreviewPanel(Panel parentPanel, int x, int y, int width)
        {
            // 預覽區外框
            _previewPanel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, PREVIEW_HEIGHT),
                BackColor = Color.FromArgb(30, 30, 33),
                BorderStyle = BorderStyle.FixedSingle,
                Tag = "previewPanel"
            };

            // 圖片顯示 (上方區域)
            _previewPictureBox = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(width - 2, PREVIEW_HEIGHT - 24),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            _previewPanel.Controls.Add(_previewPictureBox);

            // 圖檔資訊標籤 (底部)
            _previewInfoLabel = new Label
            {
                Location = new Point(0, PREVIEW_HEIGHT - 24),
                Size = new Size(width - 2, 22),
                ForeColor = Color.LightGreen,
                BackColor = Color.FromArgb(25, 25, 28),
                Font = new Font("Consolas", 9),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "載入中..."
            };
            _previewPanel.Controls.Add(_previewInfoLabel);

            parentPanel.Controls.Add(_previewPanel);

            // 初始顯示第一幀
            UpdatePreviewImage(0);
        }

        private void UpdatePreviewImage(int frameIndex)
        {
            if (_previewPictureBox == null) return;

            if (_expandedAction == null)
            {
                if (_previewInfoLabel != null) _previewInfoLabel.Text = "錯誤: 無動作";
                return;
            }
            if (_currentEntry == null)
            {
                if (_previewInfoLabel != null) _previewInfoLabel.Text = "錯誤: 無條目";
                return;
            }

            if (frameIndex < 0 || frameIndex >= _expandedAction.Frames.Count)
            {
                _previewPictureBox.Image = null;
                if (_previewInfoLabel != null) _previewInfoLabel.Text = $"錯誤: 幀索引 {frameIndex} 超出範圍";
                return;
            }

            var sprFrame = _expandedAction.Frames[frameIndex];

            // sprite 檔案名稱 = spriteId-{ImageId + direction}
            // 例如：spriteId=3225, ImageId=16, direction=4 → 3225-20.spr
            int direction = _expandedAction.IsDirectional ? _selectedDirection : 0;
            int subId = sprFrame.ImageId + direction;

            // 載入對應的 sprite 資料
            LoadSpriteData(_currentEntry.SpriteId, sprFrame.ImageId, direction);

            if (_currentFrames == null)
            {
                if (_previewInfoLabel != null) _previewInfoLabel.Text = _lastLoadError ?? "錯誤: 無圖檔資料";
                return;
            }

            // 實際的圖片索引就是 FrameIndex
            int actualFrameIndex = sprFrame.FrameIndex;

            // 更新資訊標籤: SpriteId-subId #幀索引
            if (_previewInfoLabel != null)
            {
                _previewInfoLabel.Text = $"{_currentEntry.SpriteId}-{subId} #{actualFrameIndex}";
            }

            // 確保索引在範圍內
            if (actualFrameIndex < 0 || actualFrameIndex >= _currentFrames.Length)
            {
                _previewPictureBox.Image = null;
                if (_previewInfoLabel != null)
                    _previewInfoLabel.Text = $"{_currentEntry.SpriteId}-{subId} #? (超出範圍 {actualFrameIndex}/{_currentFrames.Length})";
                return;
            }

            try
            {
                var frame = _currentFrames[actualFrameIndex];
                _previewPictureBox.Image = frame.image;
            }
            catch
            {
                _previewPictureBox.Image = null;
            }
        }

        private void DirectionButton_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            int dir = (int)btn.Tag;

            if (dir == _selectedDirection) return;

            _selectedDirection = dir;

            // 更新按鈕樣式
            foreach (var b in _directionButtons)
            {
                int bDir = (int)b.Tag;
                b.ForeColor = bDir == _selectedDirection ? Color.Yellow : Color.White;
                b.BackColor = bDir == _selectedDirection ? Color.FromArgb(80, 80, 100) : Color.FromArgb(60, 60, 65);
            }

            // 重新載入該方向的幀
            RefreshDirectionFrames();
        }

        private void RefreshDirectionFrames()
        {
            if (_expandedPanel == null || _expandedAction == null) return;
            if (!_expandedAction.IsDirectional) return;

            StopAnimation();
            _frameBoxes.Clear();
            _highlightBox = null;
            _currentFrameIndex = 0;

            // 找到 frameContainer
            Panel frameContainer = null;
            foreach (Control c in _expandedPanel.Controls)
            {
                if (c is Panel p && p.Tag?.ToString() == "frameContainer")
                {
                    frameContainer = p;
                    break;
                }
            }

            if (frameContainer == null) return;

            // 清除舊的幀格子
            frameContainer.Controls.Clear();

            // 建立新的幀格子 (所有方向共用同一組幀)
            int xPos = 2;
            for (int i = 0; i < _expandedAction.Frames.Count; i++)
            {
                var frame = _expandedAction.Frames[i];
                var frameBox = CreateFrameBox(frame, i, _selectedDirection);  // 傳入當前方向
                frameBox.Location = new Point(xPos, 2);
                frameContainer.Controls.Add(frameBox);
                _frameBoxes.Add(frameBox);
                xPos += FRAME_BOX_WIDTH + FRAME_BOX_MARGIN;
            }

            // 方向改變後更新預覽圖片
            UpdatePreviewImage(0);

            // 重新開始動畫
            timerAnimation.Start();
        }

        private void StopAnimation()
        {
            timerAnimation.Stop();
        }

        private void TimerAnimation_Tick(object sender, EventArgs e)
        {
            if (_expandedAction == null || _frameBoxes.Count == 0)
            {
                StopAnimation();
                return;
            }

            // 移除上一個高亮
            if (_highlightBox != null)
            {
                _highlightBox.BackColor = Color.FromArgb(50, 50, 54);
            }

            // 高亮當前幀
            if (_currentFrameIndex < _frameBoxes.Count)
            {
                _highlightBox = _frameBoxes[_currentFrameIndex];
                _highlightBox.BackColor = Color.FromArgb(80, 120, 80);

                // 更新預覽圖片
                UpdatePreviewImage(_currentFrameIndex);

                // 根據幀的 Duration 調整間隔
                // Duration 單位時間 = 1/24 秒，例如 Duration=4 → 4/24秒 ≈ 167ms
                var action = _expandedAction;
                if (_currentFrameIndex < action.Frames.Count)
                {
                    var frame = action.Frames[_currentFrameIndex];
                    int intervalMs = (int)Math.Round(frame.Duration * UNIT_TIME_MS);
                    timerAnimation.Interval = Math.Max(20, intervalMs);  // 最小 20ms 避免太快
                }
            }

            _currentFrameIndex = (_currentFrameIndex + 1) % _frameBoxes.Count;
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
