using System;
using System.Text;
using Eto.Drawing;
using Eto.Forms;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 十六進制預覽器（用於未知格式的二進制檔案）
    /// </summary>
    public class HexViewer : BaseViewer
    {
        private const int BytesPerLine = 16;
        private const int MaxLines = 1000;

        // 空的 SupportedExtensions，因為這是 fallback viewer
        public override string[] SupportedExtensions => Array.Empty<string>();

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;

            var sb = new StringBuilder();
            int lines = Math.Min(data.Length / BytesPerLine + 1, MaxLines);

            for (int i = 0; i < lines; i++)
            {
                int offset = i * BytesPerLine;
                sb.Append($"{offset:X8}  ");

                // Hex bytes
                for (int j = 0; j < BytesPerLine; j++)
                {
                    if (offset + j < data.Length)
                        sb.Append($"{data[offset + j]:X2} ");
                    else
                        sb.Append("   ");
                    if (j == 7) sb.Append(" ");
                }

                // ASCII representation
                sb.Append(" |");
                for (int j = 0; j < BytesPerLine && offset + j < data.Length; j++)
                {
                    byte b = data[offset + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine("|");
            }

            if (data.Length > lines * BytesPerLine)
                sb.AppendLine($"... ({data.Length - lines * BytesPerLine} more bytes)");

            _control = new RichTextArea
            {
                ReadOnly = true,
                Text = sb.ToString(),
                Font = new Font("Menlo, Monaco, Consolas, monospace", 12)
            };
        }
    }
}
