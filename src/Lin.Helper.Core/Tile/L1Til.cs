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
        /// <param name="srcData">TIL 檔案資料</param>
        /// <param name="validateFormat">是否驗證格式（預設 true）</param>
        /// <returns>TileBlocks 或 null（解析失敗時）</returns>
        public static TileBlocks ParseToTileBlocks(byte[] srcData, bool validateFormat = true)
        {
            try
            {
                byte[] tilData = Decompress(srcData);

                using (BinaryReader br = new BinaryReader(new MemoryStream(tilData)))
                {
                    int nAllBlockCount = br.ReadInt32();

                    // 基本驗證：block 數量
                    if (nAllBlockCount <= 0 || nAllBlockCount > 65536)
                        return null;

                    int[] nsBlockOffset = new int[nAllBlockCount];
                    for (int i = 0; i < nAllBlockCount; i++)
                    {
                        nsBlockOffset[i] = br.ReadInt32();
                    }
                    int endOffset = br.ReadInt32();

                    int nCurPosition = (int)br.BaseStream.Position;
                    int dataLength = (int)(br.BaseStream.Length - nCurPosition);

                    // 驗證 endOffset 是否合理
                    if (validateFormat && endOffset > dataLength)
                    {
                        // 檔案被截斷：endOffset 超過實際資料長度
                        return null;
                    }

                    var uniqueOffsets = new SortedSet<int>(nsBlockOffset);
                    uniqueOffsets.Add(endOffset);

                    var offsetList = uniqueOffsets.ToList();

                    // 驗證格式：檢查是否有太多異常小的 block
                    if (validateFormat && offsetList.Count > 10)
                    {
                        int tinyBlockCount = 0;
                        for (int i = 0; i < offsetList.Count - 1; i++)
                        {
                            int blockSize = offsetList[i + 1] - offsetList[i];
                            // 正常的 block 至少應該有幾個 bytes（type + 一些資料）
                            // 如果 blockSize <= 2，很可能是損壞的偏移表
                            if (blockSize <= 2)
                                tinyBlockCount++;
                        }
                        // 如果超過 10% 的 blocks 都是異常小的，視為格式錯誤
                        if (tinyBlockCount > offsetList.Count / 10)
                            return null;
                    }

                    var uniqueBlocks = new Dictionary<int, byte[]>();

                    for (int i = 0; i < offsetList.Count - 1; i++)
                    {
                        int offset = offsetList[i];
                        int nextOffset = offsetList[i + 1];
                        int nSize = nextOffset - offset;

                        if (nSize > 0)
                        {
                            int nPosition = nCurPosition + offset;

                            // 驗證讀取位置是否在檔案範圍內
                            if (validateFormat && (nPosition + nSize > br.BaseStream.Length))
                            {
                                // 資料被截斷
                                return null;
                            }

                            br.BaseStream.Seek(nPosition, SeekOrigin.Begin);
                            byte[] data = br.ReadBytes(nSize);
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

        #region Rendering

        /// <summary>
        /// 將 block 渲染到 RGB555 canvas
        /// </summary>
        /// <param name="blockData">Block 資料</param>
        /// <param name="destX">目標 X 座標</param>
        /// <param name="destY">目標 Y 座標</param>
        /// <param name="canvas">RGB555 canvas (ushort[])</param>
        /// <param name="canvasWidth">Canvas 寬度</param>
        /// <param name="canvasHeight">Canvas 高度</param>
        public static void RenderBlock(byte[] blockData, int destX, int destY, ushort[] canvas, int canvasWidth, int canvasHeight)
        {
            if (blockData == null || blockData.Length < 2) return;

            int idx = 0;
            byte type = blockData[idx++];

            // Type 1: 左對齊菱形 (bit1=0, bit0=1)
            if ((type & 0x02) == 0 && (type & 0x01) != 0)
            {
                for (int ty = 0; ty < 24 && idx < blockData.Length - 1; ty++)
                {
                    int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                    int tx = 0; // 左對齊
                    for (int p = 0; p < n && idx < blockData.Length - 1; p++)
                    {
                        ushort color = (ushort)(blockData[idx++] | (blockData[idx++] << 8));
                        int px = destX + tx;
                        int py = destY + ty;
                        if (px >= 0 && px < canvasWidth && py >= 0 && py < canvasHeight)
                        {
                            canvas[py * canvasWidth + px] = color;
                        }
                        tx++;
                    }
                }
            }
            // Type 0: 靠右對齊菱形 (bit1=0, bit0=0)
            else if ((type & 0x02) == 0 && (type & 0x01) == 0)
            {
                for (int ty = 0; ty < 24 && idx < blockData.Length - 1; ty++)
                {
                    int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                    int tx = 24 - n; // 靠右對齊
                    for (int p = 0; p < n && idx < blockData.Length - 1; p++)
                    {
                        ushort color = (ushort)(blockData[idx++] | (blockData[idx++] << 8));
                        int px = destX + tx;
                        int py = destY + ty;
                        if (px >= 0 && px < canvasWidth && py >= 0 && py < canvasHeight)
                        {
                            canvas[py * canvasWidth + px] = color;
                        }
                        tx++;
                    }
                }
            }
            // 壓縮格式 (type 2/3/6/7 等)
            else if (blockData.Length >= 5)
            {
                byte xOffset = blockData[idx++];
                byte yOffset = blockData[idx++];
                byte xxLen = blockData[idx++];
                byte yLen = blockData[idx++];

                for (int ty = 0; ty < yLen && idx < blockData.Length; ty++)
                {
                    int tx = xOffset;
                    byte segCount = blockData[idx++];
                    for (int seg = 0; seg < segCount && idx < blockData.Length - 1; seg++)
                    {
                        int skip = blockData[idx++];
                        int count = blockData[idx++];
                        tx += skip / 2;

                        for (int p = 0; p < count && idx < blockData.Length - 1; p++)
                        {
                            ushort color = (ushort)(blockData[idx++] | (blockData[idx++] << 8));
                            int px = destX + tx;
                            int py = destY + ty + yOffset;
                            if (px >= 0 && px < canvasWidth && py >= 0 && py < canvasHeight)
                            {
                                canvas[py * canvasWidth + px] = color;
                            }
                            tx++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 將 block 渲染到 24x24 的 RGB555 canvas
        /// </summary>
        public static ushort[] RenderBlockToCanvas(byte[] blockData)
        {
            var canvas = new ushort[24 * 24];
            RenderBlock(blockData, 0, 0, canvas, 24, 24);
            return canvas;
        }

        /// <summary>
        /// 將 block 渲染到 BGRA32 格式 (用於 WPF/現代圖形)
        /// </summary>
        public static void RenderBlockToBgra(byte[] blockData, int destX, int destY, byte[] canvas, int canvasWidth, int canvasHeight, byte bgR = 0, byte bgG = 0, byte bgB = 0)
        {
            RenderBlockToBgra(blockData, destX, destY, canvas, canvasWidth, canvasHeight, bgR, bgG, bgB, applyTypeAlpha: false);
        }

        /// <summary>
        /// 將 block 渲染到 BGRA32 格式，支援完整 type 特效
        /// - bit0/1: 菱形方向/壓縮格式 (由 RenderBlock 處理)
        /// - bit2 (0x04): 半透明 50%
        /// - bit4 (0x10): Inverted alpha - 雲 (白=不透明，黑=透明)
        /// - bit5 (0x20): Inverted alpha - 煙/血 (黑=不透明，白=透明)
        /// </summary>
        /// <param name="blockData">Block 資料</param>
        /// <param name="destX">目標 X 座標</param>
        /// <param name="destY">目標 Y 座標</param>
        /// <param name="canvas">BGRA32 畫布</param>
        /// <param name="canvasWidth">畫布寬度</param>
        /// <param name="canvasHeight">畫布高度</param>
        /// <param name="bgR">背景紅色分量</param>
        /// <param name="bgG">背景綠色分量</param>
        /// <param name="bgB">背景藍色分量</param>
        /// <param name="applyTypeAlpha">是否根據 block type 套用透明度效果</param>
        /// <param name="transparentBackground">是否使用透明背景 (true 時忽略 bgR/bgG/bgB)</param>
        public static void RenderBlockToBgra(byte[] blockData, int destX, int destY, byte[] canvas, int canvasWidth, int canvasHeight, byte bgR, byte bgG, byte bgB, bool applyTypeAlpha, bool transparentBackground = false)
        {
            if (blockData == null || blockData.Length < 1)
                return;

            var rgb555Canvas = new ushort[canvasWidth * canvasHeight];
            RenderBlock(blockData, destX, destY, rgb555Canvas, canvasWidth, canvasHeight);

            byte blockType = blockData[0];
            bool hasBit2 = (blockType & 0x04) != 0;         // 半透明
            bool hasInvAlpha = HasInvertedAlpha(blockType); // bit4 or bit5

            // Convert RGB555 to BGRA32
            for (int y = 0; y < canvasHeight; y++)
            {
                for (int x = 0; x < canvasWidth; x++)
                {
                    int srcIdx = y * canvasWidth + x;
                    int dstIdx = srcIdx * 4;
                    ushort rgb555 = rgb555Canvas[srcIdx];

                    if (rgb555 == 0)
                    {
                        if (transparentBackground)
                        {
                            canvas[dstIdx + 0] = 0;
                            canvas[dstIdx + 1] = 0;
                            canvas[dstIdx + 2] = 0;
                            canvas[dstIdx + 3] = 0; // 全透明
                        }
                        else
                        {
                            canvas[dstIdx + 0] = bgB; // B
                            canvas[dstIdx + 1] = bgG; // G
                            canvas[dstIdx + 2] = bgR; // R
                            canvas[dstIdx + 3] = 255; // A (背景不透明)
                        }
                    }
                    else if (applyTypeAlpha && hasInvAlpha)
                    {
                        // Inverted alpha: bit4 (雲) 或 bit5 (煙/血)
                        byte alpha = CalculateInvertedAlpha(rgb555, blockType);
                        if (alpha < 8)
                        {
                            canvas[dstIdx + 0] = 0;
                            canvas[dstIdx + 1] = 0;
                            canvas[dstIdx + 2] = 0;
                            canvas[dstIdx + 3] = 0; // 全透明
                        }
                        else
                        {
                            ushort renderColor = GetInvertedAlphaRenderColor(rgb555, blockType);
                            int b5 = renderColor & 0x1F;
                            int g5 = (renderColor >> 5) & 0x1F;
                            int r5 = (renderColor >> 10) & 0x1F;
                            canvas[dstIdx + 0] = (byte)((b5 << 3) | (b5 >> 2)); // B
                            canvas[dstIdx + 1] = (byte)((g5 << 3) | (g5 >> 2)); // G
                            canvas[dstIdx + 2] = (byte)((r5 << 3) | (r5 >> 2)); // R
                            canvas[dstIdx + 3] = alpha;
                        }
                    }
                    else
                    {
                        int b5 = rgb555 & 0x1F;
                        int g5 = (rgb555 >> 5) & 0x1F;
                        int r5 = (rgb555 >> 10) & 0x1F;
                        canvas[dstIdx + 0] = (byte)((b5 << 3) | (b5 >> 2)); // B
                        canvas[dstIdx + 1] = (byte)((g5 << 3) | (g5 >> 2)); // G
                        canvas[dstIdx + 2] = (byte)((r5 << 3) | (r5 >> 2)); // R
                        // bit2 半透明 (50% opacity)
                        canvas[dstIdx + 3] = (applyTypeAlpha && hasBit2) ? (byte)128 : (byte)255;
                    }
                }
            }
        }

        /// <summary>
        /// 檢查 block type 是否有特殊渲染標記 (bit2)
        /// </summary>
        public static bool HasSpecialRenderingFlag(byte[] blockData)
        {
            if (blockData == null || blockData.Length < 1)
                return false;
            return (blockData[0] & 0x04) != 0;
        }

        /// <summary>
        /// 直接渲染到 unsafe byte* 緩衝區 (高效能版本，RGB555 格式)
        /// </summary>
        public static unsafe void RenderBlockDirect(byte[] blockData, int destX, int destY, byte* buffer, int rowPitch, int maxWidth, int maxHeight)
        {
            if (blockData == null || blockData.Length < 2) return;

            fixed (byte* tilPtr = blockData)
            {
                byte* ptr = tilPtr;
                byte type = *(ptr++);

                // Type 1: 左對齊菱形
                if ((type & 0x02) == 0 && (type & 0x01) != 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 0;
                        for (int p = 0; p < n; p++)
                        {
                            ushort color = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                *(buffer + offset) = (byte)(color & 0xFF);
                                *(buffer + offset + 1) = (byte)(color >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // Type 0: 靠右對齊菱形
                else if ((type & 0x02) == 0 && (type & 0x01) == 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 24 - n;
                        for (int p = 0; p < n; p++)
                        {
                            ushort color = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                *(buffer + offset) = (byte)(color & 0xFF);
                                *(buffer + offset + 1) = (byte)(color >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // 壓縮格式
                else
                {
                    byte xOffset = *(ptr++);
                    byte yOffset = *(ptr++);
                    byte xxLen = *(ptr++);
                    byte yLen = *(ptr++);

                    for (int ty = 0; ty < yLen; ty++)
                    {
                        int tx = xOffset;
                        byte segCount = *(ptr++);
                        for (int seg = 0; seg < segCount; seg++)
                        {
                            tx += *(ptr++) / 2;
                            int count = *(ptr++);
                            for (int p = 0; p < count; p++)
                            {
                                ushort color = (ushort)(*(ptr++) | (*(ptr++) << 8));
                                int px = destX + tx;
                                int py = destY + ty + yOffset;
                                if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                                {
                                    int offset = py * rowPitch + (px * 2);
                                    *(buffer + offset) = (byte)(color & 0xFF);
                                    *(buffer + offset + 1) = (byte)(color >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// RGB555 轉 RGB565
        /// RGB555: 0RRRRRGGGGGBBBBB -> RGB565: RRRRRGGGGGGBBBBB
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ushort Rgb555ToRgb565(ushort rgb555)
        {
            // RGB555: 0RRRRR GGGGG BBBBB (bit 15 unused)
            // RGB565: RRRRR GGGGGG BBBBB
            int r = (rgb555 >> 10) & 0x1F;  // 5 bits
            int g = (rgb555 >> 5) & 0x1F;   // 5 bits -> 6 bits
            int b = rgb555 & 0x1F;          // 5 bits
            // G 從 5 bits 擴展到 6 bits: 左移 1 位並複製最高位到最低位
            int g6 = (g << 1) | (g >> 4);
            return (ushort)((r << 11) | (g6 << 5) | b);
        }

        /// <summary>
        /// 直接渲染到 unsafe byte* 緩衝區 (高效能版本，RGB565 格式)，支援完整 type 特效
        /// - bit2 (0x04): 半透明 50%
        /// - bit4 (0x10): Inverted alpha - 雲 (白=不透明，黑=透明)
        /// - bit5 (0x20): Inverted alpha - 煙/血 (黑=不透明，白=透明)
        /// </summary>
        public static unsafe void RenderBlockDirectRgb565(byte[] blockData, int destX, int destY, byte* buffer, int rowPitch, int maxWidth, int maxHeight, bool applyTypeAlpha)
        {
            if (blockData == null || blockData.Length < 2) return;

            byte blockType = blockData[0];
            bool hasBit2 = applyTypeAlpha && (blockType & 0x04) != 0;         // 半透明
            bool hasInvAlpha = applyTypeAlpha && HasInvertedAlpha(blockType); // bit4 or bit5
            bool needsBlend = hasBit2 || hasInvAlpha;

            fixed (byte* tilPtr = blockData)
            {
                byte* ptr = tilPtr;
                byte type = *(ptr++);

                // Type 1: 左對齊菱形
                if ((type & 0x02) == 0 && (type & 0x01) != 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 0;
                        for (int p = 0; p < n; p++)
                        {
                            ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                ushort result;
                                if (needsBlend)
                                {
                                    ushort dstColor565 = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                    result = BlendPixelWithAlphaRgb565(srcColor, dstColor565, type, hasInvAlpha, hasBit2);
                                }
                                else
                                {
                                    result = Rgb555ToRgb565(srcColor);
                                }
                                *(buffer + offset) = (byte)(result & 0xFF);
                                *(buffer + offset + 1) = (byte)(result >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // Type 0: 靠右對齊菱形
                else if ((type & 0x02) == 0 && (type & 0x01) == 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 24 - n;
                        for (int p = 0; p < n; p++)
                        {
                            ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                ushort result;
                                if (needsBlend)
                                {
                                    ushort dstColor565 = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                    result = BlendPixelWithAlphaRgb565(srcColor, dstColor565, type, hasInvAlpha, hasBit2);
                                }
                                else
                                {
                                    result = Rgb555ToRgb565(srcColor);
                                }
                                *(buffer + offset) = (byte)(result & 0xFF);
                                *(buffer + offset + 1) = (byte)(result >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // 壓縮格式
                else
                {
                    byte xOffset = *(ptr++);
                    byte yOffset = *(ptr++);
                    byte xxLen = *(ptr++);
                    byte yLen = *(ptr++);

                    for (int ty = 0; ty < yLen; ty++)
                    {
                        int tx = xOffset;
                        byte segCount = *(ptr++);
                        for (int seg = 0; seg < segCount; seg++)
                        {
                            tx += *(ptr++) / 2;
                            int count = *(ptr++);
                            for (int p = 0; p < count; p++)
                            {
                                ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                                int px = destX + tx;
                                int py = destY + ty + yOffset;
                                if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                                {
                                    int offset = py * rowPitch + (px * 2);
                                    ushort result;
                                    if (needsBlend)
                                    {
                                        ushort dstColor565 = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                        result = BlendPixelWithAlphaRgb565(srcColor, dstColor565, type, hasInvAlpha, hasBit2);
                                    }
                                    else
                                    {
                                        result = Rgb555ToRgb565(srcColor);
                                    }
                                    *(buffer + offset) = (byte)(result & 0xFF);
                                    *(buffer + offset + 1) = (byte)(result >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 混合像素並輸出 RGB565 (支援 bit2 半透明和 bit4/5 inverted alpha)
        /// srcColor 為 RGB555，dstColor 為 RGB565
        /// </summary>
        private static ushort BlendPixelWithAlphaRgb565(ushort srcColor, ushort dstColor565, byte blockType, bool hasInvAlpha, bool hasBit2)
        {
            if (hasInvAlpha)
            {
                // Inverted alpha: 根據亮度計算 alpha
                byte alpha = CalculateInvertedAlpha(srcColor, blockType);
                if (alpha < 8) return dstColor565; // 幾乎透明，保留背景
                ushort renderColor = GetInvertedAlphaRenderColor(srcColor, blockType);
                ushort renderColor565 = Rgb555ToRgb565(renderColor);
                return BlendRgb565WithAlpha(dstColor565, renderColor565, alpha);
            }
            else if (hasBit2)
            {
                // bit2: 50% 混合
                ushort srcColor565 = Rgb555ToRgb565(srcColor);
                return BlendRgb565(srcColor565, dstColor565);
            }
            return Rgb555ToRgb565(srcColor);
        }

        /// <summary>
        /// RGB565 顏色混合 (50% opacity)
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ushort BlendRgb565(ushort src, ushort dst)
        {
            // RGB565 格式: RRRRRGGGGGGBBBBB
            int srcR = (src >> 11) & 0x1F;
            int srcG = (src >> 5) & 0x3F;
            int srcB = src & 0x1F;

            int dstR = (dst >> 11) & 0x1F;
            int dstG = (dst >> 5) & 0x3F;
            int dstB = dst & 0x1F;

            // 50% blend: (src + dst) / 2
            int blendR = (srcR + dstR) >> 1;
            int blendG = (srcG + dstG) >> 1;
            int blendB = (srcB + dstB) >> 1;

            return (ushort)((blendR << 11) | (blendG << 5) | blendB);
        }

        /// <summary>
        /// RGB565 alpha blending
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ushort BlendRgb565WithAlpha(ushort bg, ushort fg, byte alpha)
        {
            if (alpha == 255) return fg;
            if (alpha == 0) return bg;

            uint invAlpha = (uint)(255 - alpha);

            uint bgR = (uint)((bg >> 11) & 0x1F);
            uint bgG = (uint)((bg >> 5) & 0x3F);
            uint bgB = (uint)(bg & 0x1F);

            uint fgR = (uint)((fg >> 11) & 0x1F);
            uint fgG = (uint)((fg >> 5) & 0x3F);
            uint fgB = (uint)(fg & 0x1F);

            uint outR = (bgR * invAlpha + fgR * alpha) / 255;
            uint outG = (bgG * invAlpha + fgG * alpha) / 255;
            uint outB = (bgB * invAlpha + fgB * alpha) / 255;

            return (ushort)((outR << 11) | (outG << 5) | outB);
        }

        /// <summary>
        /// 直接渲染到 unsafe byte* 緩衝區 (高效能版本，RGB555 格式)，支援完整 type 特效
        /// - bit2 (0x04): 半透明 50%
        /// - bit4 (0x10): Inverted alpha - 雲 (白=不透明，黑=透明)
        /// - bit5 (0x20): Inverted alpha - 煙/血 (黑=不透明，白=透明)
        /// </summary>
        /// <param name="blockData">block 資料</param>
        /// <param name="destX">目標 X 座標</param>
        /// <param name="destY">目標 Y 座標</param>
        /// <param name="buffer">目標緩衝區指標</param>
        /// <param name="rowPitch">每行的 byte 數</param>
        /// <param name="maxWidth">最大寬度</param>
        /// <param name="maxHeight">最大高度</param>
        /// <param name="applyTypeAlpha">是否根據 block type 套用透明度效果</param>
        public static unsafe void RenderBlockDirect(byte[] blockData, int destX, int destY, byte* buffer, int rowPitch, int maxWidth, int maxHeight, bool applyTypeAlpha)
        {
            if (blockData == null || blockData.Length < 2) return;

            byte blockType = blockData[0];
            bool hasBit2 = (blockType & 0x04) != 0;         // 半透明
            bool hasInvAlpha = HasInvertedAlpha(blockType); // bit4 or bit5

            // 不需要任何 alpha 效果，使用原本的高效能版本
            if (!applyTypeAlpha || (!hasBit2 && !hasInvAlpha))
            {
                RenderBlockDirect(blockData, destX, destY, buffer, rowPitch, maxWidth, maxHeight);
                return;
            }

            // 需要 alpha 處理
            fixed (byte* tilPtr = blockData)
            {
                byte* ptr = tilPtr;
                byte type = *(ptr++);

                // Type 1: 左對齊菱形
                if ((type & 0x02) == 0 && (type & 0x01) != 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 0;
                        for (int p = 0; p < n; p++)
                        {
                            ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                ushort dstColor = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                ushort blended = BlendPixelWithAlpha(srcColor, dstColor, type, hasInvAlpha, hasBit2);
                                *(buffer + offset) = (byte)(blended & 0xFF);
                                *(buffer + offset + 1) = (byte)(blended >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // Type 0: 靠右對齊菱形
                else if ((type & 0x02) == 0 && (type & 0x01) == 0)
                {
                    for (int ty = 0; ty < 24; ty++)
                    {
                        int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                        int tx = 24 - n;
                        for (int p = 0; p < n; p++)
                        {
                            ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                            int px = destX + tx;
                            int py = destY + ty;
                            if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                            {
                                int offset = py * rowPitch + (px * 2);
                                ushort dstColor = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                ushort blended = BlendPixelWithAlpha(srcColor, dstColor, type, hasInvAlpha, hasBit2);
                                *(buffer + offset) = (byte)(blended & 0xFF);
                                *(buffer + offset + 1) = (byte)(blended >> 8);
                            }
                            tx++;
                        }
                    }
                }
                // 壓縮格式
                else
                {
                    byte xOffset = *(ptr++);
                    byte yOffset = *(ptr++);
                    byte xxLen = *(ptr++);
                    byte yLen = *(ptr++);

                    for (int ty = 0; ty < yLen; ty++)
                    {
                        int tx = xOffset;
                        byte segCount = *(ptr++);
                        for (int seg = 0; seg < segCount; seg++)
                        {
                            tx += *(ptr++) / 2;
                            int count = *(ptr++);
                            for (int p = 0; p < count; p++)
                            {
                                ushort srcColor = (ushort)(*(ptr++) | (*(ptr++) << 8));
                                int px = destX + tx;
                                int py = destY + ty + yOffset;
                                if (px >= 0 && px < maxWidth && py >= 0 && py < maxHeight)
                                {
                                    int offset = py * rowPitch + (px * 2);
                                    ushort dstColor = (ushort)(*(buffer + offset) | (*(buffer + offset + 1) << 8));
                                    ushort blended = BlendPixelWithAlpha(srcColor, dstColor, type, hasInvAlpha, hasBit2);
                                    *(buffer + offset) = (byte)(blended & 0xFF);
                                    *(buffer + offset + 1) = (byte)(blended >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 混合像素 (支援 bit2 半透明和 bit4/5 inverted alpha)
        /// </summary>
        private static ushort BlendPixelWithAlpha(ushort srcColor, ushort dstColor, byte blockType, bool hasInvAlpha, bool hasBit2)
        {
            if (hasInvAlpha)
            {
                // Inverted alpha: 根據亮度計算 alpha
                byte alpha = CalculateInvertedAlpha(srcColor, blockType);
                if (alpha < 8) return dstColor; // 幾乎透明，保留背景
                ushort renderColor = GetInvertedAlphaRenderColor(srcColor, blockType);
                return BlendRgb555WithAlpha(dstColor, renderColor, alpha);
            }
            else if (hasBit2)
            {
                // bit2: 50% 混合
                return BlendRgb555(srcColor, dstColor);
            }
            return srcColor;
        }

        /// <summary>
        /// 檢查是否為透明色
        /// </summary>
        private static bool IsTransparentColor(ushort color)
        {
            return color == 0 || color == 0x0421 || color == 0x7FFF || color == 0x7FE0;
        }

        /// <summary>
        /// 計算 inverted alpha 值 (用於雲霧/煙霧效果)
        /// bit4 (0x10): 白色不透明，黑色透明 (雲 - map 0)
        /// bit5 (0x20): 黑色不透明，白色透明 (煙 - map 666)
        /// </summary>
        public static byte CalculateInvertedAlpha(ushort color, byte ttype)
        {
            if (IsTransparentColor(color)) return 0;

            ushort color15 = (ushort)(color & 0x7FFF);

            // Extract RGB555 components
            uint r = (uint)((color15 >> 10) & 0x1F);
            uint g = (uint)((color15 >> 5) & 0x1F);
            uint b = (uint)(color15 & 0x1F);

            // Calculate brightness (max of RGB)
            uint brightness = Math.Max(r, Math.Max(g, b));

            if ((ttype & 0x20) != 0)
            {
                // Bit 0x20: Dark = opaque, White = transparent (smoke, map 666)
                return (byte)((31 - brightness) * 255 / 31);
            }
            else if ((ttype & 0x10) != 0)
            {
                // Bit 0x10: White = opaque, Dark = transparent (cloud, map 0)
                return (byte)(brightness * 255 / 31);
            }

            return 255; // Full opacity for normal tiles
        }

        /// <summary>
        /// 取得 inverted alpha 的渲染顏色
        /// </summary>
        public static ushort GetInvertedAlphaRenderColor(ushort color, byte ttype)
        {
            ushort color15 = (ushort)(color & 0x7FFF);
            uint r = (uint)((color15 >> 10) & 0x1F);
            uint g = (uint)((color15 >> 5) & 0x1F);
            uint b = (uint)(color15 & 0x1F);

            // Check if grayscale
            bool isGrayscale = Math.Abs((int)r - (int)g) <= 2 && Math.Abs((int)g - (int)b) <= 2;

            if (isGrayscale)
            {
                if ((ttype & 0x20) != 0)
                    return 0x6318; // Dark gray smoke (map 666)
                else
                    return 0x739C; // Light gray cloud (map 0)
            }
            else
            {
                // Colored effect (blood) - darken original color
                uint darkR = Math.Min(r * 2 / 3, 31);
                uint darkG = Math.Min(g * 2 / 3, 31);
                uint darkB = Math.Min(b * 2 / 3, 31);
                return (ushort)((darkR << 10) | (darkG << 5) | darkB);
            }
        }

        /// <summary>
        /// RGB555 alpha blending
        /// </summary>
        public static ushort BlendRgb555WithAlpha(ushort bg, ushort fg, byte alpha)
        {
            if (alpha == 255) return fg;
            if (alpha == 0) return bg;

            uint invAlpha = (uint)(255 - alpha);

            uint bgR = (uint)((bg >> 10) & 0x1F);
            uint bgG = (uint)((bg >> 5) & 0x1F);
            uint bgB = (uint)(bg & 0x1F);

            uint fgR = (uint)((fg >> 10) & 0x1F);
            uint fgG = (uint)((fg >> 5) & 0x1F);
            uint fgB = (uint)(fg & 0x1F);

            uint outR = (bgR * invAlpha + fgR * alpha) / 255;
            uint outG = (bgG * invAlpha + fgG * alpha) / 255;
            uint outB = (bgB * invAlpha + fgB * alpha) / 255;

            return (ushort)((outR << 10) | (outG << 5) | outB);
        }

        /// <summary>
        /// 檢查 block type 是否使用 inverted alpha
        /// </summary>
        public static bool HasInvertedAlpha(byte ttype)
        {
            return (ttype & 0x30) != 0; // bit4 or bit5
        }

        /// <summary>
        /// RGB555 顏色混合 (50% opacity)
        /// </summary>
        private static ushort BlendRgb555(ushort src, ushort dst)
        {
            // RGB555 格式: 0RRRRRGGGGGBBBBB
            int srcR = (src >> 10) & 0x1F;
            int srcG = (src >> 5) & 0x1F;
            int srcB = src & 0x1F;

            int dstR = (dst >> 10) & 0x1F;
            int dstG = (dst >> 5) & 0x1F;
            int dstB = dst & 0x1F;

            // 50% blend: (src + dst) / 2
            int blendR = (srcR + dstR) >> 1;
            int blendG = (srcG + dstG) >> 1;
            int blendB = (srcB + dstB) >> 1;

            return (ushort)((blendR << 10) | (blendG << 5) | blendB);
        }

        #endregion
    }
}
