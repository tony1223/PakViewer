using System;
using System.Collections.Generic;
using System.IO;

namespace Lin.Helper.Core.Tile
{
    /// <summary>
    /// Lineage M (Mobile) Tile 解析器
    /// 解析 tile2_raw 格式並轉換為 L1Tile 相容的 block 資料
    /// </summary>
    public static class MTil
    {
        #region Constants

        /// <summary>Block 尺寸 (24x24)</summary>
        public const int BlockSize = 24;

        /// <summary>每個 tile 的 block 數量</summary>
        public const int BlocksPerTile = 256;

        /// <summary>
        /// DEFAULT block RLE Table A (flag bit0=0, 如 0x40, 0x42...)
        /// 標準菱形: 從上往下擴展再收縮
        /// </summary>
        private static readonly (int skip, int draw, int rowFlag)[] RleTableA = {
            (22,  2, 1), (20,  4, 1), (18,  6, 1), (16,  8, 1),
            (14, 10, 1), (12, 12, 1), (10, 14, 1), ( 8, 16, 1),
            ( 6, 18, 1), ( 4, 20, 1), ( 2, 22, 1), ( 0, 24, 1),
            ( 2, 22, 1), ( 4, 20, 1), ( 6, 18, 1), ( 8, 16, 1),
            (10, 14, 1), (12, 12, 1), (14, 10, 1), (16,  8, 1),
            (18,  6, 1), (20,  4, 1), (22,  2, 1),
        };

        /// <summary>
        /// DEFAULT block RLE Table B (flag bit0=1, 如 0x41, 0x43...)
        /// 左對齊菱形
        /// </summary>
        private static readonly (int skip, int draw, int rowFlag)[] RleTableB = {
            ( 0,  2, 1), ( 0,  4, 1), ( 0,  6, 1), ( 0,  8, 1),
            ( 0, 10, 1), ( 0, 12, 1), ( 0, 14, 1), ( 0, 16, 1),
            ( 0, 18, 1), ( 0, 20, 1), ( 0, 22, 1), ( 0, 24, 1),
            ( 0, 22, 1), ( 0, 20, 1), ( 0, 18, 1), ( 0, 16, 1),
            ( 0, 14, 1), ( 0, 12, 1), ( 0, 10, 1), ( 0,  8, 1),
            ( 0,  6, 1), ( 0,  4, 1), ( 0,  2, 1),
        };

        #endregion

        #region Data Structures

        /// <summary>
        /// 解析後的 Tile Block
        /// </summary>
        public class MBlock
        {
            public int Index { get; set; }
            public byte Flags { get; set; }
            public byte Width { get; set; }
            public byte Height { get; set; }
            public byte ColorCount { get; set; }
            public int DataSize { get; set; }
            public List<ushort> RleData { get; set; } = new List<ushort>();
            public byte[] Pixels { get; set; } = Array.Empty<byte>();

            public bool IsDefault => (Flags & 0x40) != 0;
            public bool UseTableB => (Flags & 0x01) != 0;
        }

        /// <summary>
        /// 解析結果
        /// </summary>
        public class ParseResult
        {
            public int BlockCount { get; set; }
            public bool HasGlobalPalette { get; set; }
            public ushort[] GlobalPalette { get; set; } = Array.Empty<ushort>();
            public List<MBlock> Blocks { get; set; } = new List<MBlock>();
        }

        #endregion

        #region Parsing

        /// <summary>
        /// 解析 Lineage M tile 檔案
        /// </summary>
        public static ParseResult Parse(byte[] data)
        {
            if (data == null || data.Length < 4)
                throw new ArgumentException("Data too short");

            var result = new ParseResult();
            int offset = 0;

            // Read header
            uint header = BitConverter.ToUInt32(data, offset);
            offset += 4;

            result.BlockCount = (int)(header & 0xFFF);
            result.HasGlobalPalette = ((header >> 12) & 1) == 1;

            // Read global palette
            if (result.HasGlobalPalette)
            {
                int colorCount = data[offset++];
                if (colorCount == 0) colorCount = 256;

                result.GlobalPalette = new ushort[colorCount];
                for (int i = 0; i < colorCount; i++)
                {
                    result.GlobalPalette[i] = BitConverter.ToUInt16(data, offset);
                    offset += 2;
                }
            }

            // Read flags array
            byte[] flagsArray = new byte[result.BlockCount];
            Array.Copy(data, offset, flagsArray, 0, result.BlockCount);
            offset += result.BlockCount;

            // Parse block headers
            for (int i = 0; i < result.BlockCount; i++)
            {
                byte flags = flagsArray[i];
                var block = new MBlock { Index = i, Flags = flags };

                if ((flags & 0x40) != 0)
                {
                    // DEFAULT block - no header data
                    block.Width = 24;
                    block.Height = 12;
                    block.ColorCount = 0;
                    block.DataSize = 288;
                }
                else
                {
                    // NORMAL block - read header
                    if (offset + 3 > data.Length) break;

                    block.Width = data[offset++];
                    block.Height = data[offset++];
                    block.ColorCount = data[offset++];

                    // Read RLE data
                    block.DataSize = 0;
                    for (int j = 0; j < block.ColorCount; j++)
                    {
                        if (offset + 2 > data.Length) break;
                        ushort rleValue = BitConverter.ToUInt16(data, offset);
                        offset += 2;
                        block.RleData.Add(rleValue);

                        // dataSize = sum of green channel (draw counts)
                        int g = (rleValue >> 5) & 0x1F;
                        block.DataSize += g;
                    }
                }

                result.Blocks.Add(block);
            }

            // Read pixel data
            foreach (var block in result.Blocks)
            {
                if (block.DataSize > 0)
                {
                    if (offset + block.DataSize > data.Length) break;
                    block.Pixels = new byte[block.DataSize];
                    Array.Copy(data, offset, block.Pixels, 0, block.DataSize);
                    offset += block.DataSize;
                }
            }

            return result;
        }

        /// <summary>
        /// 從檔案解析
        /// </summary>
        public static ParseResult ParseFile(string filepath)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return Parse(data);
        }

        #endregion

        #region Rendering

        /// <summary>
        /// 將 block 渲染為 24x24 的 RGB555 像素陣列
        /// </summary>
        /// <returns>24x24 = 576 個 ushort (RGB555)</returns>
        public static ushort[] RenderBlockToRgb555(MBlock block, ushort[] globalPalette)
        {
            var canvas = new ushort[BlockSize * BlockSize];

            if (block.Pixels == null || block.Pixels.Length == 0)
                return canvas;

            // Convert indexed pixels to RGB555 colors
            // MTil palette 是 RGB565，需要轉換成 RGB555
            var pixelColors = new List<ushort>();
            foreach (byte idx in block.Pixels)
            {
                if (idx < globalPalette.Length)
                    pixelColors.Add(Rgb565ToRgb555(globalPalette[idx]));
                else
                    pixelColors.Add(0);
            }

            // Get RLE entries
            var rleEntries = new List<(int skip, int draw, int rowFlag)>();

            if (block.RleData.Count > 0)
            {
                // NORMAL block: extract RLE from values
                foreach (ushort rleValue in block.RleData)
                {
                    int skip = rleValue & 0x1F;
                    int draw = (rleValue >> 5) & 0x1F;
                    int rowFlag = (rleValue >> 10) & 0x1F;
                    rleEntries.Add((skip, draw, rowFlag));
                }
            }
            else if (block.IsDefault)
            {
                // DEFAULT block: use pre-defined RLE table
                var table = block.UseTableB ? RleTableB : RleTableA;
                rleEntries.AddRange(table);
            }
            else
            {
                // No RLE - render as rectangle (fallback)
                int w = block.Width > 0 ? block.Width : 24;
                int h = block.Height > 0 ? block.Height : 12;
                for (int i = 0; i < pixelColors.Count && i < w * h; i++)
                {
                    int x = i % w;
                    int y = i / w;
                    if (x < BlockSize && y < BlockSize)
                        canvas[y * BlockSize + x] = pixelColors[i];
                }
                return canvas;
            }

            // RLE rendering
            // 對於 NORMAL blocks，Width 和 Height 欄位實際上是 x_offset 和 y_offset
            int xOffset = block.IsDefault ? 0 : block.Width;
            int yOffset = block.IsDefault ? 0 : block.Height;

            int pixelIdx = 0;
            int row = yOffset;
            int xBase = xOffset;

            foreach (var (skip, draw, rowFlag) in rleEntries)
            {
                int x = xBase + skip;

                for (int j = 0; j < draw && pixelIdx < pixelColors.Count; j++)
                {
                    int px = x + j;
                    if (px >= 0 && px < BlockSize && row >= 0 && row < BlockSize)
                    {
                        canvas[row * BlockSize + px] = pixelColors[pixelIdx];
                    }
                    pixelIdx++;
                }

                if (rowFlag > 0)
                {
                    row += rowFlag;
                    xBase = xOffset;  // 重置到 x_offset，不是 0
                }
                else
                {
                    xBase = x + draw;
                }
            }

            return canvas;
        }

        /// <summary>
        /// 將 block 渲染為 24x24 的 byte[] (L1Tile 相容格式)
        /// 全部使用 compressed format (type 2/3/6/7) 以保持正確位置
        /// </summary>
        public static byte[] RenderBlockToL1Format(MBlock block, ushort[] globalPalette)
        {
            // 全部使用壓縮格式，因為 DEFAULT blocks 的 RLE table 會造成非置中的形狀
            // 而 type 0/1 是置中的簡單菱形，位置會不對
            return RenderNormalBlockToL1Compressed(block, globalPalette);
        }

        /// <summary>
        /// DEFAULT block -> type 0/1 簡單菱形
        /// </summary>
        private static byte[] RenderDefaultBlockToL1(MBlock block, ushort[] globalPalette)
        {
            byte blockType = (byte)(block.UseTableB ? 1 : 0);
            var result = new List<byte>();
            result.Add(blockType);

            foreach (byte idx in block.Pixels)
            {
                ushort color = 0;
                if (idx < globalPalette.Length)
                {
                    color = Rgb565ToRgb555(globalPalette[idx]);
                }
                result.Add((byte)(color & 0xFF));
                result.Add((byte)((color >> 8) & 0xFF));
            }

            result.Add(0);
            return result.ToArray();
        }

        /// <summary>
        /// NORMAL block -> type 6/7 壓縮格式
        /// </summary>
        private static byte[] RenderNormalBlockToL1Compressed(MBlock block, ushort[] globalPalette)
        {
            // 先渲染到 canvas
            var canvas = RenderBlockToRgb555(block, globalPalette);

            // 找出 bounding box
            int minX = BlockSize, minY = BlockSize, maxX = -1, maxY = -1;
            for (int y = 0; y < BlockSize; y++)
            {
                for (int x = 0; x < BlockSize; x++)
                {
                    if (canvas[y * BlockSize + x] != 0)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            // 如果沒有任何非透明像素，使用原始 RLE 資料的 bounding box
            if (maxX < 0)
            {
                // 從 RLE 資料推導 bounding box
                if (block.RleData.Count > 0)
                {
                    int totalRows = 0;
                    int maxWidth = 0;
                    foreach (ushort rleValue in block.RleData)
                    {
                        int skip = rleValue & 0x1F;
                        int draw = (rleValue >> 5) & 0x1F;
                        int rowFlag = (rleValue >> 10) & 0x1F;
                        maxWidth = Math.Max(maxWidth, skip + draw);
                        if (rowFlag > 0)
                            totalRows += rowFlag;
                    }

                    if (maxWidth > 0 && totalRows > 0)
                    {
                        // 輸出一個有效的壓縮格式空 block
                        byte emptyBlockType = (byte)(block.UseTableB ? 7 : 6);
                        var emptyResult = new List<byte>();
                        emptyResult.Add(emptyBlockType);
                        emptyResult.Add(0);                    // x_offset
                        emptyResult.Add(0);                    // y_offset
                        emptyResult.Add((byte)maxWidth);       // xxLen
                        emptyResult.Add((byte)totalRows);      // yLen

                        // 每行輸出 0 個 segment (空行)
                        for (int i = 0; i < totalRows; i++)
                        {
                            emptyResult.Add(0); // segment count = 0
                        }
                        emptyResult.Add(0); // 結尾
                        return emptyResult.ToArray();
                    }
                }

                // 真的沒有任何資料，輸出最小有效結構
                return new byte[] { (byte)(block.UseTableB ? 7 : 6), 0, 0, 1, 1, 0, 0 };
            }

            // type 6 = 置中, type 7 = 左對齊
            byte blockType = (byte)(block.UseTableB ? 7 : 6);
            var result = new List<byte>();
            result.Add(blockType);
            result.Add((byte)minX);           // x_offset
            result.Add((byte)minY);           // y_offset
            result.Add((byte)(maxX - minX + 1)); // xxLen
            result.Add((byte)(maxY - minY + 1)); // yLen

            // 逐行編碼
            for (int y = minY; y <= maxY; y++)
            {
                var segments = new List<(int start, List<ushort> pixels)>();
                int x = minX;

                while (x <= maxX)
                {
                    // 跳過透明像素
                    while (x <= maxX && canvas[y * BlockSize + x] == 0)
                        x++;

                    if (x > maxX) break;

                    // 收集連續非透明像素
                    int startX = x;
                    var segPixels = new List<ushort>();
                    while (x <= maxX && canvas[y * BlockSize + x] != 0)
                    {
                        segPixels.Add(canvas[y * BlockSize + x]);
                        x++;
                    }
                    segments.Add((startX, segPixels));
                }

                // 寫入 segment count
                result.Add((byte)segments.Count);

                int currentX = minX;
                foreach (var seg in segments)
                {
                    int skip = seg.start - currentX;
                    result.Add((byte)(skip * 2)); // skip in bytes
                    result.Add((byte)seg.pixels.Count);

                    foreach (var color in seg.pixels)
                    {
                        result.Add((byte)(color & 0xFF));
                        result.Add((byte)((color >> 8) & 0xFF));
                    }
                    currentX = seg.start + seg.pixels.Count;
                }
            }

            result.Add(0); // 結尾 byte
            return result.ToArray();
        }

        #endregion

        #region Conversion to L1Tile Format

        /// <summary>
        /// 將 M Tile 轉換為 L1Til.TileBlocks
        /// </summary>
        public static L1Til.TileBlocks ConvertToL1Til(byte[] mTileData)
        {
            var parsed = Parse(mTileData);

            var offsets = new int[BlocksPerTile];
            var uniqueBlocks = new Dictionary<int, byte[]>();
            int currentOffset = 0;

            for (int i = 0; i < BlocksPerTile; i++)
            {
                byte[] blockData;

                if (i < parsed.Blocks.Count && parsed.Blocks[i].Pixels.Length > 0)
                {
                    blockData = RenderBlockToL1Format(parsed.Blocks[i], parsed.GlobalPalette);
                }
                else
                {
                    // 空 block
                    blockData = new byte[] { 0, 0 };
                }

                offsets[i] = currentOffset;
                uniqueBlocks[currentOffset] = blockData;
                currentOffset += blockData.Length - 1;
            }

            return new L1Til.TileBlocks(offsets, uniqueBlocks);
        }

        /// <summary>
        /// 從檔案轉換為 L1Til.TileBlocks
        /// </summary>
        public static L1Til.TileBlocks ConvertFileToL1Til(string filepath)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return ConvertToL1Til(data);
        }

        /// <summary>
        /// 將 M Tile 檔案轉換並儲存為 L1 .til 格式
        /// </summary>
        public static void SaveToL1Til(string inputPath, string outputPath, L1Til.CompressionType compression = L1Til.CompressionType.None)
        {
            var tileBlocks = ConvertFileToL1Til(inputPath);
            L1Til.Save(tileBlocks, outputPath, compression);
        }

        #endregion

        #region Utility

        /// <summary>
        /// RGB555 轉 RGBA (用於顯示)
        /// </summary>
        public static (byte r, byte g, byte b, byte a) Rgb555ToRgba(ushort rgb555)
        {
            if (rgb555 == 0)
                return (0, 0, 0, 0);

            int b5 = rgb555 & 0x1F;
            int g5 = (rgb555 >> 5) & 0x1F;
            int r5 = (rgb555 >> 10) & 0x1F;

            byte r8 = (byte)((r5 << 3) | (r5 >> 2));
            byte g8 = (byte)((g5 << 3) | (g5 >> 2));
            byte b8 = (byte)((b5 << 3) | (b5 >> 2));

            return (r8, g8, b8, 255);
        }

        /// <summary>
        /// 將 RGB565 轉換為 RGB555
        /// MTil palette 使用 RGB565 (R5 G6 B5)
        /// L1Til 使用 RGB555 (R5 G5 B5)
        /// </summary>
        public static ushort Rgb565ToRgb555(ushort rgb565)
        {
            if (rgb565 == 0) return 0;
            // RGB565: RRRRR GGGGGG BBBBB
            int r = (rgb565 >> 11) & 0x1F;  // bits 11-15
            int g = (rgb565 >> 5) & 0x3F;   // bits 5-10 (6 bits)
            int b = rgb565 & 0x1F;          // bits 0-4
            // 轉換 G 從 6-bit 到 5-bit
            int g5 = g >> 1;
            // RGB555: RRRRR GGGGG BBBBB
            return (ushort)((r << 10) | (g5 << 5) | b);
        }

        /// <summary>
        /// 交換 RGB555 的 R 和 B 通道 (已棄用)
        /// </summary>
        public static ushort SwapRB(ushort color)
        {
            if (color == 0) return 0;
            int r = (color >> 10) & 0x1F;
            int g = (color >> 5) & 0x1F;
            int b = color & 0x1F;
            return (ushort)((b << 10) | (g << 5) | r);
        }

        #endregion
    }
}
