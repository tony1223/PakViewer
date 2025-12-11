using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PakViewer
{
    public class frmPngOptimizeProgress : Form
    {
        private ListView lvProgress;
        private Label lblStatus;
        private Label lblTotal;
        private ProgressBar progressBar;
        private Button btnCancel;
        private Button btnClose;

        private CancellationTokenSource _cts;
        private bool _isCompleted = false;
        private int _totalPaks;
        private int _completedPaks;

        // 結果
        public List<(string pakName, int pngCount, long originalSize, long newSize, string error)> Results { get; private set; }
        public long TotalOriginalSize { get; private set; }
        public long TotalNewSize { get; private set; }
        public int TotalPngCount { get; private set; }

        public frmPngOptimizeProgress()
        {
            InitializeComponent();
            Results = new List<(string, int, long, long, string)>();
        }

        private void InitializeComponent()
        {
            this.Text = "批次壓縮 PNG";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Status label
            lblStatus = new Label
            {
                Text = "準備中...",
                Location = new Point(12, 12),
                Size = new Size(660, 20),
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold)
            };
            this.Controls.Add(lblStatus);

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(12, 38),
                Size = new Size(660, 25),
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(progressBar);

            // Total label
            lblTotal = new Label
            {
                Text = "",
                Location = new Point(12, 68),
                Size = new Size(660, 20)
            };
            this.Controls.Add(lblTotal);

            // ListView for progress
            lvProgress = new ListView
            {
                Location = new Point(12, 95),
                Size = new Size(660, 310),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            lvProgress.Columns.Add("PAK 檔案", 180);
            lvProgress.Columns.Add("狀態", 100);
            lvProgress.Columns.Add("PNG 數", 70, HorizontalAlignment.Right);
            lvProgress.Columns.Add("原始大小", 90, HorizontalAlignment.Right);
            lvProgress.Columns.Add("壓縮後", 90, HorizontalAlignment.Right);
            lvProgress.Columns.Add("節省", 90, HorizontalAlignment.Right);
            this.Controls.Add(lvProgress);

            // Cancel button
            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(510, 420),
                Size = new Size(80, 30)
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);

            // Close button
            btnClose = new Button
            {
                Text = "關閉",
                Location = new Point(600, 420),
                Size = new Size(80, 30),
                Enabled = false
            };
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);

            this.FormClosing += FrmPngOptimizeProgress_FormClosing;
        }

        private void FrmPngOptimizeProgress_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isCompleted && _cts != null && !_cts.IsCancellationRequested)
            {
                var result = MessageBox.Show("確定要取消處理嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _cts.Cancel();
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                btnCancel.Enabled = false;
                lblStatus.Text = "正在取消...";
            }
        }

        public async Task ProcessAsync(string[] idxFiles)
        {
            _totalPaks = idxFiles.Length;
            _completedPaks = 0;
            _cts = new CancellationTokenSource();

            progressBar.Maximum = _totalPaks;
            progressBar.Value = 0;
            lblStatus.Text = $"處理中... (0/{_totalPaks})";

            // 初始化 ListView
            var itemMap = new ConcurrentDictionary<string, ListViewItem>();
            foreach (var idxFile in idxFiles)
            {
                string pakName = Path.GetFileName(idxFile.Replace(".idx", ".pak"));
                var item = new ListViewItem(new[] { pakName, "等待中", "", "", "", "" });
                item.Tag = idxFile;
                lvProgress.Items.Add(item);
                itemMap[idxFile] = item;
            }

            var results = new ConcurrentBag<(string pakName, int pngCount, long originalSize, long newSize, string error)>();

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(idxFiles,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            CancellationToken = _cts.Token
                        },
                        idxFile =>
                        {
                            string pakName = Path.GetFileName(idxFile.Replace(".idx", ".pak"));

                            // 更新狀態為處理中
                            this.BeginInvoke((Action)(() =>
                            {
                                if (itemMap.TryGetValue(idxFile, out var item))
                                {
                                    item.SubItems[1].Text = "處理中...";
                                    item.BackColor = Color.LightYellow;
                                    item.EnsureVisible();
                                }
                            }));

                            var (successCount, originalSize, newSize, error) = PakReader.OptimizePakPng(idxFile, null);

                            // 更新結果
                            this.BeginInvoke((Action)(() =>
                            {
                                if (itemMap.TryGetValue(idxFile, out var item))
                                {
                                    if (error != null)
                                    {
                                        item.SubItems[1].Text = "錯誤";
                                        item.SubItems[2].Text = error;
                                        item.BackColor = Color.LightPink;
                                        results.Add((pakName, 0, 0, 0, error));
                                    }
                                    else if (successCount == 0)
                                    {
                                        item.SubItems[1].Text = "無 PNG";
                                        item.BackColor = Color.LightGray;
                                    }
                                    else
                                    {
                                        long saved = originalSize - newSize;
                                        double percent = originalSize > 0 ? saved * 100.0 / originalSize : 0;

                                        item.SubItems[1].Text = "完成";
                                        item.SubItems[2].Text = successCount.ToString();
                                        item.SubItems[3].Text = FormatSize(originalSize);
                                        item.SubItems[4].Text = FormatSize(newSize);
                                        item.SubItems[5].Text = $"{FormatSize(saved)} ({percent:F1}%)";
                                        item.BackColor = saved > 0 ? Color.LightGreen : Color.White;

                                        results.Add((pakName, successCount, originalSize, newSize, null));
                                    }
                                }

                                _completedPaks++;
                                progressBar.Value = _completedPaks;
                                lblStatus.Text = $"處理中... ({_completedPaks}/{_totalPaks})";

                                // 更新總計
                                UpdateTotals(results);
                            }));
                        });
                }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "已取消";
            }

            // 完成
            _isCompleted = true;
            Results = results.ToList();
            TotalOriginalSize = Results.Where(r => r.error == null).Sum(r => r.originalSize);
            TotalNewSize = Results.Where(r => r.error == null).Sum(r => r.newSize);
            TotalPngCount = Results.Where(r => r.error == null).Sum(r => r.pngCount);

            if (!_cts.IsCancellationRequested)
            {
                long totalSaved = TotalOriginalSize - TotalNewSize;
                double totalPercent = TotalOriginalSize > 0 ? totalSaved * 100.0 / TotalOriginalSize : 0;
                lblStatus.Text = $"完成！共壓縮 {TotalPngCount} 個 PNG，節省 {FormatSize(totalSaved)} ({totalPercent:F1}%)";
            }

            btnCancel.Enabled = false;
            btnClose.Enabled = true;
        }

        private void UpdateTotals(ConcurrentBag<(string pakName, int pngCount, long originalSize, long newSize, string error)> results)
        {
            long totalOrig = results.Where(r => r.error == null).Sum(r => r.originalSize);
            long totalNew = results.Where(r => r.error == null).Sum(r => r.newSize);
            int totalPng = results.Where(r => r.error == null).Sum(r => r.pngCount);
            long totalSaved = totalOrig - totalNew;
            double percent = totalOrig > 0 ? totalSaved * 100.0 / totalOrig : 0;

            lblTotal.Text = $"累計: {totalPng} 個 PNG, {FormatSize(totalOrig)} → {FormatSize(totalNew)}, 節省 {FormatSize(totalSaved)} ({percent:F1}%)";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }
    }
}
