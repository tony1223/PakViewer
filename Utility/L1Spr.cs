// L1Spr - UI 適配層
// 實際 SPR 解析邏輯由 Lin.Helper.Core.Sprite.SprReader 提供

using System.Drawing;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreSprReader = Lin.Helper.Core.Sprite.SprReader;
using CoreSprFrame = Lin.Helper.Core.Sprite.SprFrame;
using Image = System.Drawing.Image;

namespace PakViewer.Utility
{
  /// <summary>
  /// SPR 圖檔解析 (UI 適配層)
  /// </summary>
  public static class L1Spr
  {
    /// <summary>
    /// 將 ImageSharp Image 轉換為 System.Drawing.Image
    /// </summary>
    private static Image ToBitmap(Image<Rgba32> image)
    {
      if (image == null) return null;
      using (var ms = new MemoryStream())
      {
        image.SaveAsPng(ms);
        ms.Position = 0;
        return System.Drawing.Image.FromStream(new MemoryStream(ms.ToArray()));
      }
    }

    /// <summary>
    /// 載入 SPR 檔案
    /// </summary>
    public static Frame[] Load(byte[] sprdata)
    {
      var coreFrames = CoreSprReader.Load(sprdata);
      if (coreFrames == null) return null;

      var frames = new Frame[coreFrames.Length];
      for (int i = 0; i < coreFrames.Length; i++)
      {
        frames[i] = new Frame
        {
          x_offset = coreFrames[i].XOffset,
          y_offset = coreFrames[i].YOffset,
          width = coreFrames[i].Width,
          height = coreFrames[i].Height,
          unknow_1 = coreFrames[i].Unknown1,
          unknow_2 = coreFrames[i].Unknown2,
          type = coreFrames[i].Type,
          maskcolor = coreFrames[i].MaskColor,
          image = ToBitmap(coreFrames[i].Image)
        };
      }
      return frames;
    }

    /// <summary>
    /// 建立點陣圖 (相容舊介面)
    /// </summary>
    public static Bitmap CreateBitmap(Frame FrameData, byte[] bmpdata)
    {
      if (bmpdata == null)
        return null;
      return ImageConvert.CreateBMP(FrameData.width, FrameData.height, bmpdata, 0, FrameData.maskcolor);
    }

    /// <summary>
    /// SPR 幀結構 (相容舊介面)
    /// </summary>
    public struct Frame
    {
      public int x_offset;
      public int y_offset;
      public int width;
      public int height;
      public ushort unknow_1;
      public ushort unknow_2;
      public int type;
      public ushort maskcolor;
      public Image image;
    }
  }
}
