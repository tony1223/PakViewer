using System.IO;
using Eto.Drawing;
using Eto.Forms;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// 圖片預覽器
    /// </summary>
    public class ImageViewer : BaseViewer
    {
        private ImageView _imageView;

        public override string[] SupportedExtensions => new[] { ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".webp" };

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;

            try
            {
                using var ms = new MemoryStream(data);
                _imageView = new ImageView { Image = new Bitmap(ms) };
                _control = _imageView;
            }
            catch
            {
                _control = new Label { Text = I18n.T("Error.LoadImage") };
            }
        }

        public override void Dispose()
        {
            _imageView?.Image?.Dispose();
            _imageView = null;
            base.Dispose();
        }
    }
}
