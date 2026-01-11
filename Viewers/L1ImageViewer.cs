using System.IO;
using Eto.Drawing;
using Eto.Forms;
using CoreImageConverter = Lin.Helper.Core.Image.ImageConverter;
using SixLabors.ImageSharp.Formats.Png;

namespace PakViewer.Viewers
{
    /// <summary>
    /// Lineage 圖片格式預覽器 (.tbt, .img)
    /// </summary>
    public class L1ImageViewer : BaseViewer
    {
        private ImageView _imageView;

        public override string[] SupportedExtensions => new[] { ".tbt", ".img" };

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;

            try
            {
                var ext = System.IO.Path.GetExtension(fileName)?.ToLower() ?? "";
                SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image = null;

                // 根據副檔名使用對應的載入方法
                if (ext == ".tbt")
                {
                    image = CoreImageConverter.LoadTbt(data);
                }
                else if (ext == ".img")
                {
                    image = CoreImageConverter.LoadImg(data);
                }
                else
                {
                    // 預設使用 L1Image 格式
                    var l1Image = CoreImageConverter.LoadL1Image(data);
                    image = l1Image.Image;
                }

                if (image != null)
                {
                    using var ms = new MemoryStream();
                    image.Save(ms, new PngEncoder());
                    ms.Position = 0;

                    _imageView = new ImageView { Image = new Bitmap(ms) };
                    _control = _imageView;

                    image.Dispose();
                }
                else
                {
                    _control = new Label { Text = "Failed to load L1Image" };
                }
            }
            catch (System.Exception ex)
            {
                _control = new Label { Text = $"Error: {ex.Message}" };
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
