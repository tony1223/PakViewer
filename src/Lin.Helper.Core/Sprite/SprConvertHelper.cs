using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// SPR 格式建立輔助工具 (支援指定偏移量)
    /// 供 L1SPX.ToSpr / L1SP2.ToSpr 使用
    /// </summary>
    internal static class SprConvertHelper
    {
        private const int BlockSize = 24;
        private const ushort TransparentColor = 32768; // 0x8000

        public class FrameInput
        {
            public Image<Rgba32> Image;
            public int XOffset;
            public int YOffset;
            public byte Type;
        }

        /// <summary>
        /// 從 frame 資料建立 SPR 檔案 (保留指定的偏移量)
        /// </summary>
        public static byte[] BuildSpr(FrameInput[] frames)
        {
            if (frames == null || frames.Length == 0)
                return new byte[] { 0 };

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // 分析所有 frame，建立區塊
                var frameInfos = new List<SprFrameInfo>();
                var allBlocks = new List<ushort[,]>();
                var blockMap = new Dictionary<string, int>();

                foreach (var frame in frames)
                {
                    var info = AnalyzeFrame(frame, allBlocks, blockMap);
                    frameInfos.Add(info);
                }

                // 寫入 frame count
                writer.Write((byte)frames.Length);

                // 寫入 frame headers
                for (int i = 0; i < frameInfos.Count; i++)
                {
                    var info = frameInfos[i];
                    writer.Write((short)info.XOffset);
                    writer.Write((short)info.YOffset);
                    writer.Write((short)(info.XOffset + info.Width - 1));
                    writer.Write((short)(info.YOffset + info.Height - 1));
                    writer.Write((ushort)0); // unknown1
                    writer.Write((ushort)0); // unknown2
                    writer.Write((ushort)info.BlockDefs.Count);

                    foreach (var bd in info.BlockDefs)
                    {
                        writer.Write((sbyte)bd.A);
                        writer.Write((sbyte)bd.B);
                        writer.Write(frames[i].Type);
                        writer.Write((ushort)bd.BlockId);
                    }
                }

                // 寫入 block table
                writer.Write(allBlocks.Count);

                var blockDataList = new List<byte[]>();
                foreach (var block in allBlocks)
                {
                    blockDataList.Add(EncodeBlock(block));
                }

                int currentOffset = 0;
                foreach (var bd in blockDataList)
                {
                    writer.Write(currentOffset);
                    currentOffset += bd.Length;
                }
                writer.Write(currentOffset); // end marker

                // 寫入 block data
                foreach (var bd in blockDataList)
                {
                    writer.Write(bd);
                }

                return ms.ToArray();
            }
        }

        private static SprFrameInfo AnalyzeFrame(FrameInput frame, List<ushort[,]> allBlocks,
            Dictionary<string, int> blockMap)
        {
            var image = frame.Image;
            int width = image.Width;
            int height = image.Height;

            int blocksX = (width + BlockSize - 1) / BlockSize;
            int blocksY = (height + BlockSize - 1) / BlockSize;

            var info = new SprFrameInfo { BlockDefs = new List<BlockDef>() };

            // 收集非空區塊
            var pendingBlocks = new List<(int gx, int gy, ushort[,] pixels)>();

            int minBX = int.MaxValue, maxBX = int.MinValue;
            int minBY = int.MaxValue, maxBY = int.MinValue;

            for (int gy = 0; gy < blocksY; gy++)
            {
                for (int gx = 0; gx < blocksX; gx++)
                {
                    var pixels = ExtractBlock(image, gx * BlockSize, gy * BlockSize);
                    if (IsEmpty(pixels)) continue;

                    pendingBlocks.Add((gx, gy, pixels));

                    var (a, b) = GridToIsometric(gx, gy);
                    var (bx, by) = IsometricToPixel(a, b);
                    minBX = Math.Min(minBX, bx);
                    maxBX = Math.Max(maxBX, bx + BlockSize - 1);
                    minBY = Math.Min(minBY, by);
                    maxBY = Math.Max(maxBY, by + BlockSize - 1);
                }
            }

            if (pendingBlocks.Count == 0)
            {
                // 使用指定的 offset
                info.XOffset = frame.XOffset;
                info.YOffset = frame.YOffset;
                info.Width = Math.Max(1, width);
                info.Height = Math.Max(1, height);
                return info;
            }

            // 使用指定的 offset (來自原始 SPX/SP2 的縮放值)
            info.XOffset = frame.XOffset;
            info.YOffset = frame.YOffset;
            info.Width = Math.Max(1, maxBX - minBX + 1);
            info.Height = Math.Max(1, maxBY - minBY + 1);

            foreach (var (gx, gy, pixels) in pendingBlocks)
            {
                string hash = HashBlock(pixels);
                int blockId;
                if (blockMap.TryGetValue(hash, out int existing))
                    blockId = existing;
                else
                {
                    blockId = allBlocks.Count;
                    allBlocks.Add(pixels);
                    blockMap[hash] = blockId;
                }

                var (a, b) = GridToIsometric(gx, gy);
                info.BlockDefs.Add(new BlockDef { A = a, B = b, BlockId = blockId });
            }

            return info;
        }

        private static (int a, int b) GridToIsometric(int gx, int gy)
        {
            int a = gx - 2 * gy;
            int b;
            if (a >= 0)
                b = 2 * gy + a / 2;
            else
                b = 2 * gy + (a - 1) / 2;
            return (a, b);
        }

        private static (int blockX, int blockY) IsometricToPixel(int a, int b)
        {
            int aAdj = a;
            if (aAdj < 0) aAdj--;
            int blockX = BlockSize * (b + a - aAdj / 2);
            int blockY = (BlockSize / 2) * (b - aAdj / 2);
            return (blockX, blockY);
        }

        private static ushort[,] ExtractBlock(Image<Rgba32> image, int startX, int startY)
        {
            var pixels = new ushort[BlockSize, BlockSize];
            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    int ix = startX + x;
                    int iy = startY + y;
                    if (ix < image.Width && iy < image.Height)
                    {
                        var px = image[ix, iy];
                        if (px.A < 128)
                            pixels[y, x] = TransparentColor;
                        else
                            pixels[y, x] = Rgba32ToRgb555(px);
                    }
                    else
                    {
                        pixels[y, x] = TransparentColor;
                    }
                }
            }
            return pixels;
        }

        private static bool IsEmpty(ushort[,] pixels)
        {
            for (int y = 0; y < BlockSize; y++)
                for (int x = 0; x < BlockSize; x++)
                    if (pixels[y, x] != TransparentColor)
                        return false;
            return true;
        }

        private static string HashBlock(ushort[,] pixels)
        {
            unchecked
            {
                int hash = 17;
                for (int y = 0; y < BlockSize; y++)
                    for (int x = 0; x < BlockSize; x++)
                        hash = hash * 31 + pixels[y, x];
                return hash.ToString();
            }
        }

        private static byte[] EncodeBlock(ushort[,] pixels)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                int minX = BlockSize, maxX = -1;
                int minY = BlockSize, maxY = -1;

                for (int y = 0; y < BlockSize; y++)
                    for (int x = 0; x < BlockSize; x++)
                        if (pixels[y, x] != TransparentColor)
                        {
                            minX = Math.Min(minX, x);
                            maxX = Math.Max(maxX, x);
                            minY = Math.Min(minY, y);
                            maxY = Math.Max(maxY, y);
                        }

                if (maxX < 0)
                {
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    return ms.ToArray();
                }

                int lineCount = maxY - minY + 1;
                writer.Write((byte)minX);
                writer.Write((byte)minY);
                writer.Write((byte)0);
                writer.Write((byte)lineCount);

                for (int line = 0; line < lineCount; line++)
                {
                    int y = minY + line;
                    var segments = new List<(int skip, List<ushort> colors)>();
                    int x = minX;

                    while (x < BlockSize)
                    {
                        int skipStart = x;
                        while (x < BlockSize && pixels[y, x] == TransparentColor) x++;
                        if (x >= BlockSize) break;

                        int skip = (x - skipStart) * 2;
                        var colors = new List<ushort>();
                        while (x < BlockSize && pixels[y, x] != TransparentColor)
                        {
                            colors.Add(pixels[y, x]);
                            x++;
                        }
                        if (colors.Count > 0)
                            segments.Add((skip, colors));
                    }

                    writer.Write((byte)segments.Count);
                    foreach (var seg in segments)
                    {
                        writer.Write((byte)seg.skip);
                        writer.Write((byte)seg.colors.Count);
                        foreach (var c in seg.colors)
                            writer.Write(c);
                    }
                }

                return ms.ToArray();
            }
        }

        private static ushort Rgba32ToRgb555(Rgba32 color)
        {
            int r = color.R >> 3;
            int g = color.G >> 3;
            int b = color.B >> 3;
            return (ushort)((r << 10) | (g << 5) | b);
        }

        private class SprFrameInfo
        {
            public int XOffset;
            public int YOffset;
            public int Width;
            public int Height;
            public List<BlockDef> BlockDefs;
        }

        private struct BlockDef
        {
            public int A;
            public int B;
            public int BlockId;
        }
    }
}
