using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lin.Helper.Core.Map
{
    /// <summary>
    /// S32 檔案讀取器
    /// </summary>
    public static class S32Reader
    {
        /// <summary>
        /// 解析 S32 檔案
        /// </summary>
        public static S32Data Parse(byte[] data)
        {
            S32Data s32Data = new S32Data();
            s32Data.OriginalFileData = data;

            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                s32Data.Layer1Offset = (int)br.BaseStream.Position;

                // 第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        int id = br.ReadByte();
                        int til = br.ReadUInt16();
                        int nk = br.ReadByte();

                        s32Data.Layer1[y, x] = new S32L1Floor
                        {
                            X = x,
                            Y = y,
                            TileId = til,
                            IndexId = id
                        };

                        if (!s32Data.UsedTiles.ContainsKey(til))
                        {
                            s32Data.UsedTiles[til] = new TileInfo
                            {
                                TileId = til,
                                IndexId = id,
                                UsageCount = 1
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[til].UsageCount++;
                        }
                    }
                }

                s32Data.Layer2Offset = (int)br.BaseStream.Position;

                // 第二層 - X(BYTE), Y(BYTE), IndexId(BYTE), TileId(USHORT), UK(BYTE)
                int layer2Count = br.ReadUInt16();
                for (int i = 0; i < layer2Count; i++)
                {
                    s32Data.Layer2.Add(new S32L2FloorCover
                    {
                        X = br.ReadByte(),
                        Y = br.ReadByte(),
                        IndexId = br.ReadByte(),
                        TileId = br.ReadUInt16(),
                        UK = br.ReadByte()
                    });
                }

                s32Data.Layer3Offset = (int)br.BaseStream.Position;

                // 第三層（地圖屬性）- 64x64
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        s32Data.Layer3[y, x] = new S32L3PassAndArea
                        {
                            Attribute1 = br.ReadInt16(),
                            Attribute2 = br.ReadInt16()
                        };
                    }
                }

                s32Data.Layer4Offset = (int)br.BaseStream.Position;

                // 第四層（物件）
                int layer4GroupCount = br.ReadInt32();
                for (int i = 0; i < layer4GroupCount; i++)
                {
                    int groupId = br.ReadInt16();
                    int blockCount = br.ReadUInt16();

                    for (int j = 0; j < blockCount; j++)
                    {
                        int x = br.ReadByte();
                        int y = br.ReadByte();
                        int layer = br.ReadByte();
                        int indexId = br.ReadByte();
                        int tileId = br.ReadInt16();
                        int uk = br.ReadByte();

                        var objTile = new S32L4Building
                        {
                            GroupId = groupId,
                            X = x,
                            Y = y,
                            Layer = layer,
                            IndexId = indexId,
                            TileId = tileId
                        };

                        s32Data.Layer4.Add(objTile);

                        if (!s32Data.UsedTiles.ContainsKey(tileId))
                        {
                            s32Data.UsedTiles[tileId] = new TileInfo
                            {
                                TileId = tileId,
                                IndexId = indexId,
                                UsageCount = 1
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[tileId].UsageCount++;
                        }
                    }
                }

                s32Data.Layer4EndOffset = (int)br.BaseStream.Position;

                // 讀取第5-8層的原始資料
                int remainingLength = (int)(br.BaseStream.Length - br.BaseStream.Position);
                if (remainingLength > 0)
                {
                    s32Data.Layer5to8Data = br.ReadBytes(remainingLength);
                    ParseLayers5to8(s32Data);
                }
                else
                {
                    s32Data.Layer5to8Data = new byte[0];
                }
            }

            s32Data.CalculateRealBounds();
            return s32Data;
        }

        /// <summary>
        /// 解析第 5-8 層
        /// </summary>
        private static void ParseLayers5to8(S32Data s32Data)
        {
            using (var layerStream = new MemoryStream(s32Data.Layer5to8Data))
            using (var layerReader = new BinaryReader(layerStream))
            {
                try
                {
                    // 第五層 - 事件
                    if (layerStream.Position + 4 <= layerStream.Length)
                    {
                        int lv5Count = layerReader.ReadInt32();
                        for (int i = 0; i < lv5Count && layerStream.Position + 5 <= layerStream.Length; i++)
                        {
                            s32Data.Layer5.Add(new S32L5Opacity
                            {
                                X = layerReader.ReadByte(),
                                Y = layerReader.ReadByte(),
                                ObjectIndex = layerReader.ReadUInt16(),
                                Type = layerReader.ReadByte()
                            });
                        }
                    }

                    // 第六層 - 使用的 til
                    if (layerStream.Position + 4 <= layerStream.Length)
                    {
                        int lv6Count = layerReader.ReadInt32();
                        for (int i = 0; i < lv6Count && layerStream.Position + 4 <= layerStream.Length; i++)
                        {
                            int til = layerReader.ReadInt32();
                            s32Data.Layer6.Add(new S32L6TileRef(til));
                        }
                    }

                    // 第七層 - 傳送點、入口點
                    if (layerStream.Position + 2 <= layerStream.Length)
                    {
                        int lv7Count = layerReader.ReadUInt16();
                        for (int i = 0; i < lv7Count && layerStream.Position + 1 <= layerStream.Length; i++)
                        {
                            byte len = layerReader.ReadByte();
                            if (layerStream.Position + len + 8 > layerStream.Length) break;

                            string name = Encoding.Default.GetString(layerReader.ReadBytes(len));
                            s32Data.Layer7.Add(new S32L7ExitPortal
                            {
                                Name = name,
                                X = layerReader.ReadByte(),
                                Y = layerReader.ReadByte(),
                                TargetMapId = layerReader.ReadUInt16(),
                                PortalId = layerReader.ReadInt32()
                            });
                        }
                    }

                    // 第八層 - 特效、裝飾品
                    if (layerStream.Position + 2 <= layerStream.Length)
                    {
                        ushort lv8Num = layerReader.ReadUInt16();
                        bool hasExtendedData = (lv8Num >= 0x8000);
                        if (hasExtendedData)
                        {
                            lv8Num = (ushort)(lv8Num & 0x7FFF);
                        }
                        s32Data.Layer8HasExtendedData = hasExtendedData;

                        int itemSize = hasExtendedData ? 10 : 6;
                        for (int i = 0; i < lv8Num && layerStream.Position + itemSize <= layerStream.Length; i++)
                        {
                            var item = new S32L8SPREffect
                            {
                                SprId = layerReader.ReadUInt16(),
                                X = layerReader.ReadUInt16(),
                                Y = layerReader.ReadUInt16(),
                                ExtendedData = hasExtendedData ? layerReader.ReadInt32() : 0
                            };
                            s32Data.Layer8.Add(item);
                        }
                    }
                }
                catch (EndOfStreamException)
                {
                    // 忽略讀取超出範圍的錯誤
                }
            }
        }

        /// <summary>
        /// 從檔案載入並解析 S32
        /// </summary>
        public static S32Data ParseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            S32Data s32Data = Parse(data);
            s32Data.FilePath = filePath;
            return s32Data;
        }
    }
}
