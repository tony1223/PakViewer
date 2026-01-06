using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Lin.Helper.Core.Tile
{
    /// <summary>
    /// Lineage 1 TIL 檔案解析器
    /// 支援 Classic (24x24) 和 Remaster (48x48) 版本
    /// </summary>
    public static class L1Til
    {
        #region Enums

        /// <summary>
        /// 壓縮類型
        /// </summary>
        public enum CompressionType
        {
            /// <summary>無壓縮（原始 TIL 格式）</summary>
            None,
            /// <summary>Zlib 壓縮（0x78 開頭）</summary>
            Zlib,
            /// <summary>Brotli 壓縮（0x5B 或 0x1B 開頭）</summary>
            Brotli
        }

        /// <summary>
        /// Tile 版本類型
        /// </summary>
        public enum TileVersion
        {
            /// <summary>24x24 舊版</summary>
            Classic,
            /// <summary>48x48 R版 (Remaster)</summary>
            Remaster,
            /// <summary>混合格式：block 大小在 Classic 範圍，但座標在 48x48 範圍</summary>
            Hybrid,
            /// <summary>無法判斷</summary>
            Unknown
        }

        #endregion

        #region Compression Detection & Decompression

        /// <summary>
        /// 偵測資料的壓縮類型
        /// </summary>
        public static CompressionType DetectCompression(byte[] data)
        {
            if (data == null || data.Length < 4)
                return CompressionType.None;

            byte firstByte = data[0];

            // Zlib: 0x78 開頭 (CMF byte)
            if (firstByte == 0x78)
                return CompressionType.Zlib;

            // Brotli: 0x5B 或 0x1B 開頭（Brotli stream markers）
            if (firstByte == 0x5B || firstByte == 0x1B)
                return CompressionType.Brotli;

            // 檢查是否為有效的 TIL 格式（第一個 int32 是 block count）
            int possibleBlockCount = BitConverter.ToInt32(data, 0);
            if (possibleBlockCount > 0 && possibleBlockCount <= 65536)
            {
                return CompressionType.None;
            }

            return CompressionType.None;
        }

        /// <summary>
        /// 解壓縮資料（自動偵測壓縮類型）
        /// </summary>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length < 4)
                return data;

            var compressionType = DetectCompression(data);
            return Decompress(data, compressionType);
        }

        /// <summary>
        /// 解壓縮資料（指定壓縮類型）
        /// </summary>
        public static byte[] Decompress(byte[] data, CompressionType compressionType)
        {
            if (data == null || data.Length < 4)
                return data;

            switch (compressionType)
            {
                case CompressionType.Brotli:
                    return DecompressBrotli(data);
                case CompressionType.Zlib:
                    return DecompressZlib(data);
                default:
                    return data;
            }
        }

        private static byte[] DecompressBrotli(byte[] data)
        {
            try
            {
                using var inputStream = new MemoryStream(data);
                using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                brotliStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
            catch
            {
                return data;
            }
        }

        private static byte[] DecompressZlib(byte[] data)
        {
            try
            {
                // Zlib = 2 byte header + deflate data + 4 byte checksum
                using var inputStream = new MemoryStream(data, 2, data.Length - 6);
                using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                deflateStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
            catch
            {
                return data;
            }
        }

        #endregion

        #region TileBlocks Container

        /// <summary>
        /// Tile Blocks 容器，支援共用 block 優化
        /// 多個 block index 可以指向相同的 byte[] 資料
        /// </summary>
        public class TileBlocks
        {
            private Dictionary<int, byte[]> _uniqueBlocks;
            private int[] _blockOffsets;

            public TileBlocks(int[] offsets, Dictionary<int, byte[]> uniqueBlocks)
            {
                _blockOffsets = offsets;
                _uniqueBlocks = uniqueBlocks;
            }

            /// <summary>取得指定 index 的 block 資料</summary>
            public byte[] Get(int index)
            {
                if (index < 0 || index >= _blockOffsets.Length)
                    return null;
                int offset = _blockOffsets[index];
                return _uniqueBlocks.TryGetValue(offset, out var data) ? data : null;
            }

            /// <summary>Block 總數</summary>
            public int Count => _blockOffsets.Length;

            /// <summary>不重複的 block 數量</summary>
            public int UniqueCount => _uniqueBlocks.Count;

            /// <summary>取得所有不重複的 blocks</summary>
            public IEnumerable<KeyValuePair<int, byte[]>> GetUniqueBlocks()
                => _uniqueBlocks;

            /// <summary>更新指定 offset 的 block 資料</summary>
            public void SetBlockData(int offset, byte[] newData)
            {
                if (_uniqueBlocks.ContainsKey(offset))
                    _uniqueBlocks[offset] = newData;
            }

            /// <summary>取得所有 offsets</summary>
            public int[] GetOffsets() => _blockOffsets;

            /// <summary>轉換為 List（相容舊程式）</summary>
            public List<byte[]> ToList()
            {
                var result = new List<byte[]>();
                for (int i = 0; i < _blockOffsets.Length; i++)
                {
                    result.Add(Get(i));
                }
                return result;
            }
        }

        #endregion

        #region Version Detection

        /// <summary>
        /// 判斷 til 資料是否為 R 版 (48x48) 或混合格式
        /// </summary>
        public static bool IsRemaster(byte[] tilData)
        {
            var version = GetVersion(tilData);
            return version == TileVersion.Remaster || version == TileVersion.Hybrid;
        }

        /// <summary>
        /// 取得 til 資料的版本
        /// </summary>
        public static TileVersion GetVersion(byte[] tilData)
        {
            if (tilData == null || tilData.Length < 8)
                return TileVersion.Unknown;

            try
            {
                byte[] decompressed = Decompress(tilData);

                using (var br = new BinaryReader(new MemoryStream(decompressed)))
                {
                    int blockCount = br.ReadInt32();
                    if (blockCount <= 0)
                        return TileVersion.Unknown;

                    var offsets = new List<int>();
                    for (int i = 0; i <= blockCount; i++)
                    {
                        offsets.Add(br.ReadInt32());
                    }

                    var uniqueOffsets = offsets.Distinct().OrderBy(x => x).ToList();

                    if (uniqueOffsets.Count < 2)
                        return TileVersion.Unknown;

                    int maxBlockSize = 0;
                    for (int i = 1; i < uniqueOffsets.Count; i++)
                    {
                        int diff = uniqueOffsets[i] - uniqueOffsets[i - 1];
                        if (diff > maxBlockSize)
                            maxBlockSize = diff;
                    }

                    if (maxBlockSize >= 1800)
                        return TileVersion.Remaster;
                    else if (maxBlockSize >= 10 && maxBlockSize <= 1000)
                        return TileVersion.Classic;

                    var blocks = Parse(tilData);

                    if (HasAny48x48Block(blocks))
                        return TileVersion.Remaster;

                    if (AllBlocksAreClassic(blocks))
                        return TileVersion.Classic;

                    return TileVersion.Unknown;
                }
            }
            catch
            {
                return TileVersion.Unknown;
            }
        }

        /// <summary>
        /// 取得 tile 尺寸
        /// </summary>
        public static int GetTileSize(TileVersion version)
        {
            switch (version)
            {
                case TileVersion.Classic: return 24;
                case TileVersion.Remaster: return 48;
                case TileVersion.Hybrid: return 48;
                default: return 24;
            }
        }

        /// <summary>
        /// 從 til 資料取得 tile 尺寸
        /// </summary>
        public static int GetTileSize(byte[] tilData)
        {
            return GetTileSize(GetVersion(tilData));
        }

        private static HashSet<byte> SimpleDiamondTypes = new HashSet<byte> { 0, 1, 8, 9, 16, 17 };

        private static bool HasAny48x48Block(List<byte[]> blocks)
        {
            if (blocks == null || blocks.Count == 0)
                return false;

            foreach (var block in blocks)
            {
                if (block == null || block.Length < 2)
                    continue;

                byte type = block[0];

                if (SimpleDiamondTypes.Contains(type))
                {
                    int dataLen = block.Length - 1;
                    int pixelCount = dataLen / 2;
                    if (pixelCount >= 1000)
                        return true;
                }
                else if (block.Length >= 5)
                {
                    byte x_offset = block[1];
                    byte y_offset = block[2];
                    byte xxLen = block[3];
                    byte yLen = block[4];

                    int maxX = x_offset + xxLen;
                    int maxY = y_offset + yLen;
                    if (maxX > 24 || maxY > 24 || xxLen > 24 || yLen > 24)
                        return true;
                }
            }

            return false;
        }

        private static bool AllBlocksAreClassic(List<byte[]> blocks)
        {
            if (blocks == null || blocks.Count == 0)
                return false;

            foreach (var block in blocks)
            {
                if (block == null || block.Length < 2)
                    continue;

                byte type = block[0];

                if (SimpleDiamondTypes.Contains(type))
                {
                    int dataLen = block.Length - 1;
                    int pixelCount = dataLen / 2;
                    if (pixelCount > 500)
                        return false;
                }
                else if (block.Length >= 5)
                {
                    byte x_offset = block[1];
                    byte y_offset = block[2];
                    byte xxLen = block[3];
                    byte yLen = block[4];

                    if (x_offset + xxLen > 24 || y_offset + yLen > 24)
                        return false;
                }
            }

            return true;
        }

        #endregion

        #region Parsing

        /// <summary>
        /// 解析 til 資料為 block 列表
        /// </summary>
        public static List<byte[]> Parse(byte[] srcData)
        {
            var tileBlocks = ParseToTileBlocks(srcData);
            return tileBlocks?.ToList() ?? new List<byte[]>();
        }

        /// <summary>
        /// 解析 til 資料為 TileBlocks（支援共用 block 優化）
        /// </summary>
        public static TileBlocks ParseToTileBlocks(byte[] srcData)
        {
            try
            {
                byte[] tilData = Decompress(srcData);

                using (BinaryReader br = new BinaryReader(new MemoryStream(tilData)))
                {
                    int nAllBlockCount = br.ReadInt32();

                    int[] nsBlockOffset = new int[nAllBlockCount];
                    for (int i = 0; i < nAllBlockCount; i++)
                    {
                        nsBlockOffset[i] = br.ReadInt32();
                    }
                    int endOffset = br.ReadInt32();

                    int nCurPosition = (int)br.BaseStream.Position;

                    var uniqueOffsets = new SortedSet<int>(nsBlockOffset);
                    uniqueOffsets.Add(endOffset);

                    var offsetList = uniqueOffsets.ToList();
                    var uniqueBlocks = new Dictionary<int, byte[]>();

                    for (int i = 0; i < offsetList.Count - 1; i++)
                    {
                        int offset = offsetList[i];
                        int nextOffset = offsetList[i + 1];
                        int nSize = nextOffset - offset;

                        if (nSize > 0)
                        {
                            int nPosition = nCurPosition + offset;
                            br.BaseStream.Seek(nPosition, SeekOrigin.Begin);
                            byte[] data = br.ReadBytes(nSize + 1);
                            uniqueBlocks[offset] = data;
                        }
                    }

                    return new TileBlocks(nsBlockOffset, uniqueBlocks);
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Building

        /// <summary>
        /// 從 block 列表組裝 til 檔案
        /// </summary>
        public static byte[] BuildTil(List<byte[]> blocks)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(blocks.Count);

                int currentOffset = 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    bw.Write(currentOffset);
                    currentOffset += blocks[i].Length - 1;
                }
                bw.Write(currentOffset);

                foreach (var block in blocks)
                {
                    int writeLen = block.Length - 1;
                    if (writeLen > 0)
                        bw.Write(block, 0, writeLen);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 從 TileBlocks 組裝 til 檔案（保留共用 block 結構）
        /// </summary>
        public static byte[] BuildTilFromTileBlocks(TileBlocks tileBlocks)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                int[] originalOffsets = tileBlocks.GetOffsets();
                int blockCount = originalOffsets.Length;

                bw.Write(blockCount);

                var uniqueOffsets = originalOffsets.Distinct().OrderBy(x => x).ToList();
                var offsetMapping = new Dictionary<int, int>();
                var blockDataList = new List<byte[]>();

                int newOffset = 0;
                foreach (int oldOffset in uniqueOffsets)
                {
                    offsetMapping[oldOffset] = newOffset;
                    byte[] data = null;
                    for (int i = 0; i < blockCount; i++)
                    {
                        if (originalOffsets[i] == oldOffset)
                        {
                            data = tileBlocks.Get(i);
                            break;
                        }
                    }
                    if (data != null)
                    {
                        blockDataList.Add(data);
                        newOffset += data.Length - 1;
                    }
                }

                for (int i = 0; i < blockCount; i++)
                {
                    int oldOffset = originalOffsets[i];
                    int mappedOffset = offsetMapping[oldOffset];
                    bw.Write(mappedOffset);
                }
                bw.Write(newOffset);

                foreach (var block in blockDataList)
                {
                    int writeLen = block.Length - 1;
                    if (writeLen > 0)
                        bw.Write(block, 0, writeLen);
                }

                return ms.ToArray();
            }
        }

        #endregion

        #region Downscaling

        /// <summary>
        /// 將整個 R 版 til 檔案縮小成 Classic 版
        /// </summary>
        public static byte[] DownscaleTil(byte[] tilData)
        {
            if (!IsRemaster(tilData))
                return tilData;

            var tileBlocks = ParseToTileBlocks(tilData);
            if (tileBlocks == null)
                return tilData;

            foreach (var kvp in tileBlocks.GetUniqueBlocks().ToList())
            {
                int offset = kvp.Key;
                byte[] block = kvp.Value;
                byte[] downscaled = DownscaleBlock(block);
                tileBlocks.SetBlockData(offset, downscaled);
            }

            return BuildTilFromTileBlocks(tileBlocks);
        }

        /// <summary>
        /// 將 R 版 (48x48) 的 block 縮小成 Classic 版 (24x24)
        /// </summary>
        public static byte[] DownscaleBlock(byte[] blockData)
        {
            if (blockData == null || blockData.Length < 2)
                return blockData;

            byte type = blockData[0];
            bool isSimpleDiamond = (type & 0x02) == 0;

            if (isSimpleDiamond)
            {
                return DownscaleSimpleDiamondBlock(blockData, type);
            }
            else
            {
                return DownscaleCompressedBlock(blockData, type);
            }
        }

        private static byte[] DownscaleSimpleDiamondBlock(byte[] blockData, byte type)
        {
            int srcDataLen = blockData.Length - 1;
            int srcPixelCount = srcDataLen / 2;

            if (srcPixelCount < 1000)
                return blockData;

            const int srcTileSize = 48;
            const int dstTileSize = 24;

            var srcRows = new List<ushort[]>();
            int srcOffset = 1;

            for (int ty = 0; ty < srcTileSize; ty++)
            {
                int n;
                if (ty <= srcTileSize / 2 - 1)
                    n = (ty + 1) * 2;
                else
                    n = (srcTileSize - 1 - ty) * 2;

                var row = new ushort[n];
                for (int x = 0; x < n; x++)
                {
                    if (srcOffset + 1 < blockData.Length)
                    {
                        row[x] = (ushort)(blockData[srcOffset] | (blockData[srcOffset + 1] << 8));
                        srcOffset += 2;
                    }
                }
                srcRows.Add(row);
            }

            var result = new List<byte> { type };

            for (int dstY = 0; dstY < dstTileSize; dstY++)
            {
                int dstN;
                if (dstY <= dstTileSize / 2 - 1)
                    dstN = (dstY + 1) * 2;
                else
                    dstN = (dstTileSize - 1 - dstY) * 2;

                for (int dstX = 0; dstX < dstN; dstX++)
                {
                    int srcY1 = dstY * 2;
                    int srcY2 = dstY * 2 + 1;
                    int srcX1 = dstX * 2;
                    int srcX2 = dstX * 2 + 1;

                    int r = 0, g = 0, b = 0, count = 0;

                    void SamplePixel(int sy, int sx)
                    {
                        if (sy < srcRows.Count && sx >= 0 && sx < srcRows[sy].Length)
                        {
                            ushort c = srcRows[sy][sx];
                            r += (c >> 10) & 0x1F;
                            g += (c >> 5) & 0x1F;
                            b += c & 0x1F;
                            count++;
                        }
                    }

                    SamplePixel(srcY1, srcX1);
                    SamplePixel(srcY1, srcX2);
                    SamplePixel(srcY2, srcX1);
                    SamplePixel(srcY2, srcX2);

                    if (count > 0)
                    {
                        r /= count;
                        g /= count;
                        b /= count;
                    }

                    ushort avgColor = (ushort)((r << 10) | (g << 5) | b);
                    result.Add((byte)(avgColor & 0xFF));
                    result.Add((byte)((avgColor >> 8) & 0xFF));
                }
            }

            result.Add(0);
            return result.ToArray();
        }

        private static byte[] DownscaleCompressedBlock(byte[] blockData, byte type)
        {
            if (blockData.Length < 6)
                return blockData;

            int idx = 1;
            byte x_offset = blockData[idx++];
            byte y_offset = blockData[idx++];
            byte xxLen = blockData[idx++];
            byte yLen = blockData[idx++];

            bool isHybrid = (x_offset > 24 || y_offset > 24 ||
                             x_offset + xxLen > 48 || y_offset + yLen > 48);

            if (isHybrid)
            {
                return DownscaleHybridBlock(blockData, type);
            }

            const int srcSize = 48;
            const int dstSize = 24;
            int[,] srcPixels = new int[srcSize, srcSize];
            for (int y = 0; y < srcSize; y++)
                for (int x = 0; x < srcSize; x++)
                    srcPixels[y, x] = -1;

            for (int ty = 0; ty < yLen && idx < blockData.Length - 1; ty++)
            {
                int tx = x_offset;
                byte xSegmentCount = blockData[idx++];

                for (int nx = 0; nx < xSegmentCount && idx < blockData.Length - 2; nx++)
                {
                    int skip = blockData[idx++] / 2;
                    tx += skip;
                    int xLen = blockData[idx++];

                    for (int p = 0; p < xLen && idx + 1 < blockData.Length; p++)
                    {
                        ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                        idx += 2;

                        int pixY = ty + y_offset;
                        int pixX = tx;
                        if (pixY < srcSize && pixX < srcSize)
                        {
                            srcPixels[pixY, pixX] = color;
                        }
                        tx++;
                    }
                }
            }

            int[,] dstPixels = new int[dstSize, dstSize];
            for (int y = 0; y < dstSize; y++)
                for (int x = 0; x < dstSize; x++)
                    dstPixels[y, x] = -1;

            for (int dstY = 0; dstY < dstSize; dstY++)
            {
                for (int dstX = 0; dstX < dstSize; dstX++)
                {
                    int srcY1 = dstY * 2;
                    int srcY2 = dstY * 2 + 1;
                    int srcX1 = dstX * 2;
                    int srcX2 = dstX * 2 + 1;

                    int r = 0, g = 0, b = 0, count = 0;

                    void SamplePixel(int sy, int sx)
                    {
                        if (sy < srcSize && sx < srcSize && srcPixels[sy, sx] >= 0)
                        {
                            ushort c = (ushort)srcPixels[sy, sx];
                            r += (c >> 10) & 0x1F;
                            g += (c >> 5) & 0x1F;
                            b += c & 0x1F;
                            count++;
                        }
                    }

                    SamplePixel(srcY1, srcX1);
                    SamplePixel(srcY1, srcX2);
                    SamplePixel(srcY2, srcX1);
                    SamplePixel(srcY2, srcX2);

                    if (count > 0)
                    {
                        r /= count;
                        g /= count;
                        b /= count;
                        dstPixels[dstY, dstX] = (r << 10) | (g << 5) | b;
                    }
                }
            }

            return EncodeCompressedBlock(dstPixels, type, dstSize);
        }

        private static byte[] DownscaleHybridBlock(byte[] blockData, byte type)
        {
            if (blockData.Length < 6)
                return blockData;

            byte x_offset = blockData[1];
            byte y_offset = blockData[2];
            byte xxLen = blockData[3];
            byte yLen = blockData[4];

            byte new_x_offset = (byte)(x_offset / 2);
            byte new_y_offset = (byte)(y_offset / 2);

            const int dstSize = 24;
            int[,] dstPixels = new int[dstSize, dstSize];
            for (int y = 0; y < dstSize; y++)
                for (int x = 0; x < dstSize; x++)
                    dstPixels[y, x] = -1;

            int idx = 5;

            for (int ty = 0; ty < yLen && idx < blockData.Length - 1; ty++)
            {
                int tx = x_offset;
                byte xSegmentCount = blockData[idx++];

                for (int nx = 0; nx < xSegmentCount && idx < blockData.Length - 2; nx++)
                {
                    int skip = blockData[idx++] / 2;
                    tx += skip;
                    int xLen = blockData[idx++];

                    for (int p = 0; p < xLen && idx + 1 < blockData.Length; p++)
                    {
                        ushort color = (ushort)(blockData[idx] | (blockData[idx + 1] << 8));
                        idx += 2;

                        int pixY = (ty + y_offset) / 2;
                        int pixX = tx / 2;

                        if (pixY >= 0 && pixY < dstSize && pixX >= 0 && pixX < dstSize)
                        {
                            dstPixels[pixY, pixX] = color;
                        }
                        tx++;
                    }
                }
            }

            return EncodeCompressedBlock(dstPixels, type, dstSize);
        }

        private static byte[] EncodeCompressedBlock(int[,] pixels, byte type, int size)
        {
            var result = new List<byte>();
            result.Add(type);

            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y, x] >= 0)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0)
            {
                result.Add(0);
                result.Add(0);
                result.Add(0);
                result.Add(0);
                result.Add(0);
                return result.ToArray();
            }

            byte x_offset = (byte)minX;
            byte y_offset = (byte)minY;
            byte xxLen = (byte)(maxX - minX + 1);
            byte yLen = (byte)(maxY - minY + 1);

            result.Add(x_offset);
            result.Add(y_offset);
            result.Add(xxLen);
            result.Add(yLen);

            for (int y = minY; y <= maxY; y++)
            {
                var segments = new List<(int start, List<ushort> pixels)>();
                int x = minX;
                while (x <= maxX)
                {
                    while (x <= maxX && pixels[y, x] < 0)
                        x++;

                    if (x > maxX)
                        break;

                    int startX = x;
                    var segmentPixels = new List<ushort>();
                    while (x <= maxX && pixels[y, x] >= 0)
                    {
                        segmentPixels.Add((ushort)pixels[y, x]);
                        x++;
                    }
                    segments.Add((startX, segmentPixels));
                }

                result.Add((byte)segments.Count);

                int currentX = x_offset;
                foreach (var seg in segments)
                {
                    int skip = seg.start - currentX;
                    result.Add((byte)(skip * 2));
                    result.Add((byte)seg.pixels.Count);

                    foreach (var color in seg.pixels)
                    {
                        result.Add((byte)(color & 0xFF));
                        result.Add((byte)((color >> 8) & 0xFF));
                    }

                    currentX = seg.start + seg.pixels.Count;
                }
            }

            result.Add(0);
            return result.ToArray();
        }

        #endregion

        #region Save

        /// <summary>
        /// 將 TileBlocks 儲存為 .til 檔案
        /// </summary>
        public static void Save(TileBlocks tileBlocks, string filePath, CompressionType compression = CompressionType.None)
        {
            byte[] data = BuildTilFromTileBlocks(tileBlocks);
            data = Compress(data, compression);
            File.WriteAllBytes(filePath, data);
        }

        /// <summary>
        /// 將 block 列表儲存為 .til 檔案
        /// </summary>
        public static void Save(List<byte[]> blocks, string filePath, CompressionType compression = CompressionType.None)
        {
            byte[] data = BuildTil(blocks);
            data = Compress(data, compression);
            File.WriteAllBytes(filePath, data);
        }

        /// <summary>
        /// 壓縮資料
        /// </summary>
        public static byte[] Compress(byte[] data, CompressionType compressionType)
        {
            if (data == null || data.Length == 0)
                return data;

            switch (compressionType)
            {
                case CompressionType.Brotli:
                    return CompressBrotli(data);
                case CompressionType.Zlib:
                    return CompressZlib(data);
                default:
                    return data;
            }
        }

        private static byte[] CompressBrotli(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal))
            {
                brotliStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        private static byte[] CompressZlib(byte[] data)
        {
            using var outputStream = new MemoryStream();
            // Zlib header: CMF=0x78, FLG=0x9C (default compression)
            outputStream.WriteByte(0x78);
            outputStream.WriteByte(0x9C);

            using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflateStream.Write(data, 0, data.Length);
            }

            // Adler-32 checksum
            uint adler = ComputeAdler32(data);
            outputStream.WriteByte((byte)((adler >> 24) & 0xFF));
            outputStream.WriteByte((byte)((adler >> 16) & 0xFF));
            outputStream.WriteByte((byte)((adler >> 8) & 0xFF));
            outputStream.WriteByte((byte)(adler & 0xFF));

            return outputStream.ToArray();
        }

        private static uint ComputeAdler32(byte[] data)
        {
            const uint MOD_ADLER = 65521;
            uint a = 1, b = 0;
            foreach (byte c in data)
            {
                a = (a + c) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }
            return (b << 16) | a;
        }

        #endregion

        #region Block Analysis

        /// <summary>
        /// Block 分析結果
        /// </summary>
        public class BlockAnalysis
        {
            public byte Type { get; set; }
            public int Size { get; set; }
            public bool IsSimpleDiamond { get; set; }
            public int EstimatedTileSize { get; set; }
            public string Format { get; set; }
            public byte XOffset { get; set; }
            public byte YOffset { get; set; }
            public byte XxLen { get; set; }
            public byte YLen { get; set; }
            public int MaxX => XOffset + XxLen;
            public int MaxY => YOffset + YLen;
        }

        /// <summary>
        /// 分析單個 block 的尺寸資訊
        /// </summary>
        public static BlockAnalysis AnalyzeBlock(byte[] blockData)
        {
            var result = new BlockAnalysis
            {
                Size = blockData?.Length ?? 0,
                Format = "Unknown"
            };

            if (blockData == null || blockData.Length < 2)
                return result;

            result.Type = blockData[0];
            result.IsSimpleDiamond = SimpleDiamondTypes.Contains(result.Type);

            if (result.IsSimpleDiamond)
            {
                int dataLen = blockData.Length - 1;
                int pixelCount = dataLen / 2;

                if (pixelCount >= 1000)
                {
                    result.EstimatedTileSize = 48;
                    result.Format = "48x48";
                }
                else if (pixelCount >= 200)
                {
                    result.EstimatedTileSize = 24;
                    result.Format = "24x24";
                }
                else
                {
                    result.EstimatedTileSize = 0;
                    result.Format = $"Unknown ({pixelCount} pixels)";
                }
            }
            else if (blockData.Length >= 5)
            {
                result.XOffset = blockData[1];
                result.YOffset = blockData[2];
                result.XxLen = blockData[3];
                result.YLen = blockData[4];

                int maxCoord = Math.Max(result.MaxX, result.MaxY);
                int maxLen = Math.Max(result.XxLen, result.YLen);

                if (maxCoord > 24 || maxLen > 24)
                {
                    result.EstimatedTileSize = 48;
                    if (maxLen > 24)
                        result.Format = "48x48 (Remaster)";
                    else
                        result.Format = "48x48 coords (Hybrid)";
                }
                else
                {
                    result.EstimatedTileSize = 24;
                    result.Format = "24x24";
                }
            }

            return result;
        }

        /// <summary>
        /// 分析整個 til 的所有 blocks
        /// </summary>
        public static (int classic, int remaster, int hybrid, int unknown) AnalyzeTilBlocks(byte[] tilData)
        {
            var blocks = Parse(tilData);
            int classic = 0, remaster = 0, hybrid = 0, unknown = 0;

            foreach (var block in blocks)
            {
                var analysis = AnalyzeBlock(block);
                if (analysis.Format.Contains("24x24"))
                    classic++;
                else if (analysis.Format.Contains("Remaster"))
                    remaster++;
                else if (analysis.Format.Contains("Hybrid"))
                    hybrid++;
                else
                    unknown++;
            }

            return (classic, remaster, hybrid, unknown);
        }

        #endregion

        #region Checksum

        /// <summary>
        /// 計算單個 block 的 MD5 checksum
        /// </summary>
        public static string GetBlockMd5(byte[] blockData)
        {
            if (blockData == null || blockData.Length == 0)
                return string.Empty;

            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(blockData);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 計算 tile 中所有 blocks 的 MD5 checksums
        /// </summary>
        /// <returns>每個 block index 對應的 MD5 字串</returns>
        public static string[] GetAllBlockMd5(byte[] tilData)
        {
            var blocks = Parse(tilData);
            var checksums = new string[blocks.Count];

            for (int i = 0; i < blocks.Count; i++)
            {
                checksums[i] = GetBlockMd5(blocks[i]);
            }

            return checksums;
        }

        /// <summary>
        /// 從檔案計算所有 blocks 的 MD5 checksums
        /// </summary>
        public static string[] GetAllBlockMd5FromFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            return GetAllBlockMd5(data);
        }

        /// <summary>
        /// 比較兩個 tile 檔案的 blocks，回傳差異資訊
        /// </summary>
        public static (int matched, int different, int onlyInFirst, int onlyInSecond, List<int> diffIndices)
            CompareBlocks(string file1, string file2)
        {
            var blocks1 = Parse(File.ReadAllBytes(file1));
            var blocks2 = Parse(File.ReadAllBytes(file2));

            int matched = 0, different = 0;
            var diffIndices = new List<int>();

            int maxCount = Math.Max(blocks1.Count, blocks2.Count);
            int minCount = Math.Min(blocks1.Count, blocks2.Count);

            for (int i = 0; i < minCount; i++)
            {
                string md5_1 = GetBlockMd5(blocks1[i]);
                string md5_2 = GetBlockMd5(blocks2[i]);

                if (md5_1 == md5_2)
                    matched++;
                else
                {
                    different++;
                    diffIndices.Add(i);
                }
            }

            int onlyInFirst = blocks1.Count > minCount ? blocks1.Count - minCount : 0;
            int onlyInSecond = blocks2.Count > minCount ? blocks2.Count - minCount : 0;

            return (matched, different, onlyInFirst, onlyInSecond, diffIndices);
        }

        #endregion
    }
}
