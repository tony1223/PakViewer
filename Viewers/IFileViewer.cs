using System;
using System.Text;
using Eto.Forms;
using Lin.Helper.Core.Pak;
using Lin.Helper.Core.Xml;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 檔案上下文 - 追蹤檔案的原始狀態和加密資訊
    /// </summary>
    public class FileContext
    {
        public byte[] OriginalData { get; set; }      // 原始資料 (PAK解密後)
        public byte[] DisplayData { get; set; }        // 顯示用資料 (完全解密)
        public string FileName { get; set; }
        public PakFile SourcePak { get; set; }
        public int FileIndex { get; set; }

        // 加密狀態
        public bool IsXmlEncrypted { get; set; }       // XML 層是否加密
        public Encoding FileEncoding { get; set; }     // 檔案 encoding

        /// <summary>
        /// 準備儲存資料 - 依原始狀態還原加密
        /// </summary>
        public byte[] PrepareForSave(byte[] editedData)
        {
            var result = editedData;

            // 如果原本是 XML 加密的，加密回去
            if (IsXmlEncrypted)
            {
                result = XmlCracker.Encrypt(result);
            }

            return result;
        }
    }

    /// <summary>
    /// 儲存請求事件參數
    /// </summary>
    public class SaveRequestedEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public FileContext Context { get; }

        public SaveRequestedEventArgs(byte[] data) => Data = data;

        public SaveRequestedEventArgs(byte[] data, FileContext context)
        {
            Data = data;
            Context = context;
        }
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

        /// <summary>
        /// 取得編輯工具列（如果支援編輯）
        /// </summary>
        Control GetEditToolbar();

        /// <summary>
        /// 取得檔案的文字內容（用於內容搜尋）
        /// 回傳 null 表示不支援文字搜尋
        /// </summary>
        /// <param name="data">檔案二進制資料</param>
        /// <param name="fileName">檔案名稱</param>
        string GetTextContent(byte[] data, string fileName);
    }
}
