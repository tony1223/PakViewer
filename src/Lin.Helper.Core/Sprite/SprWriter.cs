using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// Lineage 1 SPR (Sprite) 檔案寫入器
    /// </summary>
    public static class SprWriter
    {
        private const int BlockSize = 24;
        private const ushort TransparentColor = 32768; // 0x8000

        /// <summary>
        /// 從多個 PNG 圖片建立 SPR 檔案
        /// </summary>
        /// <param name="images">Image 陣列 (每個 image 為一個 frame)</param>
        /// <param name="frameType">frame type (預設 0)</param>
        /// <returns>SPR 檔案的 byte[]</returns>
        public static byte[] Create(Image<Rgba32>[] images, byte frameType = 0)
        {
            if (images == null || images.Length == 0)
                throw new ArgumentException("At least one image is required");

            if (images.Length > 254)
                throw new ArgumentException("Maximum 254 frames supported");

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 分析所有圖片，建立區塊
            var frameInfos = new List<FrameInfo>();
            var allBlocks = new List<BlockData>();
            var blockMap = new Dictionary<string, int>(); // 用於去重複

            foreach (var image in images)
            {
                var frameInfo = AnalyzeFrame(image, allBlocks, blockMap, frameType);
                frameInfos.Add(frameInfo);
            }

            // 寫入 frame count
            writer.Write((byte)images.Length);

            // 寫入 frame headers
            foreach (var info in frameInfos)
            {
                writer.Write((short)info.XOffset);
                writer.Write((short)info.YOffset);
                writer.Write((short)(info.XOffset + info.Width - 1));  // right edge
                writer.Write((short)(info.YOffset + info.Height - 1)); // bottom edge
                writer.Write((ushort)0); // unknown1
                writer.Write((ushort)0); // unknown2
                writer.Write((ushort)info.BlockDefs.Count);

                foreach (var blockDef in info.BlockDefs)
                {
                    writer.Write((sbyte)blockDef.A);
                    writer.Write((sbyte)blockDef.B);
                    writer.Write(frameType);
                    writer.Write((ushort)blockDef.BlockId);
                }
            }

            // 寫入 block table size
            writer.Write(allBlocks.Count);

            // 計算 block 資料並建立偏移表
            var blockDataList = new List<byte[]>();
            foreach (var block in allBlocks)
            {
                blockDataList.Add(EncodeBlock(block.Pixels));
            }

            // 寫入 block offsets
            int currentOffset = 0;
            foreach (var blockData in blockDataList)
            {
                writer.Write(currentOffset);
                currentOffset += blockData.Length;
            }

            // 寫入結束偏移 (total data size)
            writer.Write(currentOffset);

            // 寫入 block 資料
            foreach (var blockData in blockDataList)
            {
                writer.Write(blockData);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// 從 PNG 檔案路徑陣列建立 SPR 檔案
        /// </summary>
        public static byte[] CreateFromFiles(string[] pngPaths, byte frameType = 0)
        {
            var images = new Image<Rgba32>[pngPaths.Length];
            try
            {
                for (int i = 0; i < pngPaths.Length; i++)
                {
                    images[i] = SixLabors.ImageSharp.Image.Load<Rgba32>(pngPaths[i]);
                }
                return Create(images, frameType);
            }
            finally
            {
                foreach (var img in images)
                {
                    img?.Dispose();
                }
            }
        }

        private static FrameInfo AnalyzeFrame(Image<Rgba32> image, List<BlockData> allBlocks,
            Dictionary<string, int> blockMap, byte frameType)
        {
            int width = image.Width;
            int height = image.Height;

            // 計算需要的區塊數量
            int blocksX = (width + BlockSize - 1) / BlockSize;
            int blocksY = (height + BlockSize - 1) / BlockSize;

            var frameInfo = new FrameInfo
            {
                BlockDefs = new List<BlockDef>()
            };

            // 計算所有區塊的位置，同時追蹤邊界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            var pendingBlocks = new List<(int gx, int gy, ushort[,] pixels)>();

            // 為每個區塊位置建立區塊
            for (int gy = 0; gy < blocksY; gy++)
            {
                for (int gx = 0; gx < blocksX; gx++)
                {
                    var pixels = ExtractBlockPixels(image, gx * BlockSize, gy * BlockSize);

                    // 檢查區塊是否全透明
                    if (IsBlockEmpty(pixels))
                        continue;

                    pendingBlocks.Add((gx, gy, pixels));

                    // 計算此區塊的等角投影座標
                    var (a, b) = CalculateIsometricCoords(gx, gy);
                    var (blockX, blockY) = CalculateBlockPosition(a, b);

                    minX = Math.Min(minX, blockX);
                    maxX = Math.Max(maxX, blockX + BlockSize - 1);
                    minY = Math.Min(minY, blockY);
                    maxY = Math.Max(maxY, blockY + BlockSize - 1);
                }
            }

            // 如果沒有任何有效區塊
            if (pendingBlocks.Count == 0)
            {
                frameInfo.XOffset = 0;
                frameInfo.YOffset = 0;
                frameInfo.Width = width;
                frameInfo.Height = height;
                return frameInfo;
            }

            frameInfo.XOffset = minX;
            frameInfo.YOffset = minY;
            frameInfo.Width = maxX - minX + 1;
            frameInfo.Height = maxY - minY + 1;

            // 建立區塊定義
            foreach (var (gx, gy, pixels) in pendingBlocks)
            {
                // 計算區塊的 hash 用於去重複
                string hash = ComputeBlockHash(pixels);

                int blockId;
                if (blockMap.TryGetValue(hash, out int existingId))
                {
                    blockId = existingId;
                }
                else
                {
                    blockId = allBlocks.Count;
                    allBlocks.Add(new BlockData { Pixels = pixels });
                    blockMap[hash] = blockId;
                }

                // 計算 A, B 值 (正確的等角投影座標)
                var (a, b) = CalculateIsometricCoords(gx, gy);
                var blockDef = new BlockDef
                {
                    A = (sbyte)a,
                    B = (sbyte)b,
                    BlockId = blockId
                };
                frameInfo.BlockDefs.Add(blockDef);
            }

            return frameInfo;
        }

        /// <summary>
        /// 從格子座標 (gx, gy) 計算等角投影的 A, B 座標
        /// 使公式 blockX = 24*gx, blockY = 24*gy
        /// </summary>
        private static (int a, int b) CalculateIsometricCoords(int gx, int gy)
        {
            // 從 SprReader 的公式反推:
            // blockX = 24 * (B + A - aAdj/2)
            // blockY = 12 * (B - aAdj/2)
            // 其中 aAdj = A < 0 ? A - 1 : A (用於修正整數除法捨入)
            //
            // 目標: blockX = 24*gx, blockY = 24*gy
            // 解方程得: A = gx - 2*gy
            //           B = 2*gy + (A >= 0 ? A/2 : (A-1)/2)

            int a = gx - 2 * gy;
            int b;
            if (a >= 0)
                b = 2 * gy + a / 2;
            else
                b = 2 * gy + (a - 1) / 2;

            return (a, b);
        }

        /// <summary>
        /// 從 A, B 座標計算區塊的實際像素位置
        /// </summary>
        private static (int blockX, int blockY) CalculateBlockPosition(int a, int b)
        {
            // 與 SprReader 相同的公式
            int aAdj = a;
            if (aAdj < 0) aAdj--;
            int blockX = 24 * (b + a - aAdj / 2);
            int blockY = 12 * (b - aAdj / 2);
            return (blockX, blockY);
        }

        private static ushort[,] ExtractBlockPixels(Image<Rgba32> image, int startX, int startY)
        {
            var pixels = new ushort[BlockSize, BlockSize];

            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    int imgX = startX + x;
                    int imgY = startY + y;

                    if (imgX < image.Width && imgY < image.Height)
                    {
                        var pixel = image[imgX, imgY];
                        if (pixel.A < 128) // 視為透明
                        {
                            pixels[y, x] = TransparentColor;
                        }
                        else
                        {
                            pixels[y, x] = Rgba32ToRgb555(pixel);
                        }
                    }
                    else
                    {
                        pixels[y, x] = TransparentColor;
                    }
                }
            }

            return pixels;
        }

        private static bool IsBlockEmpty(ushort[,] pixels)
        {
            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    if (pixels[y, x] != TransparentColor)
                        return false;
                }
            }
            return true;
        }

        private static string ComputeBlockHash(ushort[,] pixels)
        {
            // 簡單的 hash 計算
            unchecked
            {
                int hash = 17;
                for (int y = 0; y < BlockSize; y++)
                {
                    for (int x = 0; x < BlockSize; x++)
                    {
                        hash = hash * 31 + pixels[y, x];
                    }
                }
                return hash.ToString();
            }
        }

        private static byte[] EncodeBlock(ushort[,] pixels)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 找出有效區域
            int minX = BlockSize, maxX = -1;
            int minY = BlockSize, maxY = -1;

            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    if (pixels[y, x] != TransparentColor)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0) // 全透明區塊
            {
                writer.Write((byte)0); // startX
                writer.Write((byte)0); // startY
                writer.Write((byte)0); // unknown
                writer.Write((byte)0); // lineCount
                return ms.ToArray();
            }

            int startX = minX;
            int startY = minY;
            int lineCount = maxY - minY + 1;

            writer.Write((byte)startX);
            writer.Write((byte)startY);
            writer.Write((byte)0); // unknown
            writer.Write((byte)lineCount);

            // 編碼每一行
            for (int line = 0; line < lineCount; line++)
            {
                int y = startY + line;
                var segments = new List<(int skip, List<ushort> pixels)>();

                int x = startX;
                while (x < BlockSize)
                {
                    // 跳過透明像素
                    int skipStart = x;
                    while (x < BlockSize && pixels[y, x] == TransparentColor)
                        x++;

                    if (x >= BlockSize)
                        break;

                    int skip = (x - skipStart) * 2; // skip 值是 bytes，需要 *2

                    // 收集連續的非透明像素
                    var pixelList = new List<ushort>();
                    while (x < BlockSize && pixels[y, x] != TransparentColor)
                    {
                        pixelList.Add(pixels[y, x]);
                        x++;
                    }

                    if (pixelList.Count > 0)
                    {
                        segments.Add((skip, pixelList));
                    }
                }

                writer.Write((byte)segments.Count);

                foreach (var seg in segments)
                {
                    writer.Write((byte)seg.skip);
                    writer.Write((byte)seg.pixels.Count);
                    foreach (var color in seg.pixels)
                    {
                        writer.Write(color);
                    }
                }
            }

            return ms.ToArray();
        }

        private static ushort Rgba32ToRgb555(Rgba32 color)
        {
            int r = color.R >> 3; // 8-bit to 5-bit
            int g = color.G >> 3;
            int b = color.B >> 3;
            return (ushort)((r << 10) | (g << 5) | b);
        }

        private class FrameInfo
        {
            public int XOffset;
            public int YOffset;
            public int Width;
            public int Height;
            public List<BlockDef> BlockDefs;
        }

        private class BlockDef
        {
            public sbyte A;
            public sbyte B;
            public int BlockId;
        }

        private class BlockData
        {
            public ushort[,] Pixels;
        }
    }
}
