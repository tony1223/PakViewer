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
    /// 載入 TIL 格式圖片 - 返回 sheet view with block numbers and grid
    /// </summary>
    public static Bitmap Load_TIL_Sheet(byte[] tildata)
    {
      var blocks = Lin.Helper.Core.Tile.L1Til.Parse(tildata);

      const int BlockSize = 24;
      const int CellPadding = 2;      // 格線寬度
      const int LabelHeight = 14;     // 編號高度
      const int CellSize = BlockSize + CellPadding + LabelHeight;  // 40
      const int ColCount = 12;        // 每排 12 個
      int rowCount = (int)System.Math.Ceiling(blocks.Count / (double)ColCount);
      int sheetWidth = CellSize * ColCount;
      int sheetHeight = CellSize * rowCount;

      var bmp = new Bitmap(sheetWidth, sheetHeight);

      using (var g = Graphics.FromImage(bmp))
      {
        g.Clear(System.Drawing.Color.FromArgb(30, 30, 30));

        var gridPen = new Pen(System.Drawing.Color.FromArgb(60, 60, 60), 1);
        var font = new Font("Arial", 8f, FontStyle.Regular);
        var textBrush = new SolidBrush(System.Drawing.Color.FromArgb(180, 180, 180));

        for (int i = 0; i < blocks.Count; i++)
        {
          int col = i % ColCount;
          int row = i / ColCount;
          int cellX = col * CellSize;
          int cellY = row * CellSize;

          // 畫格線邊框
          g.DrawRectangle(gridPen, cellX, cellY, CellSize - 1, CellSize - 1);

          // 畫 block 編號
          string label = i.ToString();
          g.DrawString(label, font, textBrush, cellX + 2, cellY + 1);

          // 畫 block 圖像
          int blockX = cellX + CellPadding / 2;
          int blockY = cellY + LabelHeight;
          RenderBlockToBitmap(bmp, blocks[i], blockX, blockY);
        }

        gridPen.Dispose();
        font.Dispose();
        textBrush.Dispose();
      }

      return bmp;
    }

    private static void RenderBlockToBitmap(Bitmap bmp, byte[] blockData, int destX, int destY)
    {
      if (blockData == null || blockData.Length < 2)
        return;

      int blockType = blockData[0];
      bool isSimpleDiamond = blockType == 0 || blockType == 1 || blockType == 8 || blockType == 9 ||
                             blockType == 16 || blockType == 17;

      var canvas = new ushort[24 * 24];

      if (isSimpleDiamond)
      {
        int[] rowWidths = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 22, 20, 18, 16, 14, 12, 10, 8, 6, 4, 2 };
        int dataIdx = 1;

        for (int row = 0; row < 23 && dataIdx < blockData.Length - 1; row++)
        {
          int width = rowWidths[row];
          int startX = (blockType & 1) == 0 ? (24 - width) / 2 : 0;

          for (int col = 0; col < width && dataIdx + 1 < blockData.Length; col++)
          {
            ushort color = (ushort)(blockData[dataIdx] | (blockData[dataIdx + 1] << 8));
            dataIdx += 2;

            int px = startX + col;
            int py = row;
            if (px >= 0 && px < 24 && py >= 0 && py < 24)
              canvas[py * 24 + px] = color;
          }
        }
      }
      else if (blockData.Length >= 5)
      {
        int xOffset = blockData[1];
        int yOffsetBlock = blockData[2];
        int xxLen = blockData[3];
        int yLen = blockData[4];
        int idx = 5;

        for (int row = 0; row < yLen && idx < blockData.Length; row++)
        {
          if (idx >= blockData.Length) break;
          int segCount = blockData[idx++];

          int currentX = xOffset;
          for (int s = 0; s < segCount && idx < blockData.Length; s++)
          {
            if (idx + 1 >= blockData.Length) break;
            int skip = blockData[idx++];
            int count = blockData[idx++];

            currentX += skip / 2;

            for (int p = 0; p < count && idx + 1 < blockData.Length; p++)
            {
              ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
              idx += 2;

              int px = currentX + p;
              int py = yOffsetBlock + row;
              if (px >= 0 && px < 24 && py >= 0 && py < 24)
                canvas[py * 24 + px] = color;
            }
            currentX += count;
          }
        }
      }

      for (int y = 0; y < 24; y++)
      {
        for (int x = 0; x < 24; x++)
        {
          ushort rgb555 = canvas[y * 24 + x];
          System.Drawing.Color color;

          if (rgb555 == 0)
          {
            color = System.Drawing.Color.FromArgb(30, 30, 30);
          }
          else
          {
            int b5 = rgb555 & 0x1F;
            int g5 = (rgb555 >> 5) & 0x1F;
            int r5 = (rgb555 >> 10) & 0x1F;
            int r8 = (r5 << 3) | (r5 >> 2);
            int g8 = (g5 << 3) | (g5 >> 2);
            int b8 = (b5 << 3) | (b5 >> 2);
            color = System.Drawing.Color.FromArgb(r8, g8, b8);
          }

          int px = destX + x;
          int py = destY + y;
          if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
            bmp.SetPixel(px, py, color);
        }
      }
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
