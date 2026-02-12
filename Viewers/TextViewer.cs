using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Xml;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 文字檔預覽器 (支援大檔案分頁)
    /// </summary>
    public class TextViewer : BaseViewer
    {
        private TextArea _textArea;
        private Drawable _lineNumberGutter;
        private UITimer _scrollTimer;
        private Font _editorFont;
        private int _firstVisibleLine;
        private int _lineCount;
        private TextBox _searchBox;
        private Label _searchResultLabel;
        private Label _encryptLabel;
        private Button _encryptBtn;
        private string _text;
        private int _lastSearchIndex;

        // 分頁相關
        private const int PageCharLimit = 200_000;  // 每頁最多 200K 字元
        private string _fullText;                    // 完整文字 (大檔案用)
        private bool _isPaginated;
        private List<int> _pageStartChars;           // 每頁起始字元位置
        private List<int> _pageStartLines;           // 每頁起始行號
        private int _currentPage;
        private int _totalPages;
        private Label _pageLabel;
        private Button _prevPageBtn;
        private Button _nextPageBtn;
        private Panel _pageNavPanel;

        public override string[] SupportedExtensions => new[] { ".txt", ".html", ".htm", ".xml", ".s", ".tbl", ".json", ".list" };

        public override bool CanSearch => true;

        public override bool CanEdit => !_isPaginated;

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;
            _lastSearchIndex = 0;

            // 建立 context 追蹤狀態
            _context = new FileContext
            {
                OriginalData = data,
                FileName = fileName,
                IsXmlEncrypted = false
            };

            byte[] displayData = data;

            // 檢查並解密 XML
            if (IsXmlFile(fileName) && XmlCracker.IsEncrypted(data))
            {
                _context.IsXmlEncrypted = true;
                displayData = XmlCracker.Decrypt((byte[])data.Clone());
            }

            _context.DisplayData = displayData;

            // 取得 encoding
            var encoding = _context.IsXmlEncrypted
                ? XmlCracker.GetXmlEncoding(displayData, fileName)
                : DetectEncoding(displayData, fileName);
            _context.FileEncoding = encoding;

            _fullText = encoding.GetString(displayData);
            _isPaginated = _fullText.Length > PageCharLimit;

            _editorFont = new Font("Consolas, monospace", 12);

            if (_isPaginated)
            {
                BuildPages();
                _text = GetPageText(0);
            }
            else
            {
                _text = _fullText;
                _fullText = null; // 不需要額外保留
            }

            _textArea = new TextArea
            {
                ReadOnly = _isPaginated, // 分頁模式唯讀
                Text = _text,
                Font = _editorFont,
                Wrap = false
            };

            _lineCount = _isPaginated
                ? CountLines(_text) + (_pageStartLines != null ? _pageStartLines[_currentPage] : 0)
                : CountLines(_text);

            // 行號面板
            _lineNumberGutter = new Drawable();
            _lineNumberGutter.Paint += PaintLineNumbers;
            UpdateGutterWidth();

            // 監聽文字變更 (僅非分頁模式)
            if (!_isPaginated)
            {
                _textArea.TextChanged += (s, e) =>
                {
                    _hasChanges = (_textArea.Text != _text);
                    _lineCount = CountLines(_textArea.Text);
                    UpdateGutterWidth();
                    _lineNumberGutter.Invalidate();
                };
            }

            // 捲動同步計時器
            _scrollTimer = new UITimer { Interval = 0.05 };
            _scrollTimer.Elapsed += (s, e) =>
            {
                int newFirst = GetFirstVisibleLine();
                if (newFirst != _firstVisibleLine)
                {
                    _firstVisibleLine = newFirst;
                    _lineNumberGutter.Invalidate();
                }
            };
            _scrollTimer.Start();

            // 分頁導覽列
            _pageNavPanel = new Panel { Visible = _isPaginated };
            if (_isPaginated)
            {
                _prevPageBtn = new Button { Text = "◀", Width = 40 };
                _nextPageBtn = new Button { Text = "▶", Width = 40 };
                _pageLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
                UpdatePageLabel();

                _prevPageBtn.Click += (s, e) => NavigatePage(_currentPage - 1);
                _nextPageBtn.Click += (s, e) => NavigatePage(_currentPage + 1);

                _pageNavPanel.Content = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Padding = new Padding(5, 2),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Items = { _prevPageBtn, _pageLabel, _nextPageBtn }
                };
            }

            var editorLayout = new TableLayout
            {
                Spacing = Size.Empty,
                Rows =
                {
                    new TableRow(
                        new TableCell(_lineNumberGutter, false),
                        new TableCell(_textArea, true)
                    )
                }
            };

            _control = new TableLayout
            {
                Rows =
                {
                    new TableRow(_pageNavPanel),
                    new TableRow(editorLayout) { ScaleHeight = true }
                }
            };
        }

        #region 分頁

        private void BuildPages()
        {
            _pageStartChars = new List<int>();
            _pageStartLines = new List<int>();
            _currentPage = 0;

            int pos = 0;
            int lineNum = 0;

            while (pos < _fullText.Length)
            {
                _pageStartChars.Add(pos);
                _pageStartLines.Add(lineNum);

                int end = Math.Min(pos + PageCharLimit, _fullText.Length);

                // 在換行處斷開以保持完整行
                if (end < _fullText.Length)
                {
                    int nl = _fullText.IndexOf('\n', end);
                    if (nl >= 0 && nl - end < 10000) end = nl + 1;
                }

                // 計算此頁行數
                for (int i = pos; i < end; i++)
                {
                    if (_fullText[i] == '\n') lineNum++;
                }

                pos = end;
            }

            _totalPages = _pageStartChars.Count;
        }

        private string GetPageText(int page)
        {
            if (page < 0 || page >= _totalPages) return "";
            int start = _pageStartChars[page];
            int end = (page + 1 < _totalPages) ? _pageStartChars[page + 1] : _fullText.Length;
            return _fullText.Substring(start, end - start);
        }

        private void NavigatePage(int page)
        {
            if (page < 0 || page >= _totalPages) return;
            _currentPage = page;
            _text = GetPageText(page);
            _textArea.Text = _text;
            _lineCount = CountLines(_text) + _pageStartLines[page];
            _firstVisibleLine = 0;
            UpdatePageLabel();
            UpdateGutterWidth();
            _lineNumberGutter.Invalidate();
        }

        private void UpdatePageLabel()
        {
            if (_pageLabel != null)
            {
                _pageLabel.Text = $"Page {_currentPage + 1} / {_totalPages}";
                _prevPageBtn.Enabled = _currentPage > 0;
                _nextPageBtn.Enabled = _currentPage < _totalPages - 1;
            }
        }

        #endregion

        private bool IsXmlFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLower();
            return ext == ".xml";
        }

        /// <summary>
        /// 取得編輯工具列 (儲存按鈕 + 加密狀態 + 編碼)
        /// </summary>
        public override Control GetEditToolbar()
        {
            var saveBtn = new Button { Text = I18n.T("Button.Save") };
            saveBtn.Click += OnSaveClick;

            // 加密按鈕：僅在 XML 且未加密時顯示
            bool showEncrypt = IsXmlFile(_fileName) && _context?.IsXmlEncrypted != true;
            _encryptBtn = new Button { Text = I18n.T("Button.Encrypt"), Visible = showEncrypt };
            _encryptBtn.Click += OnEncryptClick;

            _encryptLabel = new Label
            {
                Text = _context?.IsXmlEncrypted == true
                    ? $"[{I18n.T("Status.Encrypted")}]"
                    : "",
                TextColor = Colors.Orange,
                VerticalAlignment = VerticalAlignment.Center
            };

            var encodingLabel = new Label
            {
                Text = _context?.FileEncoding != null
                    ? $"[{GetEncodingDisplayName(_context.FileEncoding)}]"
                    : "",
                TextColor = Colors.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items = { saveBtn, _encryptBtn, _encryptLabel, encodingLabel }
            };
        }

        private static string GetEncodingDisplayName(System.Text.Encoding encoding)
        {
            // 常見編碼的友善名稱
            return encoding.WebName.ToUpper() switch
            {
                "UTF-8" => "UTF-8",
                "UTF-16" => "UTF-16",
                "BIG5" => "Big5",
                "SHIFT_JIS" => "Shift-JIS",
                "EUC-KR" => "EUC-KR",
                "GB2312" => "GB2312",
                _ => encoding.WebName.ToUpper()
            };
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (_context == null || _textArea == null) return;

            // 取得編輯後的內容
            var editedText = _textArea.Text;
            var editedBytes = _context.FileEncoding.GetBytes(editedText);

            // 還原加密狀態
            var saveData = _context.PrepareForSave(editedBytes);

            // 觸發儲存事件
            OnSaveRequested(saveData);

            // 重置變更標記
            _text = editedText;
            _hasChanges = false;
        }

        private void OnEncryptClick(object sender, EventArgs e)
        {
            if (_context == null || _textArea == null) return;

            // 設定為加密狀態
            _context.IsXmlEncrypted = true;

            // 取得編輯後的內容並加密儲存
            var editedText = _textArea.Text;
            var editedBytes = _context.FileEncoding.GetBytes(editedText);
            var saveData = _context.PrepareForSave(editedBytes);

            OnSaveRequested(saveData);

            // 更新 UI
            _text = editedText;
            _hasChanges = false;
            _encryptBtn.Visible = false;
            _encryptLabel.Text = $"[{I18n.T("Status.Encrypted")}]";
        }

        public override byte[] GetModifiedData()
        {
            if (_textArea == null || _context == null) return _data;

            var editedText = _textArea.Text;
            var editedBytes = _context.FileEncoding.GetBytes(editedText);

            // 還原加密狀態
            return _context.PrepareForSave(editedBytes);
        }

        public override Control GetSearchToolbar()
        {
            _searchBox = new TextBox { PlaceholderText = I18n.T("Placeholder.Search"), Width = 200 };
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
                Items = { new Label { Text = I18n.T("Label.Find") }, _searchBox, prevBtn, nextBtn, _searchResultLabel }
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
            if (string.IsNullOrEmpty(keyword) || _textArea == null) return;

            if (_isPaginated)
            {
                SearchNextPaginated(keyword);
                return;
            }

            // 使用 TextArea 的當前文字進行搜尋（支援編輯後的內容）
            var currentText = _textArea.Text;
            if (string.IsNullOrEmpty(currentText)) return;

            var idx = currentText.IndexOf(keyword, _lastSearchIndex + 1, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = currentText.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                _textArea.Selection = new Range<int>(idx, idx + keyword.Length);
                _textArea.Focus();
                UpdateSearchResult(keyword, currentText);
            }
            else
            {
                _searchResultLabel.Text = I18n.T("Status.NotFound");
            }
        }

        private void SearchPrev()
        {
            var keyword = _searchBox?.Text?.Trim();
            if (string.IsNullOrEmpty(keyword) || _textArea == null) return;

            if (_isPaginated)
            {
                SearchPrevPaginated(keyword);
                return;
            }

            // 使用 TextArea 的當前文字進行搜尋（支援編輯後的內容）
            var currentText = _textArea.Text;
            if (string.IsNullOrEmpty(currentText)) return;

            var searchStart = _lastSearchIndex > 0 ? _lastSearchIndex - 1 : currentText.Length - 1;
            var idx = currentText.LastIndexOf(keyword, searchStart, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = currentText.LastIndexOf(keyword, currentText.Length - 1, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                _textArea.Selection = new Range<int>(idx, idx + keyword.Length);
                _textArea.Focus();
                UpdateSearchResult(keyword, currentText);
            }
            else
            {
                _searchResultLabel.Text = I18n.T("Status.NotFound");
            }
        }

        #region 分頁搜尋

        private void SearchNextPaginated(string keyword)
        {
            // 從全文搜尋
            var idx = _fullText.IndexOf(keyword, _lastSearchIndex + 1, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = _fullText.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                NavigateToGlobalIndex(idx, keyword.Length);
                UpdateSearchResultPaginated(keyword);
            }
            else
            {
                _searchResultLabel.Text = I18n.T("Status.NotFound");
            }
        }

        private void SearchPrevPaginated(string keyword)
        {
            var searchStart = _lastSearchIndex > 0 ? _lastSearchIndex - 1 : _fullText.Length - 1;
            var idx = _fullText.LastIndexOf(keyword, searchStart, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Wrap around
                idx = _fullText.LastIndexOf(keyword, _fullText.Length - 1, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                _lastSearchIndex = idx;
                NavigateToGlobalIndex(idx, keyword.Length);
                UpdateSearchResultPaginated(keyword);
            }
            else
            {
                _searchResultLabel.Text = I18n.T("Status.NotFound");
            }
        }

        private void NavigateToGlobalIndex(int globalCharIndex, int length)
        {
            // 找到該字元位置所在的頁
            int page = 0;
            for (int i = _totalPages - 1; i >= 0; i--)
            {
                if (globalCharIndex >= _pageStartChars[i])
                {
                    page = i;
                    break;
                }
            }

            if (page != _currentPage)
                NavigatePage(page);

            // 在頁內的相對位置
            int localIndex = globalCharIndex - _pageStartChars[page];
            _textArea.Selection = new Range<int>(localIndex, localIndex + length);
            _textArea.Focus();
        }

        private void UpdateSearchResultPaginated(string keyword)
        {
            // 計算全文中的匹配數和當前位置
            int count = 0;
            int pos = 0;
            int currentMatch = 0;
            while ((pos = _fullText.IndexOf(keyword, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                if (pos <= _lastSearchIndex) currentMatch = count;
                pos++;
            }
            _searchResultLabel.Text = $"{currentMatch}/{count}";
        }

        #endregion

        private void UpdateSearchResult(string keyword, string text)
        {
            // Count total matches
            int count = 0;
            int pos = 0;
            int currentMatch = 0;
            while ((pos = text.IndexOf(keyword, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
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

            // Check BOM first (highest priority)
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode;

            // JSON files are always UTF-8 per RFC 8259
            var ext = Path.GetExtension(fileName)?.ToLower();
            if (ext == ".json")
                return Encoding.UTF8;

            // Check filename for language hint (classic Lineage files)
            var lowerName = fileName?.ToLower() ?? "";
            if (lowerName.Contains("-c") || lowerName.Contains("_c"))
                return Encoding.GetEncoding("big5");
            if (lowerName.Contains("-j") || lowerName.Contains("_j"))
                return Encoding.GetEncoding("shift_jis");
            if (lowerName.Contains("-k") || lowerName.Contains("_k"))
                return Encoding.GetEncoding("euc-kr");
            if (lowerName.Contains("-h") || lowerName.Contains("_h"))
                return Encoding.GetEncoding("gb2312");

            // Default to Big5 for Lineage files
            return Encoding.GetEncoding("big5");
        }

        /// <summary>
        /// 取得檔案的文字內容（用於內容搜尋）
        /// 會自動處理加密 XML 的解密
        /// </summary>
        public override string GetTextContent(byte[] data, string fileName)
        {
            if (data == null || data.Length == 0) return null;

            byte[] displayData = data;

            // 檢查並解密 XML
            if (IsXmlFile(fileName) && XmlCracker.IsEncrypted(data))
            {
                displayData = XmlCracker.Decrypt((byte[])data.Clone());
            }

            var encoding = IsXmlFile(fileName) && XmlCracker.IsEncrypted(data)
                ? XmlCracker.GetXmlEncoding(displayData, fileName)
                : DetectEncoding(displayData, fileName);

            return encoding.GetString(displayData);
        }

        #region 行號

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') count++;
            }
            return count;
        }

        private void UpdateGutterWidth()
        {
            int totalLines = _isPaginated && _pageStartLines != null
                ? _pageStartLines[_currentPage] + CountLines(_text)
                : _lineCount;
            int digits = Math.Max(3, totalLines.ToString().Length);
            _lineNumberGutter.Width = (digits * 9) + 16;
        }

        private int GetFirstVisibleLine()
        {
            try
            {
                // WPF: System.Windows.Controls.TextBox.GetFirstVisibleLineIndex()
                dynamic native = _textArea.ControlObject;
                int line = (int)native.GetFirstVisibleLineIndex();
                if (line >= 0) return line;
            }
            catch { }

            // Fallback: 使用游標位置估算
            return Math.Max(0, GetLineFromCharIndex(_textArea.CaretIndex));
        }

        private int GetLineFromCharIndex(int charIndex)
        {
            var text = _textArea.Text ?? "";
            int line = 0;
            for (int i = 0; i < charIndex && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }

        private void PaintLineNumbers(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            float w = _lineNumberGutter.Width;
            float h = _lineNumberGutter.Height;

            // 背景
            g.FillRectangle(new Color(0.95f, 0.95f, 0.95f), 0, 0, w, h);

            float lineHeight = _editorFont.LineHeight;
            float padding = 1f;
            int visibleLines = (int)(h / lineHeight) + 2;

            // 分頁模式行號偏移
            int lineOffset = _isPaginated && _pageStartLines != null ? _pageStartLines[_currentPage] : 0;

            for (int i = 0; i < visibleLines; i++)
            {
                int lineNum = lineOffset + _firstVisibleLine + i + 1;
                float y = padding + i * lineHeight;
                string numStr = lineNum.ToString();
                var size = g.MeasureString(_editorFont, numStr);
                float x = w - size.Width - 8;

                g.DrawText(_editorFont, Colors.Gray, x, y, numStr);
            }

            // 右側分隔線
            g.DrawLine(new Color(0.85f, 0.85f, 0.85f), w - 1, 0, w - 1, h);
        }

        #endregion

        public override void Dispose()
        {
            _scrollTimer?.Stop();
            _scrollTimer = null;
            _lineNumberGutter = null;
            _editorFont = null;
            _textArea = null;
            _searchBox = null;
            _encryptBtn = null;
            _text = null;
            _fullText = null;
            _pageStartChars = null;
            _pageStartLines = null;
            _pageLabel = null;
            base.Dispose();
        }
    }
}
