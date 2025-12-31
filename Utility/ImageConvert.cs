// ImageConvert - UI 適配層
// 實際圖片轉換邏輯由 Lin.Helper.Core.Image.ImageConverter 提供

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreImageConverter = Lin.Helper.Core.Image.ImageConverter;
using CoreL1Image = Lin.Helper.Core.Image.L1Image;
using CoreTileSet = Lin.Helper.Core.Image.TileSet;
using Image = System.Drawing.Image;

namespace PakViewer.Utility
{
  /// <summary>
  /// 圖片格式轉換 (UI 適配層)
  /// </summary>
  public static class ImageConvert
  {
    /// <summary>
    /// 將 ImageSharp Image 轉換為 System.Drawing.Bitmap
    /// </summary>
    private static Bitmap ToBitmap(Image<Rgba32> image)
    {
      if (image == null) return null;
      using (var ms = new MemoryStream())
      {
        image.SaveAsPng(ms);
        ms.Position = 0;
        return new Bitmap(ms);
      }
    }

    /// <summary>
    /// 建立 16bpp RGB555 點陣圖
    /// </summary>
    public static Bitmap CreateBMP(int width, int height, byte[] srcdata, int index, int MaskColor)
    {
      var img = CoreImageConverter.CreateBitmap(width, height, srcdata, index, MaskColor);
      return ToBitmap(img);
    }

    /// <summary>
    /// RGB555 轉換為 Color
    /// </summary>
    public static System.Drawing.Color Rgb555ToARGB(int Rgb555)
    {
      var rgba = CoreImageConverter.Rgb555ToRgba32(Rgb555);
      return System.Drawing.Color.FromArgb(rgba.A, rgba.R, rgba.G, rgba.B);
    }

    /// <summary>
    /// 載入 IMG 格式圖片
    /// </summary>
    public static Bitmap Load_IMG(byte[] imgdata)
    {
      var img = CoreImageConverter.LoadImg(imgdata);
      return ToBitmap(img);
    }

    /// <summary>
    /// 載入 L1 圖片格式 (RLE 壓縮)
    /// </summary>
    public static L1Image LoadImage(byte[] data)
    {
      var coreImage = CoreImageConverter.LoadL1Image(data);
      return new L1Image
      {
        x_offset = coreImage.XOffset,
        y_offset = coreImage.YOffset,
        image = ToBitmap(coreImage.Image)
      };
    }

    /// <summary>
    /// 載入 L1 圖片並放置到指定大小的畫布
    /// </summary>
    public static L1Image LoadImage(byte[] data, int width, int height)
    {
      var coreImage = CoreImageConverter.LoadL1Image(data, width, height);
      return new L1Image
      {
        x_offset = coreImage.XOffset,
        y_offset = coreImage.YOffset,
        image = ToBitmap(coreImage.Image)
      };
    }

    /// <summary>
    /// 載入 TBT 格式圖片
    /// </summary>
    public static Bitmap Load_TBT(byte[] tbtdata)
    {
      var img = CoreImageConverter.LoadTbt(tbtdata);
      return ToBitmap(img);
    }

    /// <summary>
    /// 載入 TIL 格式圖片 (地圖圖塊) - 返回第一個圖塊
    /// </summary>
    public static Bitmap Load_TIL(byte[] tildata)
    {
      var tileSet = CoreImageConverter.LoadTil(tildata);
      return tileSet.TileCount > 0 ? ToBitmap(tileSet.Tiles[0]) : null;
    }

    /// <summary>
    /// 載入 TIL 格式圖片 - 返回所有圖塊
    /// </summary>
    public static CoreTileSet Load_TIL_All(byte[] tildata)
    {
      return CoreImageConverter.LoadTil(tildata);
    }

    /// <summary>
    /// L1 圖片結構 (相容舊介面)
    /// </summary>
    public struct L1Image
    {
      public int x_offset;
      public int y_offset;
      public Bitmap image;
    }
  }
}
