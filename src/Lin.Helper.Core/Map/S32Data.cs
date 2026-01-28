using System;
using System.Collections.Generic;

namespace Lin.Helper.Core.Map
{
    #region 地圖區塊資訊

    /// <summary>
    /// 地圖區塊資訊
    /// </summary>
    public class MapSegInfo
    {
        public MapSegInfo() { }

        public MapSegInfo(int blockX, int blockY, bool isS32)
        {
            IsS32 = isS32;
            BlockX = blockX;
            BlockY = blockY;
            LinEndX = (blockX - 0x7fff) * 64 + 0x7fff;
            LinEndY = (blockY - 0x7fff) * 64 + 0x7fff;
            LinBeginX = LinEndX - 64 + 1;
            LinBeginY = LinEndY - 64 + 1;
        }

        /// <summary>X軸的起點座標</summary>
        public int LinBeginX { get; set; }
        /// <summary>Y軸的起點座標</summary>
        public int LinBeginY { get; set; }
        /// <summary>X軸的終點座標</summary>
        public int LinEndX { get; set; }
        /// <summary>Y軸的終點座標</summary>
        public int LinEndY { get; set; }
        /// <summary>地圖檔區塊座標X</summary>
        public int BlockX { get; set; }
        /// <summary>地圖檔區塊座標Y</summary>
        public int BlockY { get; set; }
        /// <summary>是否為.s32檔</summary>
        public bool IsS32 { get; set; }
    }

    #endregion

    #region S32 主資料結構

    /// <summary>
    /// S32 資料結構
    /// </summary>
    public class S32Data
    {
        /// <summary>標準寬度 (Layer1 本地座標)</summary>
        public const int StandardWidth = 128;

        /// <summary>標準高度 (Layer1 本地座標)</summary>
        public const int StandardHeight = 64;

        /// <summary>第一層（地板）- 64x128</summary>
        public S32L1Floor[,] Layer1 { get; set; } = new S32L1Floor[64, 128];

        /// <summary>第二層（覆蓋物）</summary>
        public List<S32L2FloorCover> Layer2 { get; set; } = new List<S32L2FloorCover>();

        /// <summary>第三層（地圖屬性）- 64x64</summary>
        public S32L3PassAndArea[,] Layer3 { get; set; } = new S32L3PassAndArea[64, 64];

        /// <summary>第四層（建築物/物件）</summary>
        public List<S32L4Building> Layer4 { get; set; } = new List<S32L4Building>();

        /// <summary>使用的所有 tile（不重複）</summary>
        public Dictionary<int, TileInfo> UsedTiles { get; set; } = new Dictionary<int, TileInfo>();

        /// <summary>保存原始文件內容</summary>
        public byte[] OriginalFileData { get; set; } = Array.Empty<byte>();

        /// <summary>第一層在文件中的偏移</summary>
        public int Layer1Offset { get; set; }
        /// <summary>第二層在文件中的偏移</summary>
        public int Layer2Offset { get; set; }
        /// <summary>第三層在文件中的偏移</summary>
        public int Layer3Offset { get; set; }
        /// <summary>第四層在文件中的偏移</summary>
        public int Layer4Offset { get; set; }
        /// <summary>第四層結束位置</summary>
        public int Layer4EndOffset { get; set; }

        /// <summary>第5-8層的原始資料</summary>
        public byte[] Layer5to8Data { get; set; } = Array.Empty<byte>();

        /// <summary>第5層 - 透明度/事件</summary>
        public List<S32L5Opacity> Layer5 { get; set; } = new List<S32L5Opacity>();

        /// <summary>第6層 - 使用的 Tile 參照</summary>
        public List<S32L6TileRef> Layer6 { get; set; } = new List<S32L6TileRef>();

        /// <summary>第7層 - 傳送點、入口點</summary>
        public List<S32L7ExitPortal> Layer7 { get; set; } = new List<S32L7ExitPortal>();

        /// <summary>第8層 - SPR 特效</summary>
        public List<S32L8SPREffect> Layer8 { get; set; } = new List<S32L8SPREffect>();

        /// <summary>第8層擴展資訊</summary>
        public bool Layer8HasExtendedData { get; set; } = false;

        /// <summary>檔案路徑</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>地圖區塊資訊</summary>
        public MapSegInfo SegInfo { get; set; }

        /// <summary>實際資料寬度 - Layer1 本地座標系統</summary>
        public int RealLocalWidth { get; set; } = StandardWidth;

        /// <summary>實際資料高度 - Layer1 本地座標系統</summary>
        public int RealLocalHeight { get; set; } = StandardHeight;

        /// <summary>實際資料寬度 - 遊戲座標系統</summary>
        public int RealGameWidth => (RealLocalWidth + 1) / 2;

        /// <summary>實際資料高度 - 遊戲座標系統</summary>
        public int RealGameHeight => RealLocalHeight;

        /// <summary>實際遊戲座標終點 X</summary>
        public int RealGameEndX => SegInfo != null ? SegInfo.LinBeginX + RealGameWidth - 1 : -1;

        /// <summary>實際遊戲座標終點 Y</summary>
        public int RealGameEndY => SegInfo != null ? SegInfo.LinBeginY + RealGameHeight - 1 : -1;

        /// <summary>
        /// 檢查遊戲座標是否在實際範圍內
        /// </summary>
        public bool ContainsGameCoord(int gameX, int gameY)
        {
            if (SegInfo == null) return false;
            return gameX >= SegInfo.LinBeginX && gameX <= RealGameEndX &&
                   gameY >= SegInfo.LinBeginY && gameY <= RealGameEndY;
        }

        /// <summary>
        /// 檢查本地座標是否在實際範圍內
        /// </summary>
        public bool ContainsLocalCoord(int localX, int localY)
        {
            return localX >= 0 && localX < RealLocalWidth &&
                   localY >= 0 && localY < RealLocalHeight;
        }

        /// <summary>
        /// 計算實際邊界 (根據各層資料的最大 X, Y)
        /// </summary>
        public void CalculateRealBounds()
        {
            int maxX = StandardWidth - 1;
            int maxY = StandardHeight - 1;

            foreach (var item in Layer2)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            foreach (var item in Layer4)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            foreach (var item in Layer5)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            foreach (var item in Layer8)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            RealLocalWidth = maxX + 1;
            RealLocalHeight = maxY + 1;
        }
    }

    #endregion

    #region Layer 1 - 地板 (S32L1Floor)

    /// <summary>
    /// Layer 1 - 地板磚資料
    /// </summary>
    public class S32L1Floor
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int TileId { get; set; }
        public int IndexId { get; set; }
    }

    /// <summary>
    /// 格子資料 (相容別名)
    /// </summary>
    public class TileCell : S32L1Floor { }

    #endregion

    #region Layer 2 - 覆蓋物 (S32L2FloorCover)

    /// <summary>
    /// Layer 2 - 覆蓋物/裝飾
    /// Format: X(BYTE), Y(BYTE), IndexId(BYTE), TileId(USHORT), UK(BYTE)
    /// </summary>
    public class S32L2FloorCover
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte IndexId { get; set; }
        public ushort TileId { get; set; }
        public byte UK { get; set; }
    }

    /// <summary>
    /// 第二層項目 (相容別名)
    /// </summary>
    public class Layer2Item : S32L2FloorCover { }

    #endregion

    #region Layer 3 - 通行與區域屬性 (S32L3PassAndArea)

    /// <summary>
    /// Layer 3 - 通行與區域屬性
    /// </summary>
    public class S32L3PassAndArea
    {
        public short Attribute1 { get; set; }
        public short Attribute2 { get; set; }
    }

    /// <summary>
    /// 地圖屬性 (相容別名)
    /// </summary>
    public class MapAttribute : S32L3PassAndArea { }

    #endregion

    #region Layer 4 - 建築物/物件 (S32L4Building)

    /// <summary>
    /// Layer 4 - 建築物/物件
    /// </summary>
    public class S32L4Building
    {
        public int GroupId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Layer { get; set; }
        public int IndexId { get; set; }
        public int TileId { get; set; }
    }

    /// <summary>
    /// 物件 Tile (相容別名)
    /// </summary>
    public class ObjectTile : S32L4Building { }

    #endregion

    #region Layer 5 - 透明度/事件 (S32L5Opacity)

    /// <summary>
    /// Layer 5 - 透明度/事件觸發
    /// </summary>
    public class S32L5Opacity
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort ObjectIndex { get; set; }
        public byte Type { get; set; }
    }

    /// <summary>
    /// 第五層項目 (相容別名)
    /// </summary>
    public class Layer5Item : S32L5Opacity { }

    #endregion

    #region Layer 6 - Tile 參照 (S32L6TileRef)

    /// <summary>
    /// Layer 6 - 使用的 Tile 參照
    /// </summary>
    public class S32L6TileRef
    {
        public int TileId { get; set; }

        public S32L6TileRef() { }
        public S32L6TileRef(int tileId) => TileId = tileId;

        public static implicit operator int(S32L6TileRef t) => t.TileId;
        public static implicit operator S32L6TileRef(int id) => new S32L6TileRef(id);
    }

    #endregion

    #region Layer 7 - 傳送點 (S32L7ExitPortal)

    /// <summary>
    /// Layer 7 - 傳送點/入口點
    /// </summary>
    public class S32L7ExitPortal
    {
        public string Name { get; set; } = string.Empty;
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort TargetMapId { get; set; }
        public int PortalId { get; set; }
    }

    /// <summary>
    /// 第七層項目 (相容別名)
    /// </summary>
    public class Layer7Item : S32L7ExitPortal { }

    #endregion

    #region Layer 8 - SPR 特效 (S32L8SPREffect)

    /// <summary>
    /// Layer 8 - SPR 特效/裝飾品
    /// </summary>
    public class S32L8SPREffect
    {
        public ushort SprId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public int ExtendedData { get; set; }
    }

    /// <summary>
    /// 第八層項目 (相容別名)
    /// </summary>
    public class Layer8Item : S32L8SPREffect { }

    #endregion

    #region 輔助類型

    /// <summary>
    /// Tile 資訊
    /// </summary>
    public class TileInfo
    {
        public int TileId { get; set; }
        public int IndexId { get; set; }
        public int UsageCount { get; set; }
    }

    #endregion
}
