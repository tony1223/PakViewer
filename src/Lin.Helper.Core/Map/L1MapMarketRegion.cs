using System;
using System.IO;

namespace Lin.Helper.Core.Map
{
    /// <summary>
    /// Lineage MapRegion 檔案讀寫器 (MarketRegion, SafeZone 等)
    ///
    /// 格式：
    /// - 每 cell: uint16 (2 bytes), little-endian
    /// - 檔案大小: 16384 bytes = 8192 cells
    /// - 網格: 128 cells × 64 rows
    /// - 2x 水平解析度 (每世界 X = 2 cells)
    /// - 覆蓋範圍: 64 世界 X × 64 世界 Y
    ///
    /// 檔名格式: {blockX:04x}{blockY:04x}.{regionType}
    /// 例如: 80008001.MarketRegion
    /// </summary>
    public class L1MapMarketRegion
    {
        #region 常數

        /// <summary>每個 cell 的大小 (bytes)</summary>
        public const int CellSize = 2;

        /// <summary>網格寬度 (cells per row)</summary>
        public const int GridWidth = 128;

        /// <summary>網格高度 (rows)</summary>
        public const int GridHeight = 64;

        /// <summary>檔案大小</summary>
        public const int FileSize = GridWidth * GridHeight * CellSize;  // 16384 bytes

        /// <summary>世界座標寬度 (因為 2x 解析度)</summary>
        public const int WorldWidth = 64;

        /// <summary>世界座標高度</summary>
        public const int WorldHeight = 64;

        /// <summary>Y 軸基準值（與 S32 相同，使用 0x7fff）</summary>
        public const int BaseY = 0x7fff;  // 32767

        #endregion

        #region 屬性

        /// <summary>檔案路徑</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Block X 座標</summary>
        public int BlockX { get; set; }

        /// <summary>Block Y 座標</summary>
        public int BlockY { get; set; }

        /// <summary>區域類型 (MarketRegion, SafeZone 等)</summary>
        public string RegionType { get; set; } = "MarketRegion";

        /// <summary>Cell 資料</summary>
        public ushort[,] Cells { get; private set; } = new ushort[GridHeight, GridWidth];

        /// <summary>是否已修改</summary>
        public bool Modified { get; private set; }

        #endregion

        #region 建構子

        public L1MapMarketRegion() { }

        public L1MapMarketRegion(int blockX, int blockY, string regionType = "MarketRegion")
        {
            BlockX = blockX;
            BlockY = blockY;
            RegionType = regionType;
        }

        #endregion

        #region 座標計算

        /// <summary>
        /// 取得此 block 覆蓋的遊戲座標範圍
        /// 使用與 S32 相同的公式：nLinEndX = (nBlockX - 0x7fff) * 64 + 0x7fff, nLinBeginX = nLinEndX - 63
        /// </summary>
        public (int MinX, int MaxX, int MinY, int MaxY) GetWorldBounds()
        {
            // X 座標計算（與 S32 L1MapSeg 公式相同）
            int endX = (BlockX - 0x7fff) * WorldWidth + 0x7fff;
            int startX = endX - WorldWidth + 1;

            // Y 座標計算
            int endY = (BlockY - BaseY) * WorldHeight + BaseY;
            int startY = endY - WorldHeight + 1;

            return (startX, startX + WorldWidth - 1, startY, startY + WorldHeight - 1);
        }

        /// <summary>
        /// 世界座標轉換為 cell 索引
        /// </summary>
        /// <returns>成功返回 (row, col)，超出範圍返回 null</returns>
        public (int Row, int Col)? WorldToCell(int worldX, int worldY)
        {
            var bounds = GetWorldBounds();

            if (worldX < bounds.MinX || worldX > bounds.MaxX ||
                worldY < bounds.MinY || worldY > bounds.MaxY)
            {
                return null;
            }

            // 2x 水平解析度
            int col = (worldX - bounds.MinX) * 2;
            int row = worldY - bounds.MinY;

            return (row, col);
        }

        /// <summary>
        /// 計算給定遊戲座標應該在哪個 block
        /// 反向公式：從 nLinBeginX 求 nBlockX
        /// nLinBeginX = (nBlockX - 0x7fff) * 64 + 0x7fff - 63
        /// nBlockX = (nLinBeginX - 0x7fff + 63) / 64 + 0x7fff
        /// </summary>
        public static (int BlockX, int BlockY) GetBlockForWorldCoord(int gameX, int gameY)
        {
            // 找到包含此座標的 block 的起始座標，再反算 blockX
            // nLinEndX = nLinBeginX + 63，nBlockX = (nLinEndX - 0x7fff) / 64 + 0x7fff
            int endX = ((gameX - 0x7fff + 63) / WorldWidth) * WorldWidth + 0x7fff;
            int blockX = (endX - 0x7fff) / WorldWidth + 0x7fff;

            int endY = ((gameY - BaseY + 63) / WorldHeight) * WorldHeight + BaseY;
            int blockY = (endY - BaseY) / WorldHeight + BaseY;

            return (blockX, blockY);
        }

        #endregion

        #region Cell 存取

        /// <summary>
        /// 讀取 cell 值
        /// </summary>
        public ushort? GetCellValue(int row, int col)
        {
            if (row < 0 || row >= GridHeight || col < 0 || col >= GridWidth)
                return null;

            return Cells[row, col];
        }

        /// <summary>
        /// 設定 cell 值
        /// </summary>
        public bool SetCellValue(int row, int col, ushort value)
        {
            if (row < 0 || row >= GridHeight || col < 0 || col >= GridWidth)
                return false;

            Cells[row, col] = value;
            Modified = true;
            return true;
        }

        /// <summary>
        /// 檢查世界座標是否在區域內
        /// </summary>
        /// <returns>在區域內返回 true，不在返回 false，超出範圍返回 null</returns>
        public bool? IsInRegion(int worldX, int worldY)
        {
            var cell = WorldToCell(worldX, worldY);
            if (!cell.HasValue)
                return null;

            return Cells[cell.Value.Row, cell.Value.Col] != 0;
        }

        /// <summary>
        /// 設定世界座標的區域狀態
        /// </summary>
        public bool SetInRegion(int worldX, int worldY, bool inRegion)
        {
            var cell = WorldToCell(worldX, worldY);
            if (!cell.HasValue)
                return false;

            ushort value = inRegion ? (ushort)1 : (ushort)0;

            // 因為 2x 解析度，每個世界座標對應 2 個 cells
            SetCellValue(cell.Value.Row, cell.Value.Col, value);
            if (cell.Value.Col + 1 < GridWidth)
            {
                SetCellValue(cell.Value.Row, cell.Value.Col + 1, value);
            }

            return true;
        }

        /// <summary>
        /// 統計區域內的 cell 數量
        /// </summary>
        public int CountInRegion()
        {
            int count = 0;
            for (int row = 0; row < GridHeight; row++)
            {
                for (int col = 0; col < GridWidth; col++)
                {
                    if (Cells[row, col] != 0)
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 清除所有 cell (設為 0)
        /// </summary>
        public void Clear()
        {
            Array.Clear(Cells, 0, Cells.Length);
            Modified = true;
        }

        #endregion

        #region 檔案操作

        /// <summary>
        /// 從檔名解析 block 座標
        /// 格式: {blockX:04x}{blockY:04x}.{regionType}
        /// </summary>
        public static (int BlockX, int BlockY, string RegionType)? ParseFilename(string filePath)
        {
            string filename = Path.GetFileName(filePath);
            if (filename.Length < 9) // 至少 8 hex chars + "."
                return null;

            // 嘗試解析 8 個 hex 字元
            if (!int.TryParse(filename.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out int blockX))
                return null;
            if (!int.TryParse(filename.Substring(4, 4), System.Globalization.NumberStyles.HexNumber, null, out int blockY))
                return null;

            int dotIndex = filename.IndexOf('.');
            if (dotIndex < 0)
                return null;

            string regionType = filename.Substring(dotIndex + 1);
            return (blockX, blockY, regionType);
        }

        /// <summary>
        /// 建構檔案名稱
        /// </summary>
        public string BuildFilename()
        {
            return $"{BlockX:x4}{BlockY:x4}.{RegionType}";
        }

        /// <summary>
        /// 從檔案載入
        /// </summary>
        public static L1MapMarketRegion Load(string filePath)
        {
            var parsed = ParseFilename(filePath);
            if (!parsed.HasValue)
                throw new ArgumentException($"Invalid filename format: {Path.GetFileName(filePath)}");

            var region = new L1MapMarketRegion
            {
                FilePath = filePath,
                BlockX = parsed.Value.BlockX,
                BlockY = parsed.Value.BlockY,
                RegionType = parsed.Value.RegionType
            };

            byte[] data = File.ReadAllBytes(filePath);
            region.LoadFromBytes(data);

            return region;
        }

        /// <summary>
        /// 從 byte 陣列載入
        /// </summary>
        public void LoadFromBytes(byte[] data)
        {
            if (data.Length < FileSize)
            {
                // 警告：檔案大小不足，使用可用資料
            }

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            for (int row = 0; row < GridHeight; row++)
            {
                for (int col = 0; col < GridWidth; col++)
                {
                    if (ms.Position + CellSize <= ms.Length)
                    {
                        Cells[row, col] = br.ReadUInt16();
                    }
                    else
                    {
                        Cells[row, col] = 0;
                    }
                }
            }

            Modified = false;
        }

        /// <summary>
        /// 儲存到檔案
        /// </summary>
        public void Save(string filePath = null)
        {
            string targetPath = filePath ?? FilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No file path specified");

            byte[] data = ToBytes();

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(targetPath, data);
            FilePath = targetPath;
            Modified = false;
        }

        /// <summary>
        /// 轉換為 byte 陣列
        /// </summary>
        public byte[] ToBytes()
        {
            using var ms = new MemoryStream(FileSize);
            using var bw = new BinaryWriter(ms);

            for (int row = 0; row < GridHeight; row++)
            {
                for (int col = 0; col < GridWidth; col++)
                {
                    bw.Write(Cells[row, col]);
                }
            }

            return ms.ToArray();
        }

        #endregion

        #region 輔助方法

        /// <summary>
        /// 建立新的空白 region 檔案
        /// </summary>
        public static L1MapMarketRegion Create(int blockX, int blockY, string regionType = "MarketRegion")
        {
            return new L1MapMarketRegion(blockX, blockY, regionType);
        }

        /// <summary>
        /// 檢查檔案是否存在
        /// </summary>
        public static bool Exists(string mapDir, int mapId, int blockX, int blockY, string regionType)
        {
            string filename = $"{blockX:x4}{blockY:x4}.{regionType}";
            string filePath = Path.Combine(mapDir, mapId.ToString(), filename);
            return File.Exists(filePath);
        }

        #endregion
    }
}
