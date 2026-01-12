using System;
using System.Collections.Generic;
using System.Linq;

namespace PakViewer.Viewers
{
    /// <summary>
    /// Viewer 工廠類別
    /// </summary>
    public static class ViewerFactory
    {
        private static readonly List<Func<IFileViewer>> _viewerFactories = new()
        {
            () => new SpriteViewer(),
            () => new TilViewer(),       // TIL 地圖圖塊
            () => new L1ImageViewer(),   // Lineage 特有格式 (.tbt, .img)
            () => new ImageViewer(),     // 標準圖片格式
            () => new TextViewer(),
        };

        private static readonly HexViewer _hexViewerPrototype = new HexViewer();

        /// <summary>
        /// 根據副檔名取得適合的 Viewer
        /// </summary>
        /// <param name="extension">副檔名（小寫，含點號）</param>
        /// <returns>新的 Viewer 實例</returns>
        public static IFileViewer CreateViewer(string extension)
        {
            extension = extension?.ToLower() ?? "";

            foreach (var factory in _viewerFactories)
            {
                var viewer = factory();
                if (viewer.SupportedExtensions.Contains(extension))
                    return viewer;
            }

            // Fallback to HexViewer
            return new HexViewer();
        }

        /// <summary>
        /// 根據檔案內容判斷是否為文字檔
        /// </summary>
        public static bool IsTextContent(byte[] data)
        {
            if (data == null || data.Length == 0) return false;

            // Check first 1000 bytes for non-printable characters
            int checkLength = Math.Min(data.Length, 1000);
            int nonPrintable = 0;

            for (int i = 0; i < checkLength; i++)
            {
                byte b = data[i];
                // Allow common text characters and control chars (tab, newline, etc.)
                if (b < 9 || (b > 13 && b < 32) || b == 127)
                    nonPrintable++;
            }

            // If less than 10% non-printable, consider it text
            return nonPrintable < checkLength * 0.1;
        }

        /// <summary>
        /// 根據檔案內容判斷是否為 PNG 圖片
        /// </summary>
        public static bool IsPngContent(byte[] data)
        {
            if (data == null || data.Length < 8) return false;

            // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
            return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
        }

        /// <summary>
        /// 根據副檔名和內容智慧選擇 Viewer
        /// </summary>
        public static IFileViewer CreateViewerSmart(string extension, byte[] data, string fileName = null)
        {
            extension = extension?.ToLower() ?? "";
            var lowerFileName = fileName?.ToLower() ?? "";

            // 特殊處理: list.spr 是文字列表檔
            if (lowerFileName.EndsWith("list.spr"))
            {
                return new TextViewer();
            }

            // 特殊處理: .spr 一般是二進制精靈
            if (extension == ".spr")
            {
                return new SpriteViewer();
            }

            // First try by extension
            foreach (var factory in _viewerFactories)
            {
                var viewer = factory();
                if (viewer.SupportedExtensions.Contains(extension))
                    return viewer;
            }

            // If unknown extension, try to detect by content
            if (IsTextContent(data))
                return new TextViewer();

            if (IsPngContent(data))
                return new ImageViewer();

            // Fallback to HexViewer
            return new HexViewer();
        }

        /// <summary>
        /// 註冊自訂 Viewer
        /// </summary>
        public static void RegisterViewer(Func<IFileViewer> factory)
        {
            _viewerFactories.Insert(0, factory); // Insert at beginning for priority
        }
    }
}
