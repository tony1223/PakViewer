using System;
using Eto.Forms;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 檔案預覽器基類，提供預設實作
    /// </summary>
    public abstract class BaseViewer : IFileViewer
    {
        protected Control _control;
        protected byte[] _data;
        protected string _fileName;
        protected bool _hasChanges;
        protected FileContext _context;

        public event EventHandler<SaveRequestedEventArgs> SaveRequested;

        /// <summary>
        /// 檔案上下文 (追蹤加密狀態等)
        /// </summary>
        public FileContext Context => _context;

        protected void OnSaveRequested(byte[] data)
        {
            SaveRequested?.Invoke(this, new SaveRequestedEventArgs(data));
        }

        public abstract string[] SupportedExtensions { get; }

        public virtual bool CanEdit => false;

        public virtual bool HasChanges => _hasChanges;

        public virtual bool CanSearch => false;

        public abstract void LoadData(byte[] data, string fileName);

        public virtual Control GetControl() => _control;

        public virtual byte[] GetModifiedData() => _data;

        public virtual Control GetSearchToolbar() => null;

        public virtual Control GetEditToolbar() => null;

        public virtual string GetTextContent(byte[] data, string fileName) => null;

        public virtual void Dispose()
        {
            _control?.Dispose();
            _control = null;
            _data = null;
        }
    }
}
