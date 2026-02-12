using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Lin.Helper.Core.Image;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// Lineage M SP2 (新版 Sprite) 檔案處理
    /// SP2 使用 Brotli 壓縮、RGB565 調色盤、多方向結構
    /// </summary>
    public static class L1SP2
    {
        private const int BlockSize = 24;
        private const byte SP2_MARKER = 0xFE; // 254

        /// <summary>
        /// 讀取 SP2 檔案 (ImageSharp 版本)
        /// 回傳 Dictionary: 方向索引 → SprFrame[]
        /// </summary>
        public static Dictionary<int, SprFrame[]> Read(byte[] data)
        {
            data = EnsureDecompressed(data);
            var parsed = ParseSp2(data);
            var result = new Dictionary<int, SprFrame[]>();

            foreach (var kv in parsed.DirectionFrames)
            {
                int dir = kv.Key;
                var pFrames = kv.Value;
                var frames = new SprFrame[pFrames.Length];

                for (int i = 0; i < pFrames.Length; i++)
                {
                    var pf = pFrames[i];
                    frames[i].XOffset = pf.XOffset;
                    frames[i].YOffset = pf.YOffset;
                    frames[i].Width = pf.Width;
                    frames[i].Height = pf.Height;
                    frames[i].Type = pf.Type;
                    frames[i].MaskColor = 0;

                    if (pf.Pixels != null)
                    {
                        frames[i].Image = new Image<Rgba32>(pf.Width, pf.Height);
                        for (int y = 0; y < pf.Height; y++)
                        {
                            for (int x = 0; x < pf.Width; x++)
                            {
                                int idx = (y * pf.Width + x) * 4;
                                frames[i].Image[x, y] = new Rgba32(
                                    pf.Pixels[idx], pf.Pixels[idx + 1],
                                    pf.Pixels[idx + 2], pf.Pixels[idx + 3]);
                            }
                        }
                    }
                }

                result[dir] = frames;
            }

            return result;
        }

        /// <summary>
        /// 讀取 SP2 檔案 (byte[] 版本，不依賴 ImageSharp)
        /// 回傳 Dictionary: 方向索引 → RawSprFrame[]
        /// </summary>
        public static Dictionary<int, RawSprFrame[]> ReadRaw(byte[] data)
        {
            data = EnsureDecompressed(data);
            var parsed = ParseSp2(data);
            var result = new Dictionary<int, RawSprFrame[]>();

            foreach (var kv in parsed.DirectionFrames)
            {
                int dir = kv.Key;
                var pFrames = kv.Value;
                var frames = new RawSprFrame[pFrames.Length];

                for (int i = 0; i < pFrames.Length; i++)
                {
                    var pf = pFrames[i];
                    frames[i].XOffset = pf.XOffset;
                    frames[i].YOffset = pf.YOffset;
                    frames[i].Width = pf.Width;
                    frames[i].Height = pf.Height;
                    frames[i].Type = pf.Type;
                    frames[i].MaskColor = 0;
                    frames[i].Pixels = pf.Pixels;
                }

                result[dir] = frames;
            }

            return result;
        }

        /// <summary>
        /// 將 SP2 轉換為 SPR 格式 (含比例縮放)
        /// 回傳 Dictionary: 方向索引 → SPR byte[]
        /// </summary>
        /// <param name="sp2Data">SP2 檔案資料</param>
        /// <param name="scale">縮放比例 (預設 1.0，SP2 與 SPR 同為 24x24 區塊)</param>
        public static Dictionary<int, byte[]> ToSpr(byte[] sp2Data, double scale = 1.0)
        {
            var dirFrames = Read(sp2Data);
            var result = new Dictionary<int, byte[]>();

            foreach (var kv in dirFrames)
            {
                int dir = kv.Key;
                var frames = kv.Value;
                var inputs = new SprConvertHelper.FrameInput[frames.Length];

                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i].Image != null)
                    {
                        Image<Rgba32> img = frames[i].Image;
                        if (Math.Abs(scale - 1.0) > 0.001)
                        {
                            int newW = Math.Max(1, (int)(img.Width * scale));
                            int newH = Math.Max(1, (int)(img.Height * scale));
                            var scaled = img.Clone(ctx => ctx.Resize(newW, newH));
                            img.Dispose();
                            img = scaled;
                        }

                        inputs[i] = new SprConvertHelper.FrameInput
                        {
                            Image = img,
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

                result[dir] = SprConvertHelper.BuildSpr(inputs);

                foreach (var input in inputs)
                    input.Image?.Dispose();
            }

            return result;
        }

        #region Brotli Decompression

        private static byte[] EnsureDecompressed(byte[] data)
        {
            if (data == null || data.Length == 0) return data;
            if (data[0] == SP2_MARKER) return data; // 已解壓

            try
            {
                using (var input = new MemoryStream(data))
                using (var brotli = new BrotliStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    brotli.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch
            {
                return data; // 解壓失敗，嘗試直接解析
            }
        }

        #endregion

        #region Internal SP2 Parser

        private static Sp2ParseResult ParseSp2(byte[] data)
        {
            var result = new Sp2ParseResult
            {
                DirectionFrames = new Dictionary<int, Sp2RenderedFrame[]>()
            };

            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                byte marker = reader.ReadByte();
                if (marker != SP2_MARKER)
                    return result;

                // 調色盤
                int palSize = reader.ReadByte();
                if (palSize == 0) palSize = 256;
                else if (palSize == 1) palSize = 3;

                ushort[] palette = new ushort[palSize]; // RGB565
                for (int i = 0; i < palSize; i++)
                    palette[i] = reader.ReadUInt16();

                // 方向數
                int dirCount = reader.ReadByte();
                if (dirCount == 15 || dirCount == 85) dirCount = 4;
                else if (dirCount == 255) dirCount = 8;

                // 各方向的 frame 與 block 定義
                var dirFrameDefs = new Dictionary<int, Sp2FrameDef[]>();
                var dirBlocks = new Dictionary<int, Sp2Block[]>();

                for (int d = 0; d < dirCount; d++)
                {
                    // Frame definitions
                    int frameCount = reader.ReadByte();
                    var frameDefs = new Sp2FrameDef[frameCount];

                    for (int f = 0; f < frameCount; f++)
                    {
                        frameDefs[f].XOffset = reader.ReadInt16();
                        frameDefs[f].YOffset = reader.ReadInt16();
                        int right = reader.ReadInt16();
                        int bottom = reader.ReadInt16();
                        frameDefs[f].Width = right - frameDefs[f].XOffset + 1;
                        frameDefs[f].Height = bottom - frameDefs[f].YOffset + 1;

                        if (frameDefs[f].Width < 0 || frameDefs[f].Height < 0)
                        {
                            frameDefs[f].Width = 1;
                            frameDefs[f].Height = 1;
                        }

                        int blockCount = reader.ReadUInt16();
                        frameDefs[f].Blocks = new Sp2BlockRef[blockCount];
                        for (int b = 0; b < blockCount; b++)
                            frameDefs[f].Blocks[b] = new Sp2BlockRef();

                        // 讀取所有 a 值
                        int maxA = 0, maxB = 0;
                        for (int b = 0; b < blockCount; b++)
                        {
                            frameDefs[f].Blocks[b].A = reader.ReadSByte();
                            if (frameDefs[f].Blocks[b].A > maxA)
                                maxA = frameDefs[f].Blocks[b].A;
                        }
                        // 讀取所有 b 值
                        for (int b = 0; b < blockCount; b++)
                        {
                            frameDefs[f].Blocks[b].B = reader.ReadSByte();
                            if (frameDefs[f].Blocks[b].B > maxB)
                                maxB = frameDefs[f].Blocks[b].B;
                        }
                        // 讀取所有 frameType 值
                        for (int b = 0; b < blockCount; b++)
                        {
                            frameDefs[f].Blocks[b].FrameType = reader.ReadByte();
                            if (frameDefs[f].Blocks[b].A == 0)
                                frameDefs[f].BaseType = frameDefs[f].Blocks[b].FrameType;
                        }
                        // 讀取所有 blockID (delta encoded)
                        int prevId = 0;
                        for (int b = 0; b < blockCount; b++)
                        {
                            int delta = reader.ReadInt16();
                            if (b == 0)
                                prevId = delta;
                            else
                                prevId += delta + 1;
                            frameDefs[f].Blocks[b].BlockID = prevId;
                        }

                        frameDefs[f].MaxA = maxA;
                        frameDefs[f].MaxB = maxB;
                    }

                    dirFrameDefs[d] = frameDefs;

                    // Block data definitions
                    int blockDataCount = reader.ReadInt32();
                    var blocks = new Sp2Block[blockDataCount];

                    for (int b = 0; b < blockDataCount; b++)
                    {
                        blocks[b].XOffset = reader.ReadByte();
                        blocks[b].YOffset = reader.ReadByte();
                        blocks[b].YLen = reader.ReadByte();
                        blocks[b].Data = reader.ReadBytes(blocks[b].YLen * 2);
                    }

                    dirBlocks[d] = blocks;
                }

                // 解碼區塊 (讀取 pixel data from stream)
                for (int d = 0; d < dirCount; d++)
                {
                    if (!dirBlocks.ContainsKey(d)) continue;
                    var blocks = dirBlocks[d];

                    for (int bi = 0; bi < blocks.Length; bi++)
                    {
                        blocks[bi].DecodedData = DecodeBlock(ref blocks[bi], reader);
                    }

                    dirBlocks[d] = blocks;
                }

                // 渲染幀
                for (int d = 0; d < dirCount; d++)
                {
                    if (!dirFrameDefs.ContainsKey(d) || !dirBlocks.ContainsKey(d))
                        continue;

                    var frameDefs = dirFrameDefs[d];
                    var blocks = dirBlocks[d];
                    var rendered = new Sp2RenderedFrame[frameDefs.Length];

                    for (int f = 0; f < frameDefs.Length; f++)
                    {
                        rendered[f] = RenderFrame(frameDefs[f], blocks, palette);
                    }

                    result.DirectionFrames[d] = rendered;
                }
            }

            return result;
        }

        /// <summary>
        /// 解碼 SP2 區塊的指令資料 → 標準區塊格式 (含 palette index)
        /// </summary>
        private static Sp2DecodedBlock DecodeBlock(ref Sp2Block block, BinaryReader reader)
        {
            var decoded = new Sp2DecodedBlock();
            var outputBytes = new List<byte>();
            int decodedWidth = 0;
            int decodedHeight = 0;
            int blankRows = 0;

            int idx = 0;
            int processed = 0;

            while (processed < block.YLen && idx < block.YLen)
            {
                // 讀取 16-bit 值 (little-endian)
                byte lo = block.Data[idx * 2];
                int val = block.Data[idx * 2 + 1] * 256 + lo;

                byte groupSize = 1;
                int blankCount = 0;

                if (val > 2048)
                    blankCount = val / 1024 - 1;

                if (val > 1024)
                {
                    val %= 1024;
                }
                else
                {
                    // 計算連續的 <= 1024 項目 (group)
                    if (idx != block.YLen - 1)
                        groupSize++;

                    int lookAhead = 0;
                    while (lookAhead < 10
                        && (idx + lookAhead + 1) * 2 <= block.Data.Length - 2
                        && idx + lookAhead != block.YLen - 1)
                    {
                        if (idx + lookAhead + 1 == block.YLen - 1)
                            break;

                        byte nextLo = block.Data[(idx + lookAhead + 1) * 2];
                        int nextVal = block.Data[(idx + lookAhead + 1) * 2 + 1] * 256 + nextLo;
                        if (nextVal > 1024) break;

                        groupSize++;
                        lookAhead++;
                    }
                }

                int remaining = groupSize - 1;
                bool isFirst = true;
                int xTrack = 0;

                for (int g = 0; g < groupSize; g++)
                {
                    int xPos = val % 32;
                    int pixelCount = val / 32;

                    if (isFirst)
                        outputBytes.Add(groupSize); // segment count

                    outputBytes.Add((byte)(xPos * 2)); // skip (bytes)
                    outputBytes.Add((byte)pixelCount);
                    xTrack += xPos;

                    for (int p = 0; p < pixelCount; p++)
                    {
                        outputBytes.Add(reader.ReadByte()); // palette index
                        xTrack++;
                    }

                    decodedWidth = Math.Max(decodedWidth, xTrack);

                    if (remaining > 0)
                    {
                        remaining--;
                        idx++;
                        lo = block.Data[idx * 2];
                        val = (block.Data[idx * 2 + 1] * 256 + lo) % 1024;
                        isFirst = false;
                    }
                    else
                    {
                        decodedHeight++;
                    }
                }

                // 空白行
                if (blankCount > 0)
                {
                    for (int r = 0; r < blankCount; r++)
                        outputBytes.Add(0); // empty scan line (0 segments)
                }
                blankRows += blankCount;

                idx++;
                processed++;
            }

            decoded.Width = decodedWidth;
            decoded.Height = decodedHeight + blankRows;
            decoded.Data = outputBytes.ToArray();
            return decoded;
        }

        /// <summary>
        /// 渲染一個幀 (組合區塊 → RGBA 像素)
        /// </summary>
        private static Sp2RenderedFrame RenderFrame(Sp2FrameDef frameDef, Sp2Block[] blocks,
            ushort[] palette)
        {
            var rendered = new Sp2RenderedFrame
            {
                XOffset = frameDef.XOffset,
                YOffset = frameDef.YOffset,
                Type = frameDef.BaseType
            };

            if (frameDef.Blocks == null || frameDef.Blocks.Length == 0 ||
                frameDef.Width <= 0 || frameDef.Height <= 0)
            {
                rendered.Width = 1;
                rendered.Height = 1;
                return rendered;
            }

            // 計算 bounding box
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int b = 0; b < frameDef.Blocks.Length; b++)
            {
                var br = frameDef.Blocks[b];
                int aAdj = br.A;
                if (aAdj < 0) aAdj--;
                int blockX = BlockSize * (br.B + br.A - aAdj / 2);
                int blockY = (BlockSize / 2) * (br.B - aAdj / 2);

                if (br.BlockID >= blocks.Length) continue;
                var block = blocks[br.BlockID];
                int bw = Math.Max(1, block.DecodedData.Width);
                int bh = Math.Max(1, block.DecodedData.Height);

                minX = Math.Min(minX, blockX + block.XOffset);
                maxX = Math.Max(maxX, blockX + block.XOffset + bw);
                minY = Math.Min(minY, blockY + block.YOffset);
                maxY = Math.Max(maxY, blockY + block.YOffset + bh);
            }

            if (minX >= maxX || minY >= maxY)
            {
                rendered.Width = 1;
                rendered.Height = 1;
                return rendered;
            }

            int width = maxX - minX;
            int height = maxY - minY;
            rendered.Width = width;
            rendered.Height = height;
            rendered.XOffset = minX;
            rendered.YOffset = minY;

            byte[] pixels = new byte[width * height * 4]; // RGBA

            // 渲染每個區塊
            for (int bi = 0; bi < frameDef.Blocks.Length; bi++)
            {
                var br = frameDef.Blocks[bi];
                if (br.BlockID >= blocks.Length) continue;

                int aAdj = br.A;
                if (aAdj < 0) aAdj--;
                int blockBaseX = BlockSize * (br.B + br.A - aAdj / 2);
                int blockBaseY = (BlockSize / 2) * (br.B - aAdj / 2);

                var block = blocks[br.BlockID];
                var dec = block.DecodedData;
                if (dec.Data == null || dec.Data.Length == 0) continue;

                int dataPos = 0;
                int drawX = blockBaseX + block.XOffset - minX;
                int drawY = blockBaseY + block.YOffset - minY;

                for (int line = 0; line < dec.Height && dataPos < dec.Data.Length; line++)
                {
                    byte segCount = dec.Data[dataPos++];
                    int x = 0;

                    for (int seg = 0; seg < segCount && dataPos + 1 < dec.Data.Length; seg++)
                    {
                        x += dec.Data[dataPos++] / 2; // skip (bytes → pixels)
                        int pxCount = dec.Data[dataPos++];

                        for (int p = 0; p < pxCount && dataPos < dec.Data.Length; p++)
                        {
                            int palIdx = dec.Data[dataPos++];
                            if (palIdx < palette.Length)
                            {
                                var rgba = ImageConverter.Rgb565ToRgba32(palette[palIdx]);
                                int px = drawX + x;
                                int py = drawY + line;

                                if (px >= 0 && px < width && py >= 0 && py < height)
                                {
                                    int offset = (py * width + px) * 4;
                                    pixels[offset] = rgba.R;
                                    pixels[offset + 1] = rgba.G;
                                    pixels[offset + 2] = rgba.B;
                                    pixels[offset + 3] = rgba.A;
                                }
                            }
                            x++;
                        }
                    }
                }
            }

            rendered.Pixels = pixels;
            return rendered;
        }

        #endregion

        #region Internal Structures

        private struct Sp2ParseResult
        {
            public Dictionary<int, Sp2RenderedFrame[]> DirectionFrames;
        }

        private struct Sp2FrameDef
        {
            public int XOffset;
            public int YOffset;
            public int Width;
            public int Height;
            public Sp2BlockRef[] Blocks;
            public int MaxA;
            public int MaxB;
            public int BaseType;
        }

        private class Sp2BlockRef
        {
            public int A;
            public int B;
            public int FrameType;
            public int BlockID;
        }

        private struct Sp2Block
        {
            public int XOffset;
            public int YOffset;
            public int YLen;
            public byte[] Data;
            public Sp2DecodedBlock DecodedData;
        }

        private struct Sp2DecodedBlock
        {
            public int Width;
            public int Height;
            public byte[] Data; // 解碼後的標準區塊格式 (segCount, skip, pxCount, paletteIdx...)
        }

        private struct Sp2RenderedFrame
        {
            public int XOffset;
            public int YOffset;
            public int Width;
            public int Height;
            public int Type;
            public byte[] Pixels; // RGBA (Width * Height * 4)
        }

        #endregion
    }
}
