using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Lin.Helper.Core.Image;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// Lineage 1 SPR (Sprite) 檔案讀取器
    /// </summary>
    public static class SprReader
    {
        /// <summary>
        /// 載入 SPR 檔案
        /// </summary>
        public static SprFrame[] Load(byte[] sprData)
        {
            ushort[] palette = null;
            ushort maskColor = 32768;

            ushort[] reservedColors = new ushort[]
            {
                31744, 768, 31, 32736, 1023, 31775, (ushort)short.MaxValue
            };

            using (var reader = new BinaryReader(new MemoryStream(sprData)))
            {
                bool hasPalette = false;
                byte frameCount = reader.ReadByte();

                // 檢查是否有調色盤
                if (frameCount == 255)
                {
                    hasPalette = true;
                    int paletteSize = reader.ReadByte();
                    if (paletteSize == 0) paletteSize = 256;

                    palette = new ushort[paletteSize];
                    for (int i = 0; i < paletteSize; i++)
                    {
                        palette[i] = reader.ReadUInt16();
                        int idx = Array.IndexOf(reservedColors, palette[i]);
                        if (idx >= 0) reservedColors[idx] = 0;
                    }

                    // 找一個未使用的遮罩色
                    foreach (ushort color in reservedColors)
                    {
                        if (color != 0)
                        {
                            maskColor = color;
                            break;
                        }
                    }

                    frameCount = reader.ReadByte();
                }

                var frames = new SprFrame[frameCount];
                var blockDefs = new BlockDef[frameCount][];

                // 讀取幀頭資訊
                for (int i = 0; i < frameCount; i++)
                {
                    frames[i].XOffset = reader.ReadInt16();
                    frames[i].YOffset = reader.ReadInt16();
                    frames[i].Width = reader.ReadInt16() - frames[i].XOffset + 1;
                    frames[i].Height = reader.ReadInt16() - frames[i].YOffset + 1;
                    frames[i].Unknown1 = reader.ReadUInt16();
                    frames[i].Unknown2 = reader.ReadUInt16();

                    int blockCount = reader.ReadUInt16();
                    if (blockCount > 0)
                    {
                        blockDefs[i] = new BlockDef[blockCount];
                        for (int j = 0; j < blockCount; j++)
                        {
                            blockDefs[i][j].A = reader.ReadSByte();
                            blockDefs[i][j].B = reader.ReadSByte();
                            blockDefs[i][j].FrameType = reader.ReadByte();
                            blockDefs[i][j].BlockId = reader.ReadUInt16();
                        }
                        frames[i].Type = blockDefs[i][0].FrameType;
                    }
                }

                // 讀取區塊偏移表
                int blockTableSize = reader.ReadInt32();
                int[] blockOffsets = new int[blockTableSize];
                for (int i = 0; i < blockTableSize; i++)
                {
                    blockOffsets[i] = reader.ReadInt32();
                }

                // 跳過未知資料
                reader.ReadInt32();

                int dataStart = (int)reader.BaseStream.Position;

                // 讀取區塊像素資料
                var blocks = new ushort[blockTableSize][,];
                for (int i = 0; i < blockTableSize; i++)
                {
                    blocks[i] = new ushort[24, 24];

                    // 初始化為透明
                    for (int y = 0; y < 24; y++)
                    {
                        for (int x = 0; x < 24; x++)
                        {
                            blocks[i][y, x] = 32768;
                        }
                    }

                    reader.BaseStream.Seek(dataStart + blockOffsets[i], SeekOrigin.Begin);

                    byte startX = reader.ReadByte();
                    byte startY = reader.ReadByte();
                    byte unknown = reader.ReadByte();
                    byte lineCount = reader.ReadByte();

                    for (int line = 0; line < lineCount; line++)
                    {
                        int x = startX;
                        byte segmentCount = reader.ReadByte();

                        for (int seg = 0; seg < segmentCount; seg++)
                        {
                            x += reader.ReadByte() / 2;
                            int pixelCount = reader.ReadByte();

                            for (int px = 0; px < pixelCount; px++)
                            {
                                ushort color;
                                if (hasPalette)
                                {
                                    color = palette[reader.ReadByte()];
                                }
                                else
                                {
                                    color = reader.ReadUInt16();
                                    int idx = Array.IndexOf(reservedColors, color);
                                    if (idx >= 0) reservedColors[idx] = 0;
                                }

                                blocks[i][line + startY, x] = color;
                                x++;
                            }
                        }
                    }
                }

                // 找最終遮罩色
                if (maskColor == 32768)
                {
                    foreach (ushort color in reservedColors)
                    {
                        if (color != 0)
                        {
                            maskColor = color;
                            break;
                        }
                    }
                }

                // 組合幀圖像 (使用擴展邊界，不裁切)
                for (int i = 0; i < frameCount; i++)
                {
                    if (blockDefs[i] == null) continue;

                    frames[i].MaskColor = maskColor;

                    // 計算擴展邊界
                    int minX = int.MaxValue, maxX = int.MinValue;
                    int minY = int.MaxValue, maxY = int.MinValue;

                    for (int b = 0; b < blockDefs[i].Length; b++)
                    {
                        int a = blockDefs[i][b].A;
                        int bVal = blockDefs[i][b].B;
                        int blockX = 24 * (bVal + a - a / 2);
                        int blockY = 12 * (bVal - a / 2);

                        minX = Math.Min(minX, blockX);
                        maxX = Math.Max(maxX, blockX + 23);
                        minY = Math.Min(minY, blockY);
                        maxY = Math.Max(maxY, blockY + 23);
                    }

                    int width = maxX - minX + 1;
                    int height = maxY - minY + 1;

                    // 更新 frame 資訊
                    frames[i].XOffset = minX;
                    frames[i].YOffset = minY;
                    frames[i].Width = width;
                    frames[i].Height = height;

                    int stride = (width + width % 2) * 2;
                    byte[] bmpData = new byte[height * stride];

                    // 填充遮罩色
                    for (int j = 0; j < bmpData.Length; j += 2)
                    {
                        bmpData[j] = (byte)(maskColor & 0xFF);
                        bmpData[j + 1] = (byte)((maskColor >> 8) & 0xFF);
                    }

                    // 繪製區塊
                    for (int b = 0; b < blockDefs[i].Length; b++)
                    {
                        int a = blockDefs[i][b].A;
                        int bVal = blockDefs[i][b].B;

                        int blockX = 24 * (bVal + a - a / 2);
                        int blockY = 12 * (bVal - a / 2);

                        var block = blocks[blockDefs[i][b].BlockId];

                        for (int by = 0; by < 24; by++)
                        {
                            for (int bx = 0; bx < 24; bx++)
                            {
                                ushort color = block[by, bx];
                                if (color == 32768) continue;

                                int px = blockX + bx - minX;
                                int py = blockY + by - minY;

                                int offset = (py * (width + width % 2) + px) * 2;
                                bmpData[offset] = (byte)(color & 0xFF);
                                bmpData[offset + 1] = (byte)((color >> 8) & 0xFF);
                            }
                        }
                    }

                    frames[i].Image = ImageConverter.CreateBitmap(width, height, bmpData, 0, maskColor);
                }

                return frames;
            }
        }

        /// <summary>
        /// 載入 SPR 檔案 (byte[] 版本，不依賴 ImageSharp)
        /// </summary>
        public static RawSprFrame[] LoadRaw(byte[] sprData)
        {
            ushort[] palette = null;
            ushort maskColor = 32768;

            ushort[] reservedColors = new ushort[]
            {
                31744, 768, 31, 32736, 1023, 31775, (ushort)short.MaxValue
            };

            using (var reader = new BinaryReader(new MemoryStream(sprData)))
            {
                bool hasPalette = false;
                byte frameCount = reader.ReadByte();

                if (frameCount == 255)
                {
                    hasPalette = true;
                    int paletteSize = reader.ReadByte();
                    if (paletteSize == 0) paletteSize = 256;

                    palette = new ushort[paletteSize];
                    for (int i = 0; i < paletteSize; i++)
                    {
                        palette[i] = reader.ReadUInt16();
                        int idx = Array.IndexOf(reservedColors, palette[i]);
                        if (idx >= 0) reservedColors[idx] = 0;
                    }

                    foreach (ushort color in reservedColors)
                    {
                        if (color != 0)
                        {
                            maskColor = color;
                            break;
                        }
                    }

                    frameCount = reader.ReadByte();
                }

                var frames = new RawSprFrame[frameCount];
                var blockDefs = new BlockDef[frameCount][];

                for (int i = 0; i < frameCount; i++)
                {
                    frames[i].XOffset = reader.ReadInt16();
                    frames[i].YOffset = reader.ReadInt16();
                    frames[i].Width = reader.ReadInt16() - frames[i].XOffset + 1;
                    frames[i].Height = reader.ReadInt16() - frames[i].YOffset + 1;
                    frames[i].Unknown1 = reader.ReadUInt16();
                    frames[i].Unknown2 = reader.ReadUInt16();

                    int blockCount = reader.ReadUInt16();
                    if (blockCount > 0)
                    {
                        blockDefs[i] = new BlockDef[blockCount];
                        for (int j = 0; j < blockCount; j++)
                        {
                            blockDefs[i][j].A = reader.ReadSByte();
                            blockDefs[i][j].B = reader.ReadSByte();
                            blockDefs[i][j].FrameType = reader.ReadByte();
                            blockDefs[i][j].BlockId = reader.ReadUInt16();
                        }
                        frames[i].Type = blockDefs[i][0].FrameType;
                    }
                }

                int blockTableSize = reader.ReadInt32();
                int[] blockOffsets = new int[blockTableSize];
                for (int i = 0; i < blockTableSize; i++)
                {
                    blockOffsets[i] = reader.ReadInt32();
                }

                reader.ReadInt32();
                int dataStart = (int)reader.BaseStream.Position;

                var blocks = new ushort[blockTableSize][,];
                for (int i = 0; i < blockTableSize; i++)
                {
                    blocks[i] = new ushort[24, 24];

                    for (int y = 0; y < 24; y++)
                    {
                        for (int x = 0; x < 24; x++)
                        {
                            blocks[i][y, x] = 32768;
                        }
                    }

                    reader.BaseStream.Seek(dataStart + blockOffsets[i], SeekOrigin.Begin);

                    byte startX = reader.ReadByte();
                    byte startY = reader.ReadByte();
                    byte unknown = reader.ReadByte();
                    byte lineCount = reader.ReadByte();

                    for (int line = 0; line < lineCount; line++)
                    {
                        int x = startX;
                        byte segmentCount = reader.ReadByte();

                        for (int seg = 0; seg < segmentCount; seg++)
                        {
                            x += reader.ReadByte() / 2;
                            int pixelCount = reader.ReadByte();

                            for (int px = 0; px < pixelCount; px++)
                            {
                                ushort color;
                                if (hasPalette)
                                {
                                    color = palette[reader.ReadByte()];
                                }
                                else
                                {
                                    color = reader.ReadUInt16();
                                    int idx = Array.IndexOf(reservedColors, color);
                                    if (idx >= 0) reservedColors[idx] = 0;
                                }

                                blocks[i][line + startY, x] = color;
                                x++;
                            }
                        }
                    }
                }

                if (maskColor == 32768)
                {
                    foreach (ushort color in reservedColors)
                    {
                        if (color != 0)
                        {
                            maskColor = color;
                            break;
                        }
                    }
                }

                // 組合幀圖像 (使用擴展邊界，不裁切)
                for (int i = 0; i < frameCount; i++)
                {
                    if (blockDefs[i] == null) continue;

                    frames[i].MaskColor = maskColor;

                    // 計算擴展邊界
                    int minX = int.MaxValue, maxX = int.MinValue;
                    int minY = int.MaxValue, maxY = int.MinValue;

                    for (int b = 0; b < blockDefs[i].Length; b++)
                    {
                        int a = blockDefs[i][b].A;
                        int bVal = blockDefs[i][b].B;
                        int blockX = 24 * (bVal + a - a / 2);
                        int blockY = 12 * (bVal - a / 2);

                        minX = Math.Min(minX, blockX);
                        maxX = Math.Max(maxX, blockX + 23);
                        minY = Math.Min(minY, blockY);
                        maxY = Math.Max(maxY, blockY + 23);
                    }

                    int width = maxX - minX + 1;
                    int height = maxY - minY + 1;

                    // 更新 frame 資訊
                    frames[i].XOffset = minX;
                    frames[i].YOffset = minY;
                    frames[i].Width = width;
                    frames[i].Height = height;

                    int stride = (width + width % 2) * 2;
                    byte[] bmpData = new byte[height * stride];

                    for (int j = 0; j < bmpData.Length; j += 2)
                    {
                        bmpData[j] = (byte)(maskColor & 0xFF);
                        bmpData[j + 1] = (byte)((maskColor >> 8) & 0xFF);
                    }

                    for (int b = 0; b < blockDefs[i].Length; b++)
                    {
                        int a = blockDefs[i][b].A;
                        int bVal = blockDefs[i][b].B;

                        int blockX = 24 * (bVal + a - a / 2);
                        int blockY = 12 * (bVal - a / 2);

                        var block = blocks[blockDefs[i][b].BlockId];

                        for (int by = 0; by < 24; by++)
                        {
                            for (int bx = 0; bx < 24; bx++)
                            {
                                ushort color = block[by, bx];
                                if (color == 32768) continue;

                                int px = blockX + bx - minX;
                                int py = blockY + by - minY;

                                int offset = (py * (width + width % 2) + px) * 2;
                                bmpData[offset] = (byte)(color & 0xFF);
                                bmpData[offset + 1] = (byte)((color >> 8) & 0xFF);
                            }
                        }
                    }

                    // 轉換為 RGBA byte[]
                    frames[i].Pixels = ImageConverter.CreateRgbaBytes(width, height, bmpData, 0, maskColor);
                }

                return frames;
            }
        }

        private struct BlockDef
        {
            public int A;
            public int B;
            public int FrameType;
            public int BlockId;
        }
    }

    /// <summary>
    /// SPR 幀結構 (ImageSharp 版本)
    /// </summary>
    public struct SprFrame
    {
        public int XOffset;
        public int YOffset;
        public int Width;
        public int Height;
        public ushort Unknown1;
        public ushort Unknown2;
        public int Type;
        public ushort MaskColor;
        public Image<Rgba32> Image;
    }

    /// <summary>
    /// SPR 幀結構 (byte[] 版本)
    /// </summary>
    public struct RawSprFrame
    {
        public int XOffset;
        public int YOffset;
        public int Width;
        public int Height;
        public ushort Unknown1;
        public ushort Unknown2;
        public int Type;
        public ushort MaskColor;
        /// <summary>
        /// RGBA 格式像素資料 (Width * Height * 4 bytes)
        /// </summary>
        public byte[] Pixels;
    }
}
