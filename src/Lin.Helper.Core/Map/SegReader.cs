using System;
using System.IO;

namespace Lin.Helper.Core.Map
{
    /// <summary>
    /// SEG 檔案讀取器 - 解析舊版 .seg 格式地圖檔案
    /// 格式：
    /// 1. Layer1: 64×128 × 2 bytes (IndexId 1 byte + TileId 1 byte)
    /// 2. Layer2: count (WORD) + items (4 bytes each)
    /// 3. Layer3: 64×64 × 2 bytes (地圖屬性)
    /// 4. Layer4: count (DWORD) + groups (每物件 5 bytes)
    /// </summary>
    public static class SegReader
    {
        /// <summary>
        /// 解析 SEG 檔案並轉換為 S32Data 結構
        /// </summary>
        public static S32Data Parse(byte[] data)
        {
            if (data == null || data.Length < 16384) // Layer1 最小大小 64*128*2
                return null;

            S32Data s32Data = new S32Data();
            s32Data.OriginalFileData = data;

            try
            {
                using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
                {
                    s32Data.Layer1Offset = (int)br.BaseStream.Position;

                    // 第一層（地板）- 64x128，每格 2 bytes (IndexId + TileId)
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            byte indexId = br.ReadByte();
                            byte tileId = br.ReadByte();

                            s32Data.Layer1[y, x] = new S32L1Floor
                            {
                                X = x,
                                Y = y,
                                TileId = tileId,
                                IndexId = indexId
                            };

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

                    s32Data.Layer2Offset = (int)br.BaseStream.Position;

                    // 第二層 - count (WORD) + items (4 bytes each: X, Y, TileId(2))
                    if (br.BaseStream.Position + 2 <= br.BaseStream.Length)
                    {
                        int layer2Count = br.ReadUInt16();

                        for (int i = 0; i < layer2Count && br.BaseStream.Position + 4 <= br.BaseStream.Length; i++)
                        {
                            byte x = br.ReadByte();
                            byte y = br.ReadByte();
                            ushort tileData = br.ReadUInt16();

                            // SEG 格式: TileId 在低 8 位, IndexId 在高 8 位
                            byte tileId = (byte)(tileData & 0xFF);
                            byte indexId = (byte)(tileData >> 8);

                            s32Data.Layer2.Add(new S32L2FloorCover
                            {
                                X = x,
                                Y = y,
                                IndexId = indexId,
                                TileId = tileId,
                                UK = 0
                            });
                        }
                    }

                    s32Data.Layer3Offset = (int)br.BaseStream.Position;

                    // 第三層（地圖屬性）- 64x64，每格 2 bytes
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            if (br.BaseStream.Position + 2 <= br.BaseStream.Length)
                            {
                                byte attr1 = br.ReadByte();
                                byte attr2 = br.ReadByte();
                                s32Data.Layer3[y, x] = new S32L3PassAndArea
                                {
                                    Attribute1 = attr1,
                                    Attribute2 = attr2
                                };
                            }
                            else
                            {
                                s32Data.Layer3[y, x] = new S32L3PassAndArea
                                {
                                    Attribute1 = 0,
                                    Attribute2 = 0
                                };
                            }
                        }
                    }

                    s32Data.Layer4Offset = (int)br.BaseStream.Position;

                    // 第四層（物件）
                    if (br.BaseStream.Position + 4 <= br.BaseStream.Length)
                    {
                        int layer4GroupCount = br.ReadInt32();

                        for (int i = 0; i < layer4GroupCount && br.BaseStream.Position < br.BaseStream.Length; i++)
                        {
                            if (br.BaseStream.Position + 4 > br.BaseStream.Length) break;

                            int groupId = br.ReadInt16();
                            int blockCount = br.ReadUInt16();

                            for (int j = 0; j < blockCount && br.BaseStream.Position < br.BaseStream.Length; j++)
                            {
                                if (br.BaseStream.Position + 5 > br.BaseStream.Length) break;

                                // SEG 格式: x(1), y(1), layer(1), indexId(1), tileId(1)
                                int x = br.ReadByte();
                                int y = br.ReadByte();
                                int layer = br.ReadByte();
                                int indexId = br.ReadByte();
                                int tileId = br.ReadByte();

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
                    }

                    s32Data.Layer4EndOffset = (int)br.BaseStream.Position;

                    // 讀取剩餘資料作為 Layer5-8
                    int remainingLength = (int)(br.BaseStream.Length - br.BaseStream.Position);
                    if (remainingLength > 0)
                    {
                        s32Data.Layer5to8Data = br.ReadBytes(remainingLength);
                    }
                    else
                    {
                        s32Data.Layer5to8Data = new byte[0];
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return s32Data;
        }

        /// <summary>
        /// 從檔案解析 SEG
        /// </summary>
        public static S32Data ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            byte[] data = File.ReadAllBytes(filePath);
            var result = Parse(data);
            if (result != null)
            {
                result.FilePath = filePath;
            }
            return result;
        }
    }
}
