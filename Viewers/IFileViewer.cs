using System;
using Eto.Forms;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 儲存請求事件參數
    /// </summary>
    public class SaveRequestedEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public SaveRequestedEventArgs(byte[] data) => Data = data;
    }

    /// <summary>
    /// 檔案預覽器介面
    /// </summary>
    public interface IFileViewer : System.IDisposable
    {
        /// <summary>
        /// 當使用者點擊儲存時觸發，由主程式處理實際儲存邏輯
        /// </summary>
        event EventHandler<SaveRequestedEventArgs> SaveRequested;
        /// <summary>
        /// 支援的副檔名（小寫，含點號，如 ".spr"）
        /// </summary>
        string[] SupportedExtensions { get; }

        /// <summary>
        /// 載入檔案資料
        /// </summary>
        /// <param name="data">檔案二進制資料</param>
        /// <param name="fileName">檔案名稱（用於編碼偵測等）</param>
        void LoadData(byte[] data, string fileName);

        /// <summary>
        /// 取得 UI 控件
        /// </summary>
        Control GetControl();

        /// <summary>
        /// 是否支援編輯
        /// </summary>
        bool CanEdit { get; }

        /// <summary>
        /// 是否有未儲存的變更
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// 取得修改後的資料（如果支援編輯）
        /// </summary>
        byte[] GetModifiedData();

        /// <summary>
        /// 取得搜尋控制項（如果支援搜尋）
        /// </summary>
        Control GetSearchToolbar();

        /// <summary>
        /// 是否支援搜尋
        /// </summary>
        bool CanSearch { get; }
    }
}
