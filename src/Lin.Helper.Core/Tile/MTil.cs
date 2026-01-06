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
            var pixelColors = new List<ushort>();
            foreach (byte idx in block.Pixels)
            {
                if (idx < globalPalette.Length)
                    pixelColors.Add(globalPalette[idx]);
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
            int pixelIdx = 0;
            int row = 0;
            int xBase = 0;

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
                    xBase = 0;
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
        /// 格式: type + RGB555 pixels (簡單菱形格式)
        /// </summary>
        public static byte[] RenderBlockToL1Format(MBlock block, ushort[] globalPalette)
        {
            var rgb555 = RenderBlockToRgb555(block, globalPalette);

            // 使用 L1Tile 的簡單菱形格式 (type 0)
            // 依照菱形順序輸出像素
            var result = new List<byte>();
            result.Add(0); // type = 0 (簡單菱形)

            // 24x24 菱形: 每行的像素數
            // 第 0 行: 2 像素, 第 1 行: 4 像素, ..., 第 11 行: 24 像素
            // 第 12 行: 22 像素, ..., 第 22 行: 2 像素
            for (int y = 0; y < 23; y++)
            {
                int n;
                if (y <= 11)
                    n = (y + 1) * 2;
                else
                    n = (23 - y) * 2;

                int startX = (24 - n) / 2;

                for (int x = 0; x < n; x++)
                {
                    int px = startX + x;
                    ushort color = rgb555[y * BlockSize + px];
                    result.Add((byte)(color & 0xFF));
                    result.Add((byte)((color >> 8) & 0xFF));
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

        #endregion
    }
}
