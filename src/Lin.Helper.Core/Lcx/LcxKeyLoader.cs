using System;
using System.Collections.Generic;
using System.IO;

namespace Lin.Helper.Core.Lcx
{
    /// <summary>
    /// 從 lcx.key 檔案載入 ChaCha20-Poly1305 金鑰。
    /// 格式: 每行一個 hex 編碼的 32-byte 金鑰 (64 hex chars)，# 開頭為註解。
    /// </summary>
    public static class LcxKeyLoader
    {
        public const string KeyFileName = "lcx.key";

        /// <summary>
        /// 嘗試從指定目錄載入 lcx.key。
        /// 找不到檔案或無有效金鑰時回傳 null。
        /// </summary>
        public static byte[][] TryLoadFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return null;

            string keyFilePath = Path.Combine(directory, KeyFileName);
            return TryLoadFromFile(keyFilePath);
        }

        /// <summary>
        /// 嘗試從指定路徑載入金鑰檔。
        /// 找不到檔案或無有效金鑰時回傳 null。
        /// </summary>
        public static byte[][] TryLoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            var keys = new List<byte[]>();
            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith("#")) continue;

                try
                {
                    var key = Convert.FromHexString(trimmed);
                    if (key.Length == 32)
                        keys.Add(key);
                }
                catch
                {
                    // Skip invalid hex lines
                }
            }

            return keys.Count > 0 ? keys.ToArray() : null;
        }
    }
}
