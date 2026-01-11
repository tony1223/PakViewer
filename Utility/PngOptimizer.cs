using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace PakViewer.Utility
{
    /// <summary>
    /// PNG 無損壓縮工具（使用 SixLabors.ImageSharp，跨平台支援）
    /// </summary>
    public static class PngOptimizer
    {
        /// <summary>
        /// 壓縮單個 PNG 檔案（直接修改原檔）
        /// </summary>
        /// <param name="pngPath">PNG 檔案路徑</param>
        /// <param name="level">壓縮等級 (1-6, 預設 4) - 對應 ImageSharp CompressionLevel</param>
        /// <returns>(是否成功, 節省的位元組數)</returns>
        public static (bool success, long savedBytes, string error) Optimize(string pngPath, int level = 4)
        {
            if (!File.Exists(pngPath))
                return (false, 0, "檔案不存在");

            try
            {
                var originalSize = new FileInfo(pngPath).Length;
                var originalData = File.ReadAllBytes(pngPath);

                // 使用 ImageSharp 重新編碼 PNG
                using (var image = Image.Load(originalData))
                {
                    var encoder = new PngEncoder
                    {
                        CompressionLevel = MapCompressionLevel(level),
                        FilterMethod = PngFilterMethod.Adaptive,
                        ColorType = PngColorType.RgbWithAlpha
                    };

                    using (var ms = new MemoryStream())
                    {
                        image.SaveAsPng(ms, encoder);
                        var optimizedData = ms.ToArray();

                        // 只在壓縮後比原檔小時才寫入
                        if (optimizedData.Length < originalSize)
                        {
                            File.WriteAllBytes(pngPath, optimizedData);
                            return (true, originalSize - optimizedData.Length, null);
                        }
                        else
                        {
                            // 壓縮後反而變大，保持原檔
                            return (true, 0, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message);
            }
        }

        /// <summary>
        /// 壓縮 PNG 資料（傳入 byte[]，傳回壓縮後的 byte[]）
        /// </summary>
        /// <param name="pngData">PNG 檔案資料</param>
        /// <param name="level">壓縮等級 (1-6, 預設 4)</param>
        /// <returns>(壓縮後的資料, 節省的位元組數, 錯誤訊息)</returns>
        public static (byte[] data, long savedBytes, string error) OptimizeData(byte[] pngData, int level = 4)
        {
            try
            {
                var originalSize = pngData.Length;

                using (var image = Image.Load(pngData))
                {
                    var encoder = new PngEncoder
                    {
                        CompressionLevel = MapCompressionLevel(level),
                        FilterMethod = PngFilterMethod.Adaptive,
                        ColorType = PngColorType.RgbWithAlpha
                    };

                    using (var ms = new MemoryStream())
                    {
                        image.SaveAsPng(ms, encoder);
                        var optimizedData = ms.ToArray();

                        // 只在壓縮後比原檔小時才使用新資料
                        if (optimizedData.Length < originalSize)
                        {
                            return (optimizedData, originalSize - optimizedData.Length, null);
                        }
                        else
                        {
                            // 壓縮後反而變大，回傳原資料
                            return (pngData, 0, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (pngData, 0, ex.Message);
            }
        }

        /// <summary>
        /// 批次壓縮多個 PNG 檔案
        /// </summary>
        /// <param name="pngPaths">PNG 檔案路徑列表</param>
        /// <param name="level">壓縮等級</param>
        /// <param name="progress">進度回報 (已完成數, 總數, 目前檔名)</param>
        /// <returns>(成功數, 總節省位元組, 錯誤列表)</returns>
        public static (int successCount, long totalSavedBytes, System.Collections.Generic.List<string> errors)
            OptimizeBatch(System.Collections.Generic.IList<string> pngPaths, int level = 4,
                Action<int, int, string> progress = null)
        {
            int successCount = 0;
            long totalSaved = 0;
            var errors = new System.Collections.Generic.List<string>();

            for (int i = 0; i < pngPaths.Count; i++)
            {
                var path = pngPaths[i];
                progress?.Invoke(i + 1, pngPaths.Count, Path.GetFileName(path));

                var (success, saved, error) = Optimize(path, level);

                if (success)
                {
                    successCount++;
                    totalSaved += saved;
                }
                else
                {
                    errors.Add($"{Path.GetFileName(path)}: {error}");
                }
            }

            return (successCount, totalSaved, errors);
        }

        /// <summary>
        /// 將 1-6 等級對應到 ImageSharp 的 PngCompressionLevel
        /// </summary>
        private static PngCompressionLevel MapCompressionLevel(int level)
        {
            // ImageSharp's PngCompressionLevel ranges from Level0 (no compression) to Level9 (max compression)
            // Map our 1-6 scale: 1 -> Level3, 2 -> Level4, 3 -> Level5, 4 -> Level6, 5 -> Level7, 6 -> Level9
            return level switch
            {
                1 => PngCompressionLevel.Level3,
                2 => PngCompressionLevel.Level4,
                3 => PngCompressionLevel.Level5,
                4 => PngCompressionLevel.Level6,
                5 => PngCompressionLevel.Level7,
                6 => PngCompressionLevel.Level9,
                _ => PngCompressionLevel.Level6
            };
        }
    }
}
