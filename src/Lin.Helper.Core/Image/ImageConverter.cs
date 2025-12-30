using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Lin.Helper.Core.Image
{
    /// <summary>
    /// Lineage 1 圖片格式轉換工具
    /// </summary>
    public static class ImageConverter
    {
        /// <summary>
        /// 建立 16bpp RGB555 點陣圖
        /// </summary>
        public static Bitmap CreateBitmap(int width, int height, byte[] srcData, int index, int maskColor)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format16bppRgb555);
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            int stride = bmpData.Stride;
            byte[] buffer = new byte[stride * height];

            if (srcData.Length - index == buffer.Length)
            {
                Array.Copy(srcData, index, buffer, 0, buffer.Length);
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(srcData, index + y * width * 2, buffer, y * stride, width * 2);
                }
            }

            Marshal.Copy(buffer, 0, bmpData.Scan0, srcData.Length - index);
            bitmap.UnlockBits(bmpData);

            if (maskColor >= 0)
            {
                bitmap.MakeTransparent(Rgb555ToColor(maskColor));
            }

            return bitmap;
        }

        /// <summary>
        /// RGB555 轉換為 Color
        /// </summary>
        public static Color Rgb555ToColor(int rgb555)
        {
            int r = (rgb555 & 31744) >> 10;
            int g = (rgb555 & 992) >> 5;
            int b = rgb555 & 31;
            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// 載入 IMG 格式圖片
        /// </summary>
        public static Bitmap LoadImg(byte[] imgData)
        {
            int width = BitConverter.ToInt16(imgData, 0);
            int height = BitConverter.ToInt16(imgData, 2);
            int maskColor = BitConverter.ToInt16(imgData, 4) == 1
                ? BitConverter.ToUInt16(imgData, 6)
                : -1;

            return CreateBitmap(width, height, imgData, 8, maskColor);
        }

        /// <summary>
        /// 載入 L1 圖片格式 (RLE 壓縮)
        /// </summary>
        public static L1Image LoadL1Image(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                L1Image image;
                image.XOffset = reader.ReadByte();
                image.YOffset = reader.ReadByte();

                int width = reader.ReadByte();
                int height = reader.ReadByte();

                if (width == 0 || height == 0)
                {
                    image.Image = null;
                    return image;
                }

                image.Image = new Bitmap(width, height, PixelFormat.Format16bppRgb555);
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData bmpData = image.Image.LockBits(rect, ImageLockMode.WriteOnly, image.Image.PixelFormat);

                int stride = bmpData.Stride;
                byte[] buffer = new byte[height * stride];

                for (int y = 0; y < height; y++)
                {
                    int segmentCount = reader.ReadByte();
                    int x = 0;

                    for (int seg = 0; seg < segmentCount; seg++)
                    {
                        x += reader.ReadByte();
                        int pixelCount = reader.ReadByte() * 2;
                        byte[] pixels = reader.ReadBytes(pixelCount);
                        Array.Copy(pixels, 0, buffer, y * stride + x, pixelCount);
                        x += pixelCount;
                    }
                }

                Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
                image.Image.UnlockBits(bmpData);

                return image;
            }
        }

        /// <summary>
        /// 載入 L1 圖片並放置到指定大小的畫布
        /// </summary>
        public static L1Image LoadL1Image(byte[] data, int canvasWidth, int canvasHeight)
        {
            Bitmap canvas = new Bitmap(canvasWidth, canvasHeight);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                L1Image image = LoadL1Image(data);
                if (image.Image != null)
                {
                    g.DrawImageUnscaled(image.Image, image.XOffset, image.YOffset);
                }
                image.Image = canvas;
                return image;
            }
        }

        /// <summary>
        /// 載入 TBT 格式圖片
        /// </summary>
        public static Bitmap LoadTbt(byte[] tbtData)
        {
            return LoadL1Image(tbtData).Image;
        }

        /// <summary>
        /// 載入 TIL 格式圖片 (地圖圖塊)
        /// </summary>
        public static TileSet LoadTil(byte[] tilData)
        {
            using (var reader = new BinaryReader(new MemoryStream(tilData)))
            {
                int tileCount = reader.ReadInt16();
                int unknown = reader.ReadInt16();

                int[] offsets = new int[tileCount + 1];
                int dataStart = 4 + (tileCount + 1) * 4;

                for (int i = 0; i <= tileCount; i++)
                {
                    offsets[i] = dataStart + reader.ReadInt32();
                }

                var tileSet = new TileSet
                {
                    TileCount = tileCount,
                    Tiles = new Bitmap[tileCount]
                };

                for (int i = 0; i < tileCount; i++)
                {
                    reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                    int tileType = reader.ReadByte();
                    int dataLength = offsets[i + 1] - (int)reader.BaseStream.Position;
                    byte[] tileData = reader.ReadBytes(dataLength);

                    var image = LoadL1Image(tileData, 24, 24);
                    tileSet.Tiles[i] = image.Image;
                }

                return tileSet;
            }
        }
    }

    /// <summary>
    /// L1 圖片結構
    /// </summary>
    public struct L1Image
    {
        public int XOffset;
        public int YOffset;
        public Bitmap Image;
    }

    /// <summary>
    /// 地圖圖塊集合
    /// </summary>
    public class TileSet
    {
        public int TileCount { get; set; }
        public Bitmap[] Tiles { get; set; }
    }
}
