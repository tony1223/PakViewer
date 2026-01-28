using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lin.Helper.Core.Map
{
    /// <summary>
    /// S32 檔案寫入器
    /// </summary>
    public static class S32Writer
    {
        /// <summary>
        /// 將 S32Data 寫入檔案
        /// </summary>
        public static void Write(S32Data s32Data, string filePath)
        {
            byte[] data = ToBytes(s32Data);
            File.WriteAllBytes(filePath, data);
        }

        /// <summary>
        /// 將 S32Data 轉換為 byte 陣列
        /// </summary>
        public static byte[] ToBytes(S32Data s32Data)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // 第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null)
                        {
                            bw.Write((byte)cell.IndexId);
                            bw.Write((ushort)cell.TileId);
                            bw.Write((byte)0); // nk
                        }
                        else
                        {
                            bw.Write((byte)0);
                            bw.Write((ushort)0);
                            bw.Write((byte)0);
                        }
                    }
                }

                // 第二層
                bw.Write((ushort)s32Data.Layer2.Count);
                foreach (var item in s32Data.Layer2)
                {
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.IndexId);
                    bw.Write(item.TileId);
                    bw.Write(item.UK);
                }

                // 第三層（地圖屬性）- 64x64
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        var attr = s32Data.Layer3[y, x];
                        if (attr != null)
                        {
                            bw.Write(attr.Attribute1);
                            bw.Write(attr.Attribute2);
                        }
                        else
                        {
                            bw.Write((short)0);
                            bw.Write((short)0);
                        }
                    }
                }

                // 第四層（物件）
                var groupedObjects = s32Data.Layer4
                    .GroupBy(o => o.GroupId)
                    .OrderBy(g => g.Key)
                    .ToList();

                bw.Write(groupedObjects.Count);

                foreach (var group in groupedObjects)
                {
                    var objects = group.OrderBy(o => o.Layer).ToList();
                    bw.Write((short)group.Key);
                    bw.Write((ushort)objects.Count);

                    foreach (var obj in objects)
                    {
                        bw.Write((byte)obj.X);
                        bw.Write((byte)obj.Y);
                        bw.Write((byte)obj.Layer);
                        bw.Write((byte)obj.IndexId);
                        bw.Write((short)obj.TileId);
                        bw.Write((byte)0); // uk
                    }
                }

                // 第五層 - 事件
                bw.Write(s32Data.Layer5.Count);
                foreach (var item in s32Data.Layer5)
                {
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.ObjectIndex);
                    bw.Write(item.Type);
                }

                // 第六層 - 使用的 til（重新計算並排序）
                HashSet<int> usedTileIds = new HashSet<int>();

                // 從 Layer1 收集
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            usedTileIds.Add(cell.TileId);
                        }
                    }
                }

                // 從 Layer4 收集
                foreach (var obj in s32Data.Layer4)
                {
                    if (obj.TileId > 0)
                    {
                        usedTileIds.Add(obj.TileId);
                    }
                }

                // 排序後寫入
                List<int> sortedTileIds = usedTileIds.OrderBy(id => id).ToList();
                bw.Write(sortedTileIds.Count);
                foreach (var tilId in sortedTileIds)
                {
                    bw.Write(tilId);
                }

                // 更新記憶體中的 Layer6 資料
                s32Data.Layer6.Clear();
                s32Data.Layer6.AddRange(sortedTileIds.Select(id => new S32L6TileRef(id)));

                // 第七層 - 傳送點、入口點
                bw.Write((ushort)s32Data.Layer7.Count);
                foreach (var item in s32Data.Layer7)
                {
                    byte[] nameBytes = Encoding.Default.GetBytes(item.Name ?? "");
                    bw.Write((byte)nameBytes.Length);
                    bw.Write(nameBytes);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.TargetMapId);
                    bw.Write(item.PortalId);
                }

                // 第八層 - 特效、裝飾品
                ushort lv8Count = (ushort)s32Data.Layer8.Count;
                if (s32Data.Layer8HasExtendedData)
                {
                    lv8Count |= 0x8000;
                }
                bw.Write(lv8Count);
                foreach (var item in s32Data.Layer8)
                {
                    bw.Write(item.SprId);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    if (s32Data.Layer8HasExtendedData)
                    {
                        bw.Write(item.ExtendedData);
                    }
                }

                return ms.ToArray();
            }
        }
    }
}
