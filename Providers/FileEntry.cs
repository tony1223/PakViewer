namespace PakViewer.Providers
{
    /// <summary>
    /// 檔案項目 - 統一的檔案資訊結構
    /// </summary>
    public class FileEntry
    {
        /// <summary>
        /// 檔案索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 檔案名稱
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 檔案大小 (bytes)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 檔案在 PAK 內的偏移量 (PAK Provider 用)
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// 實際檔案路徑 (Folder/Single Provider 用)
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 來源名稱 (PAK 檔名 / 資料夾名)
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// 來源 Provider 參考
        /// </summary>
        public IFileProvider Source { get; set; }
    }
}
