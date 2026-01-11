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

        public event EventHandler<SaveRequestedEventArgs> SaveRequested;

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

        public virtual void Dispose()
        {
            _control?.Dispose();
            _control = null;
            _data = null;
        }
    }
}
