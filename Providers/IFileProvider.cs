using System;
using System.Collections.Generic;

namespace PakViewer.Providers
{
    /// <summary>
    /// 檔案提供者介面 - 統一不同來源的檔案存取
    /// </summary>
    public interface IFileProvider : IDisposable
    {
        /// <summary>
        /// Provider 顯示名稱
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 檔案數量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 檔案列表
        /// </summary>
        IReadOnlyList<FileEntry> Files { get; }

        /// <summary>
        /// 根據索引提取檔案內容
        /// </summary>
        byte[] Extract(int index);

        /// <summary>
        /// 根據 FileEntry 提取檔案內容
        /// </summary>
        byte[] Extract(FileEntry entry);

        /// <summary>
        /// 取得所有副檔名
        /// </summary>
        IEnumerable<string> GetExtensions();

        /// <summary>
        /// 取得來源選項 (用於下拉選單)
        /// </summary>
        IEnumerable<string> GetSourceOptions();

        /// <summary>
        /// 設定目前選取的來源選項
        /// </summary>
        void SetSourceOption(string option);

        /// <summary>
        /// 取得目前選取的來源選項
        /// </summary>
        string CurrentSourceOption { get; }

        /// <summary>
        /// 是否有多個來源選項
        /// </summary>
        bool HasMultipleSourceOptions { get; }
    }
}
