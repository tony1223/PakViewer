using System;
using System.Diagnostics;
using System.IO;

namespace PakViewer.Utility
{
    /// <summary>
    /// PNG 無損壓縮工具（使用嵌入的 oxipng.exe）
    /// </summary>
    public static class PngOptimizer
    {
        private static string _oxipngPath;
        private static readonly object _lock = new object();

        /// <summary>
        /// 取得 oxipng.exe 路徑（首次使用時從嵌入資源解壓）
        /// </summary>
        public static string GetOxipngPath()
        {
            if (_oxipngPath != null && File.Exists(_oxipngPath))
                return _oxipngPath;

            lock (_lock)
            {
                if (_oxipngPath != null && File.Exists(_oxipngPath))
                    return _oxipngPath;

                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PakViewer");
                Directory.CreateDirectory(appData);

                _oxipngPath = Path.Combine(appData, "oxipng.exe");

                if (!File.Exists(_oxipngPath))
                {
                    // 從嵌入資源解壓
                    using (var stream = typeof(PngOptimizer).Assembly
                        .GetManifestResourceStream("PakViewer.Tools.oxipng.exe"))
                    {
                        if (stream == null)
                            throw new InvalidOperationException("找不到嵌入的 oxipng.exe 資源");

                        using (var file = File.Create(_oxipngPath))
                        {
                            stream.CopyTo(file);
                        }
                    }
                }

                return _oxipngPath;
            }
        }

        /// <summary>
        /// 壓縮單個 PNG 檔案（直接修改原檔）
        /// </summary>
        /// <param name="pngPath">PNG 檔案路徑</param>
        /// <param name="level">壓縮等級 (1-6, 預設 4)</param>
        /// <returns>(是否成功, 節省的位元組數)</returns>
        public static (bool success, long savedBytes, string error) Optimize(string pngPath, int level = 4)
        {
            if (!File.Exists(pngPath))
                return (false, 0, "檔案不存在");

            try
            {
                var originalSize = new FileInfo(pngPath).Length;

                var psi = new ProcessStartInfo
                {
                    FileName = GetOxipngPath(),
                    Arguments = $"-o {level} --strip safe \"{pngPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(60000); // 60 秒超時

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        return (false, 0, "壓縮超時");
                    }

                    if (proc.ExitCode != 0)
                    {
                        var error = proc.StandardError.ReadToEnd();
                        return (false, 0, $"壓縮失敗: {error}");
                    }
                }

                var newSize = new FileInfo(pngPath).Length;
                return (true, originalSize - newSize, null);
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
            // 建立暫存檔
            var tempFile = Path.Combine(Path.GetTempPath(), $"pakviewer_png_{Guid.NewGuid():N}.png");

            try
            {
                File.WriteAllBytes(tempFile, pngData);

                var (success, savedBytes, error) = Optimize(tempFile, level);

                if (!success)
                    return (pngData, 0, error);

                var optimizedData = File.ReadAllBytes(tempFile);
                return (optimizedData, savedBytes, null);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
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
    }
}
