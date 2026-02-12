using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Lin.Helper.Core.Image
{
    /// <summary>
    /// Lineage 1 圖片格式轉換工具 (跨平台版本)
    /// </summary>
    public static class ImageConverter
    {
        #region ImageSharp 版本

        /// <summary>
        /// 建立圖片從 RGB555 資料
        /// </summary>
        public static Image<Rgba32> CreateBitmap(int width, int height, byte[] srcData, int index, int maskColor)
        {
            var image = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = index + (y * width + x) * 2;
                    if (offset + 1 >= srcData.Length) break;

                    ushort rgb555 = (ushort)(srcData[offset] | (srcData[offset + 1] << 8));
                    var color = Rgb555ToRgba32(rgb555);

                    // 處理透明遮罩色
                    if (maskColor >= 0 && rgb555 == maskColor)
                    {
                        color = new Rgba32(0, 0, 0, 0);
                    }

                    image[x, y] = color;
                }
            }

            return image;
        }

        /// <summary>
        /// RGB555 轉換為 Rgba32
        /// </summary>
        public static Rgba32 Rgb555ToRgba32(int rgb555)
        {
            // RGB555: 0RRRRRGGGGGBBBBB
            int r = (rgb555 >> 10) & 0x1F;
            int g = (rgb555 >> 5) & 0x1F;
            int b = rgb555 & 0x1F;

            // 5-bit to 8-bit conversion
            r = (r << 3) | (r >> 2);
            g = (g << 3) | (g >> 2);
            b = (b << 3) | (b >> 2);

            return new Rgba32((byte)r, (byte)g, (byte)b, 255);
        }

        /// <summary>
        /// RGB555 轉換為 Color (相容舊介面)
        /// </summary>
        public static Rgba32 Rgb555ToColor(int rgb555)
        {
            return Rgb555ToRgba32(rgb555);
        }

        /// <summary>
        /// RGB565 轉換為 Rgba32
        /// </summary>
        public static Rgba32 Rgb565ToRgba32(int rgb565)
        {
            // RGB565: RRRRRGGGGGGBBBBB
            int r = (rgb565 >> 11) & 0x1F;
            int g = (rgb565 >> 5) & 0x3F;
            int b = rgb565 & 0x1F;

            r = (r << 3) | (r >> 2);
            g = (g << 2) | (g >> 4);
            b = (b << 3) | (b >> 2);

            return new Rgba32((byte)r, (byte)g, (byte)b, 255);
        }

        /// <summary>
        /// RGB565 轉換為 RGB555
        /// </summary>
        public static ushort Rgb565ToRgb555(ushort rgb565)
        {
            int r = (rgb565 >> 11) & 0x1F;
            int g = (rgb565 >> 5) & 0x3F;
            int b = rgb565 & 0x1F;
            return (ushort)((r << 10) | ((g >> 1) << 5) | b);
        }

        #endregion

        #region byte[] 版本 (無 ImageSharp 依賴)

        /// <summary>
        /// RGB565 轉換為 RGBA byte 陣列 (4 bytes: R, G, B, A)
        /// </summary>
        public static void Rgb565ToRgba(int rgb565, byte[] dest, int destIndex)
        {
            int r = (rgb565 >> 11) & 0x1F;
            int g = (rgb565 >> 5) & 0x3F;
            int b = rgb565 & 0x1F;

            dest[destIndex] = (byte)((r << 3) | (r >> 2));
            dest[destIndex + 1] = (byte)((g << 2) | (g >> 4));
            dest[destIndex + 2] = (byte)((b << 3) | (b >> 2));
            dest[destIndex + 3] = 255;
        }

        /// <summary>
        /// RGB555 轉換為 RGBA byte 陣列 (4 bytes: R, G, B, A)
        /// </summary>
        public static void Rgb555ToRgba(int rgb555, byte[] dest, int destIndex)
        {
            int r = (rgb555 >> 10) & 0x1F;
            int g = (rgb555 >> 5) & 0x1F;
            int b = rgb555 & 0x1F;

            dest[destIndex] = (byte)((r << 3) | (r >> 2));
            dest[destIndex + 1] = (byte)((g << 3) | (g >> 2));
            dest[destIndex + 2] = (byte)((b << 3) | (b >> 2));
            dest[destIndex + 3] = 255;
        }

        /// <summary>
        /// 建立 RGBA byte[] 從 RGB555 資料
        /// </summary>
        /// <returns>RGBA 格式的 byte[] (width * height * 4 bytes)</returns>
        public static byte[] CreateRgbaBytes(int width, int height, byte[] srcData, int index, int maskColor)
        {
            byte[] result = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = index + (y * width + x) * 2;
                    int destOffset = (y * width + x) * 4;

                    if (srcOffset + 1 >= srcData.Length) break;

                    ushort rgb555 = (ushort)(srcData[srcOffset] | (srcData[srcOffset + 1] << 8));

                    if (maskColor >= 0 && rgb555 == maskColor)
                    {
                        // 透明
                        result[destOffset] = 0;
                        result[destOffset + 1] = 0;
                        result[destOffset + 2] = 0;
                        result[destOffset + 3] = 0;
                    }
                    else
                    {
                        Rgb555ToRgba(rgb555, result, destOffset);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 載入 IMG 格式為 RawImage (byte[])
        /// </summary>
        public static RawImage LoadImgRaw(byte[] imgData)
        {
            int width = BitConverter.ToInt16(imgData, 0);
            int height = BitConverter.ToInt16(imgData, 2);
            int maskColor = BitConverter.ToInt16(imgData, 4) == 1
                ? BitConverter.ToUInt16(imgData, 6)
                : -1;

            return new RawImage
            {
                Width = width,
                Height = height,
                Pixels = CreateRgbaBytes(width, height, imgData, 8, maskColor)
            };
        }

        /// <summary>
        /// 載入 L1 圖片格式為 RawL1Image (byte[])
        /// </summary>
        public static RawL1Image LoadL1ImageRaw(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                RawL1Image result;
                result.XOffset = reader.ReadByte();
                result.YOffset = reader.ReadByte();

                int width = reader.ReadByte();
                int height = reader.ReadByte();

                if (width == 0 || height == 0)
                {
                    result.Width = 0;
                    result.Height = 0;
                    result.Pixels = null;
                    return result;
                }

                result.Width = width;
                result.Height = height;

                // 建立 RGB555 緩衝區
                ushort[] pixels = new ushort[width * height];

                for (int y = 0; y < height; y++)
                {
                    int segmentCount = reader.ReadByte();
                    int x = 0;

                    for (int seg = 0; seg < segmentCount; seg++)
                    {
                        x += reader.ReadByte();
                        int pixelCount = reader.ReadByte();

                        for (int p = 0; p < pixelCount; p++)
                        {
                            if (x + p < width)
                            {
                                ushort rgb555 = reader.ReadUInt16();
                                pixels[y * width + x + p] = rgb555;
                            }
                            else
                            {
                                reader.ReadUInt16();
                            }
                        }
                        x += pixelCount;
                    }
                }

                // 轉換為 RGBA byte[]
                result.Pixels = new byte[width * height * 4];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int destOffset = i * 4;
                    ushort rgb555 = pixels[i];
                    if (rgb555 == 0)
                    {
                        // 透明
                        result.Pixels[destOffset] = 0;
                        result.Pixels[destOffset + 1] = 0;
                        result.Pixels[destOffset + 2] = 0;
                        result.Pixels[destOffset + 3] = 0;
                    }
                    else
                    {
                        Rgb555ToRgba(rgb555, result.Pixels, destOffset);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 載入 L1 圖片並放置到指定大小的畫布 (byte[] 版本)
        /// </summary>
        public static RawL1Image LoadL1ImageRaw(byte[] data, int canvasWidth, int canvasHeight)
        {
            RawL1Image image = LoadL1ImageRaw(data);

            byte[] canvas = new byte[canvasWidth * canvasHeight * 4];
            // 初始化為全透明

            if (image.Pixels != null)
            {
                // 將圖片複製到畫布上
                for (int y = 0; y < image.Height && y + image.YOffset < canvasHeight; y++)
                {
                    for (int x = 0; x < image.Width && x + image.XOffset < canvasWidth; x++)
                    {
                        int srcOffset = (y * image.Width + x) * 4;
                        int destOffset = ((y + image.YOffset) * canvasWidth + (x + image.XOffset)) * 4;

                        canvas[destOffset] = image.Pixels[srcOffset];
                        canvas[destOffset + 1] = image.Pixels[srcOffset + 1];
                        canvas[destOffset + 2] = image.Pixels[srcOffset + 2];
                        canvas[destOffset + 3] = image.Pixels[srcOffset + 3];
                    }
                }
            }

            return new RawL1Image
            {
                XOffset = image.XOffset,
                YOffset = image.YOffset,
                Width = canvasWidth,
                Height = canvasHeight,
                Pixels = canvas
            };
        }

        /// <summary>
        /// 載入 TBT 格式為 RawImage (byte[])
        /// </summary>
        public static RawImage LoadTbtRaw(byte[] tbtData)
        {
            var l1 = LoadL1ImageRaw(tbtData);
            return new RawImage
            {
                Width = l1.Width,
                Height = l1.Height,
                Pixels = l1.Pixels
            };
        }

        /// <summary>
        /// 載入 TIL 格式為 RawTileSet (byte[])
        /// </summary>
        public static RawTileSet LoadTilRaw(byte[] tilData)
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

                var tileSet = new RawTileSet
                {
                    TileCount = tileCount,
                    Tiles = new RawImage[tileCount]
                };

                for (int i = 0; i < tileCount; i++)
                {
                    reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                    int tileType = reader.ReadByte();
                    int dataLength = offsets[i + 1] - (int)reader.BaseStream.Position;
                    byte[] tileData = reader.ReadBytes(dataLength);

                    var image = LoadL1ImageRaw(tileData, 24, 24);
                    tileSet.Tiles[i] = new RawImage
                    {
                        Width = image.Width,
                        Height = image.Height,
                        Pixels = image.Pixels
                    };
                }

                return tileSet;
            }
        }

        #endregion

        /// <summary>
        /// 載入 IMG 格式圖片
        /// </summary>
        public static Image<Rgba32> LoadImg(byte[] imgData)
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
                L1Image result;
                result.XOffset = reader.ReadByte();
                result.YOffset = reader.ReadByte();

                int headerWidth = reader.ReadByte();
                int headerHeight = reader.ReadByte();

                if (headerWidth == 0 || headerHeight == 0)
                {
                    result.Image = null;
                    return result;
                }

                // 先掃描一次來確定實際需要的寬度
                // skip 值是 bytes 單位，需要除以 2 轉成 pixels
                long startPos = reader.BaseStream.Position;
                int maxX = headerWidth;

                for (int y = 0; y < headerHeight; y++)
                {
                    int segmentCount = reader.ReadByte();
                    int x = 0;
                    for (int seg = 0; seg < segmentCount; seg++)
                    {
                        // skip 值是 bytes 單位，需要除以 2 轉成 pixels
                        int skip = reader.ReadByte() / 2;
                        int pixelCount = reader.ReadByte();

                        x += skip;
                        int endX = x + pixelCount;
                        if (endX > maxX) maxX = endX;
                        reader.BaseStream.Position += pixelCount * 2;
                        x = endX;
                    }
                }

                int actualWidth = maxX;
                int height = headerHeight;

                // 回到資料起始位置重新解析
                reader.BaseStream.Position = startPos;

                // 建立 RGB555 緩衝區
                ushort[] pixels = new ushort[actualWidth * height];

                for (int y = 0; y < height; y++)
                {
                    int segmentCount = reader.ReadByte();
                    int x = 0;

                    for (int seg = 0; seg < segmentCount; seg++)
                    {
                        // skip 值是 bytes 單位，需要除以 2 轉成 pixels
                        int skip = reader.ReadByte() / 2;
                        int pixelCount = reader.ReadByte();

                        x += skip;

                        for (int p = 0; p < pixelCount; p++)
                        {
                            ushort rgb555 = reader.ReadUInt16();
                            if (x + p < actualWidth)
                            {
                                pixels[y * actualWidth + x + p] = rgb555;
                            }
                        }
                        x += pixelCount;
                    }
                }

                // 轉換為 Image<Rgba32>
                result.Image = new Image<Rgba32>(actualWidth, height);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < actualWidth; x++)
                    {
                        ushort rgb555 = pixels[y * actualWidth + x];
                        if (rgb555 == 0)
                        {
                            result.Image[x, y] = new Rgba32(0, 0, 0, 0); // 透明
                        }
                        else
                        {
                            result.Image[x, y] = Rgb555ToRgba32(rgb555);
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 載入 L1 圖片並放置到指定大小的畫布
        /// </summary>
        public static L1Image LoadL1Image(byte[] data, int canvasWidth, int canvasHeight)
        {
            var canvas = new Image<Rgba32>(canvasWidth, canvasHeight, new Rgba32(0, 0, 0, 0));
            L1Image image = LoadL1Image(data);

            if (image.Image != null)
            {
                canvas.Mutate(ctx => ctx.DrawImage(image.Image, new Point(image.XOffset, image.YOffset), 1f));
                image.Image.Dispose();
            }

            image.Image = canvas;
            return image;
        }

        /// <summary>
        /// 載入 TBT 格式圖片
        /// </summary>
        public static Image<Rgba32> LoadTbt(byte[] tbtData)
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
                    Tiles = new Image<Rgba32>[tileCount]
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
        public Image<Rgba32> Image;
    }

    /// <summary>
    /// 地圖圖塊集合
    /// </summary>
    public class TileSet
    {
        public int TileCount { get; set; }
        public Image<Rgba32>[] Tiles { get; set; }
    }

    #region Raw (byte[]) 結構

    /// <summary>
    /// 原始圖片結構 (RGBA byte[])
    /// </summary>
    public struct RawImage
    {
        public int Width;
        public int Height;
        /// <summary>
        /// RGBA 格式像素資料 (Width * Height * 4 bytes)
        /// </summary>
        public byte[] Pixels;
    }

    /// <summary>
    /// 原始 L1 圖片結構 (RGBA byte[])
    /// </summary>
    public struct RawL1Image
    {
        public int XOffset;
        public int YOffset;
        public int Width;
        public int Height;
        /// <summary>
        /// RGBA 格式像素資料 (Width * Height * 4 bytes)
        /// </summary>
        public byte[] Pixels;
    }

    /// <summary>
    /// 原始地圖圖塊集合 (byte[] 版本)
    /// </summary>
    public class RawTileSet
    {
        public int TileCount { get; set; }
        public RawImage[] Tiles { get; set; }
    }

    #endregion
}
