using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Lin.Helper.Core.Image;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// Lineage 1 SPX (Remastered Sprite) 檔案處理
    /// SPX 與 SPR 類似，但使用 48x48 區塊、Int32 BlockID、Int32 區塊數
    /// </summary>
    public static class L1SPX
    {
        private const int BlockSize = 48;
        private const ushort TransparentRgb555 = 32768; // 0x8000

        /// <summary>
        /// 讀取 SPX 檔案 (ImageSharp 版本)
        /// </summary>
        public static SprFrame[] Read(byte[] data)
        {
            var parsed = ParseSpx(data);

            var frames = new SprFrame[parsed.Frames.Length];
            for (int i = 0; i < parsed.Frames.Length; i++)
            {
                var pf = parsed.Frames[i];
                frames[i].XOffset = pf.XOffset;
                frames[i].YOffset = pf.YOffset;
                frames[i].Width = pf.Width;
                frames[i].Height = pf.Height;
                frames[i].Unknown1 = pf.Unknown1;
                frames[i].Unknown2 = pf.Unknown2;
                frames[i].Type = pf.Type;
                frames[i].MaskColor = parsed.MaskColor;

                if (pf.BmpData != null)
                {
                    frames[i].Image = ImageConverter.CreateBitmap(
                        pf.Width, pf.Height, pf.BmpData, 0, parsed.MaskColor);
                }
            }

            return frames;
        }

        /// <summary>
        /// 讀取 SPX 檔案 (byte[] 版本，不依賴 ImageSharp)
        /// </summary>
        public static RawSprFrame[] ReadRaw(byte[] data)
        {
            var parsed = ParseSpx(data);

            var frames = new RawSprFrame[parsed.Frames.Length];
            for (int i = 0; i < parsed.Frames.Length; i++)
            {
                var pf = parsed.Frames[i];
                frames[i].XOffset = pf.XOffset;
                frames[i].YOffset = pf.YOffset;
                frames[i].Width = pf.Width;
                frames[i].Height = pf.Height;
                frames[i].Unknown1 = pf.Unknown1;
                frames[i].Unknown2 = pf.Unknown2;
                frames[i].Type = pf.Type;
                frames[i].MaskColor = parsed.MaskColor;

                if (pf.BmpData != null)
                {
                    frames[i].Pixels = ImageConverter.CreateRgbaBytes(
                        pf.Width, pf.Height, pf.BmpData, 0, parsed.MaskColor);
                }
            }

            return frames;
        }

        /// <summary>
        /// 將 SPX 轉換為 SPR 格式 (含比例縮放)
        /// </summary>
        /// <param name="spxData">SPX 檔案資料</param>
        /// <param name="scale">縮放比例 (預設 0.5，即 48→24)</param>
        /// <returns>SPR 格式的 byte[]</returns>
        public static byte[] ToSpr(byte[] spxData, double scale = 0.5)
        {
            var frames = Read(spxData);
            var inputs = new SprConvertHelper.FrameInput[frames.Length];

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].Image != null)
                {
                    int newW = Math.Max(1, (int)(frames[i].Image.Width * scale));
                    int newH = Math.Max(1, (int)(frames[i].Image.Height * scale));
                    var scaled = frames[i].Image.Clone(ctx => ctx.Resize(newW, newH));
                    frames[i].Image.Dispose();

                    inputs[i] = new SprConvertHelper.FrameInput
                    {
                        Image = scaled,
                        XOffset = (int)(frames[i].XOffset * scale),
                        YOffset = (int)(frames[i].YOffset * scale),
                        Type = (byte)frames[i].Type
                    };
                }
                else
                {
                    inputs[i] = new SprConvertHelper.FrameInput
                    {
                        Image = new Image<Rgba32>(1, 1),
                        XOffset = 0,
                        YOffset = 0,
                        Type = 0
                    };
                }
            }

            var result = SprConvertHelper.BuildSpr(inputs);

            // Dispose scaled images
            foreach (var input in inputs)
                input.Image?.Dispose();

            return result;
        }

        #region Internal SPX Parser

        private static SpxParseResult ParseSpx(byte[] data)
        {
            ushort[] palette = null;
            ushort maskColor = TransparentRgb555;
            bool hasPalette = false;

            ushort[] reservedColors = new ushort[]
            {
                31744, 768, 31, 32736, 1023, 31775, (ushort)short.MaxValue
            };

            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                byte frameCount = reader.ReadByte();

                // 調色盤
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

                    foreach (ushort c in reservedColors)
                    {
                        if (c != 0) { maskColor = c; break; }
                    }

                    frameCount = reader.ReadByte();
                }

                var blockDefs = new SpxBlockDef[frameCount][];
                var parsedFrames = new SpxParsedFrame[frameCount];

                // 讀取幀頭
                for (int i = 0; i < frameCount; i++)
                {
                    parsedFrames[i].XOffset = reader.ReadInt16();
                    parsedFrames[i].YOffset = reader.ReadInt16();
                    int right = reader.ReadInt16();
                    int bottom = reader.ReadInt16();
                    parsedFrames[i].Width = right - parsedFrames[i].XOffset + 1;
                    parsedFrames[i].Height = bottom - parsedFrames[i].YOffset + 1;
                    parsedFrames[i].Unknown1 = reader.ReadUInt16();
                    parsedFrames[i].Unknown2 = reader.ReadUInt16(); // max_a(byte) + max_b(byte)

                    int blockCount = reader.ReadInt32(); // SPX: Int32
                    if (blockCount > 0)
                    {
                        blockDefs[i] = new SpxBlockDef[blockCount];
                        for (int j = 0; j < blockCount; j++)
                        {
                            blockDefs[i][j].A = reader.ReadSByte();
                            blockDefs[i][j].B = reader.ReadSByte();
                            blockDefs[i][j].FrameType = reader.ReadByte();
                            blockDefs[i][j].BlockId = reader.ReadInt32(); // SPX: Int32
                        }
                        parsedFrames[i].Type = blockDefs[i][0].FrameType;
                    }
                }

                // 區塊偏移表
                int blockTableSize = reader.ReadInt32();
                int[] blockOffsets = new int[blockTableSize + 1];
                for (int i = 0; i <= blockTableSize; i++)
                {
                    blockOffsets[i] = reader.ReadInt32();
                }

                int dataStart = (int)reader.BaseStream.Position;

                // 讀取區塊像素
                var blocks = new ushort[blockTableSize][,];
                for (int i = 0; i < blockTableSize; i++)
                {
                    blocks[i] = new ushort[BlockSize, BlockSize];
                    for (int y = 0; y < BlockSize; y++)
                        for (int x = 0; x < BlockSize; x++)
                            blocks[i][y, x] = TransparentRgb555;

                    reader.BaseStream.Seek(dataStart + blockOffsets[i], SeekOrigin.Begin);

                    byte startX = reader.ReadByte();
                    byte startY = reader.ReadByte();
                    byte blockWidth = reader.ReadByte();
                    byte lineCount = reader.ReadByte();

                    for (int line = 0; line < lineCount; line++)
                    {
                        int x = startX;
                        byte segCount = reader.ReadByte();
                        for (int seg = 0; seg < segCount; seg++)
                        {
                            x += reader.ReadByte() / 2;
                            int pxCount = reader.ReadByte();
                            for (int px = 0; px < pxCount; px++)
                            {
                                ushort color;
                                if (hasPalette)
                                    color = palette[reader.ReadByte()];
                                else
                                {
                                    color = reader.ReadUInt16();
                                    int idx = Array.IndexOf(reservedColors, color);
                                    if (idx >= 0) reservedColors[idx] = 0;
                                }

                                if (line + startY < BlockSize && x < BlockSize)
                                    blocks[i][line + startY, x] = color;
                                x++;
                            }
                        }
                    }
                }

                // 最終遮罩色
                if (maskColor == TransparentRgb555)
                {
                    foreach (ushort c in reservedColors)
                    {
                        if (c != 0) { maskColor = c; break; }
                    }
                }

                // 組合幀
                for (int i = 0; i < frameCount; i++)
                {
                    if (blockDefs[i] == null) continue;

                    int minX = int.MaxValue, maxX = int.MinValue;
                    int minY = int.MaxValue, maxY = int.MinValue;

                    for (int b = 0; b < blockDefs[i].Length; b++)
                    {
                        int a = blockDefs[i][b].A;
                        int bVal = blockDefs[i][b].B;
                        int aAdj = a;
                        if (aAdj < 0) aAdj--;
                        int blockX = BlockSize * (bVal + a - aAdj / 2);
                        int blockY = (BlockSize / 2) * (bVal - aAdj / 2);

                        minX = Math.Min(minX, blockX);
                        maxX = Math.Max(maxX, blockX + BlockSize - 1);
                        minY = Math.Min(minY, blockY);
                        maxY = Math.Max(maxY, blockY + BlockSize - 1);
                    }

                    int width = maxX - minX + 1;
                    int height = maxY - minY + 1;

                    parsedFrames[i].XOffset = minX;
                    parsedFrames[i].YOffset = minY;
                    parsedFrames[i].Width = width;
                    parsedFrames[i].Height = height;

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
                        int aAdj = a;
                        if (aAdj < 0) aAdj--;
                        int blockX = BlockSize * (bVal + a - aAdj / 2);
                        int blockY = (BlockSize / 2) * (bVal - aAdj / 2);

                        var block = blocks[blockDefs[i][b].BlockId];

                        for (int by = 0; by < BlockSize; by++)
                        {
                            for (int bx = 0; bx < BlockSize; bx++)
                            {
                                ushort color = block[by, bx];
                                if (color == TransparentRgb555) continue;

                                int px = blockX + bx - minX;
                                int py = blockY + by - minY;
                                if (px < 0 || px >= width || py < 0 || py >= height) continue;

                                int offset = (py * (width + width % 2) + px) * 2;
                                bmpData[offset] = (byte)(color & 0xFF);
                                bmpData[offset + 1] = (byte)((color >> 8) & 0xFF);
                            }
                        }
                    }

                    parsedFrames[i].BmpData = bmpData;
                }

                return new SpxParseResult
                {
                    Frames = parsedFrames,
                    MaskColor = maskColor
                };
            }
        }

        private struct SpxBlockDef
        {
            public int A;
            public int B;
            public int FrameType;
            public int BlockId;
        }

        private struct SpxParsedFrame
        {
            public int XOffset;
            public int YOffset;
            public int Width;
            public int Height;
            public ushort Unknown1;
            public ushort Unknown2;
            public int Type;
            public byte[] BmpData; // RGB555 pixel data
        }

        private struct SpxParseResult
        {
            public SpxParsedFrame[] Frames;
            public ushort MaskColor;
        }

        #endregion
    }
}
