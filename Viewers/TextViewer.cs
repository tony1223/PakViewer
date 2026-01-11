using System;
using System.Text;
using Eto.Drawing;
using Eto.Forms;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 文字檔預覽器
    /// </summary>
    public class TextViewer : BaseViewer
    {
        private RichTextArea _textArea;
        private TextBox _searchBox;
        private Label _searchResultLabel;
        private string _text;
        private int _lastSearchIndex;

        public override string[] SupportedExtensions => new[] { ".txt", ".html", ".htm", ".xml", ".s", ".tbl", ".json" };

        public override bool CanSearch => true;

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;
            _lastSearchIndex = 0;

            var encoding = DetectEncoding(data, fileName);
            _text = encoding.GetString(data);

            _textArea = new RichTextArea
            {
                ReadOnly = true,
                Text = _text,
                Font = new Font("Menlo, Monaco, Consolas, monospace", 12)
            };

            _control = _textArea;
        }

        public override Control GetSearchToolbar()
        {
            _searchBox = new TextBox { PlaceholderText = "Search...", Width = 200 };
            _searchBox.KeyDown += OnSearchKeyDown;

            var prevBtn = new Button { Text = "\u25c0", Width = 30 };
            prevBtn.Click += OnSearchPrev;

            var nextBtn = new Button { Text = "\u25b6", Width = 30 };
            nextBtn.Click += OnSearchNext;

            _searchResultLabel = new Label { Text = "" };

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items = { new Label { Text = "Find:" }, _searchBox, prevBtn, nextBtn, _searchResultLabel }
            };
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter)
            {
                SearchNext();
                e.Handled = true;
            }
        }

        private void OnSearchPrev(object sender, EventArgs e) => SearchPrev();
        private void OnSearchNext(object sender, EventArgs e) => SearchNext();

        private void SearchNext()
        {
            var keyword = _searchBox?.Text?.Trim();
            if (string.IsNullOrEmpty(keyword) || _text == null) return;

            var idx = _text.IndexOf(keyword, _lastSearchIndex + 1, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = _text.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                _textArea.Selection = new Range<int>(idx, idx + keyword.Length);
                _textArea.Focus();
                UpdateSearchResult(keyword);
            }
            else
            {
                _searchResultLabel.Text = "Not found";
            }
        }

        private void SearchPrev()
        {
            var keyword = _searchBox?.Text?.Trim();
            if (string.IsNullOrEmpty(keyword) || _text == null) return;

            var searchStart = _lastSearchIndex > 0 ? _lastSearchIndex - 1 : _text.Length - 1;
            var idx = _text.LastIndexOf(keyword, searchStart, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = _text.LastIndexOf(keyword, _text.Length - 1, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                _textArea.Selection = new Range<int>(idx, idx + keyword.Length);
                _textArea.Focus();
                UpdateSearchResult(keyword);
            }
            else
            {
                _searchResultLabel.Text = "Not found";
            }
        }

        private void UpdateSearchResult(string keyword)
        {
            // Count total matches
            int count = 0;
            int pos = 0;
            int currentMatch = 0;
            while ((pos = _text.IndexOf(keyword, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                if (pos <= _lastSearchIndex) currentMatch = count;
                pos++;
            }
            _searchResultLabel.Text = $"{currentMatch}/{count}";
        }

        private static Encoding DetectEncoding(byte[] data, string fileName)
        {
            // Register code pages if not already done
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Check filename for language hint
            var lowerName = fileName?.ToLower() ?? "";
            if (lowerName.Contains("-c") || lowerName.Contains("_c"))
                return Encoding.GetEncoding("big5");
            if (lowerName.Contains("-j") || lowerName.Contains("_j"))
                return Encoding.GetEncoding("shift_jis");
            if (lowerName.Contains("-k") || lowerName.Contains("_k"))
                return Encoding.GetEncoding("euc-kr");
            if (lowerName.Contains("-h") || lowerName.Contains("_h"))
                return Encoding.GetEncoding("gb2312");

            // Check BOM
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode;

            // Default to Big5 for Lineage files
            return Encoding.GetEncoding("big5");
        }

        public override void Dispose()
        {
            _textArea = null;
            _searchBox = null;
            _text = null;
            base.Dispose();
        }
    }
}
