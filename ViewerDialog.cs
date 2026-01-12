using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Pak;
using PakViewer.Controls;
using PakViewer.Localization;
using PakViewer.Viewers;

namespace PakViewer
{
    /// <summary>
    /// 詳細檢視視窗 - 獨立視窗顯示完整預覽器
    /// </summary>
    public class ViewerDialog : Form
    {
        private PakFile _pak;
        private List<GalleryItem> _items;
        private int _currentIndex;
        private Panel _viewerContainer;
        private Label _titleLabel;
        private Label _indexLabel;
        private IFileViewer _currentViewer;

        public ViewerDialog(PakFile pak, List<GalleryItem> items, int startIndex)
        {
            _pak = pak;
            _items = items;
            _currentIndex = FindItemIndex(startIndex);

            Title = I18n.T("ViewerDialog.Title");
            Size = new Size(800, 600);
            MinimumSize = new Size(400, 300);
            Resizable = true;

            BuildUI();
            LoadCurrentFile();
        }

        private int FindItemIndex(int pakIndex)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Index == pakIndex)
                    return i;
            }
            return 0;
        }

        private void BuildUI()
        {
            // 標題列
            _titleLabel = new Label
            {
                Font = new Font(SystemFont.Bold, 12),
                VerticalAlignment = VerticalAlignment.Center
            };

            _indexLabel = new Label
            {
                TextColor = Colors.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(10, 5),
                Items = { _titleLabel, _indexLabel }
            };

            // 導航按鈕
            var prevBtn = new Button { Text = I18n.T("Button.Prev"), Width = 80 };
            var nextBtn = new Button { Text = I18n.T("Button.Next"), Width = 80 };
            var exportBtn = new Button { Text = I18n.T("Button.Export"), Width = 80 };

            prevBtn.Click += (s, e) => Navigate(-1);
            nextBtn.Click += (s, e) => Navigate(1);
            exportBtn.Click += OnExport;

            var navLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(10, 5),
                Items = { prevBtn, nextBtn, new StackLayoutItem(null, true), exportBtn }
            };

            // 預覽區容器
            _viewerContainer = new Panel { BackgroundColor = Colors.DarkGray };

            // 整體佈局
            Content = new TableLayout
            {
                Rows =
                {
                    new TableRow(headerLayout),
                    new TableRow(_viewerContainer) { ScaleHeight = true },
                    new TableRow(navLayout)
                }
            };

            // 鍵盤快捷鍵
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Keys.Left:
                case Keys.Up:
                    Navigate(-1);
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Down:
                    Navigate(1);
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        private void Navigate(int delta)
        {
            if (_items.Count == 0) return;

            _currentIndex = (_currentIndex + delta + _items.Count) % _items.Count;
            LoadCurrentFile();
        }

        private void LoadCurrentFile()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            // 更新標題
            _titleLabel.Text = item.FileName;
            _indexLabel.Text = $"[{_currentIndex + 1}/{_items.Count}]";
            Title = $"{I18n.T("ViewerDialog.Title")} - {item.FileName}";

            // 載入檔案
            try
            {
                var data = _pak.Extract(item.Index);
                var ext = Path.GetExtension(item.FileName).ToLowerInvariant();

                // 釋放舊 viewer
                _currentViewer?.Dispose();
                _currentViewer = null;

                // 建立新 viewer
                _currentViewer = ViewerFactory.CreateViewerSmart(ext, data, item.FileName);
                _currentViewer.LoadData(data, item.FileName);

                // 顯示 viewer
                var viewerControl = _currentViewer.GetControl();
                var editToolbar = _currentViewer.CanEdit ? _currentViewer.GetEditToolbar() : null;

                if (editToolbar != null)
                {
                    _viewerContainer.Content = new TableLayout
                    {
                        Spacing = new Size(0, 5),
                        Rows =
                        {
                            new TableRow(editToolbar),
                            new TableRow(viewerControl) { ScaleHeight = true }
                        }
                    };
                }
                else
                {
                    _viewerContainer.Content = viewerControl;
                }
            }
            catch (Exception ex)
            {
                _viewerContainer.Content = new Label
                {
                    Text = $"Error: {ex.Message}",
                    TextColor = Colors.Red,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
            }
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            var dialog = new SaveFileDialog
            {
                Title = I18n.T("Dialog.SaveFile"),
                FileName = item.FileName
            };

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    var data = _pak.Extract(item.Index);
                    File.WriteAllBytes(dialog.FileName, data);
                    MessageBox.Show(this, I18n.T("Status.Exported", 1), I18n.T("Dialog.Info"), MessageBoxType.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, I18n.T("Dialog.Error"), MessageBoxType.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentViewer?.Dispose();
                _currentViewer = null;
            }
            base.Dispose(disposing);
        }
    }
}
