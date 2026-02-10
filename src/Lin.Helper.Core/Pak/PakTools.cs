using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Lin.Helper.Core.Pak
{
    /// <summary>
    /// Lineage 1 PAK 檔案加密/解密工具
    /// </summary>
    public static class PakTools
    {
        private static byte[] _map1;
        private static byte[] _map2;
        private static byte[] _map3;
        private static byte[] _map4;
        private static byte[] _map5;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化加密表 (自動從內嵌資源或外部載入)
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // 嘗試從內嵌資源載入
            var assembly = Assembly.GetExecutingAssembly();
            _map1 = LoadEmbeddedResource(assembly, "Lin.Helper.Core.Resources.Map1.bin");
            _map2 = LoadEmbeddedResource(assembly, "Lin.Helper.Core.Resources.Map2.bin");
            _map3 = LoadEmbeddedResource(assembly, "Lin.Helper.Core.Resources.Map3.bin");
            _map4 = LoadEmbeddedResource(assembly, "Lin.Helper.Core.Resources.Map4.bin");
            _map5 = LoadEmbeddedResource(assembly, "Lin.Helper.Core.Resources.Map5.bin");

            _initialized = _map1 != null && _map2 != null && _map3 != null && _map4 != null && _map5 != null;
        }

        /// <summary>
        /// 手動設定加密表 (用於從外部來源載入)
        /// </summary>
        public static void SetMaps(byte[] map1, byte[] map2, byte[] map3, byte[] map4, byte[] map5)
        {
            _map1 = map1 ?? throw new ArgumentNullException(nameof(map1));
            _map2 = map2 ?? throw new ArgumentNullException(nameof(map2));
            _map3 = map3 ?? throw new ArgumentNullException(nameof(map3));
            _map4 = map4 ?? throw new ArgumentNullException(nameof(map4));
            _map5 = map5 ?? throw new ArgumentNullException(nameof(map5));
            _initialized = true;
        }

        private static byte[] LoadEmbeddedResource(Assembly assembly, string resourceName)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                byte[] data = new byte[stream.Length];
                stream.ReadExactly(data, 0, data.Length);
                return data;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
                if (!_initialized)
                {
                    throw new InvalidOperationException(
                        "PakTools maps not initialized. Call SetMaps() with encryption tables.");
                }
            }
        }

        /// <summary>
        /// 加密資料
        /// </summary>
        public static byte[] Encode(byte[] src, int index)
        {
            return Encode(src, index, null);
        }

        /// <summary>
        /// 加密資料 (支援進度回報)
        /// </summary>
        public static byte[] Encode(byte[] src, int index, IProgress<int> progress)
        {
            return Coder(src, index, true, progress);
        }

        /// <summary>
        /// 解密資料
        /// </summary>
        public static byte[] Decode(byte[] src, int index)
        {
            return Decode(src, index, null);
        }

        /// <summary>
        /// 解密資料 (支援進度回報)
        /// </summary>
        public static byte[] Decode(byte[] src, int index, IProgress<int> progress)
        {
            return Coder(src, index, false, progress);
        }

        /// <summary>
        /// 解碼索引檔的第一筆記錄
        /// </summary>
        public static IndexRecord DecodeIndexFirstRecord(byte[] src)
        {
            byte[] src1 = new byte[36];
            Array.Copy(src, src1, src1.Length);
            return new IndexRecord(Decode(src1, 4), 0);
        }

        private static byte[] Coder(byte[] src, int index, bool isEncode, IProgress<int> progress)
        {
            EnsureInitialized();

            byte[] result = new byte[src.Length - index];
            int totalLength = src.Length;

            if (result.Length >= 8)
            {
                byte[] block = new byte[8];
                int blockCount = result.Length / 8;
                int destIndex = 0;

                for (int i = 0; i < blockCount; i++)
                {
                    Array.Copy(src, destIndex + index, block, 0, 8);
                    byte[] processed = isEncode ? EncodeBlock(block) : DecodeBlock(block);
                    Array.Copy(processed, 0, result, destIndex, 8);
                    destIndex += 8;

                    // 每處理 1000 個區塊回報一次進度
                    if (progress != null && i % 1000 == 0)
                    {
                        progress.Report(destIndex * 100 / totalLength);
                    }
                }
            }

            // 處理剩餘不足 8 bytes 的部分
            int remaining = result.Length % 8;
            if (remaining > 0)
            {
                int destIndex = result.Length - remaining;
                Array.Copy(src, destIndex + index, result, destIndex, remaining);
            }

            progress?.Report(100);
            return result;
        }

        private static byte[] DecodeBlock(byte[] src)
        {
            byte[][] rounds = new byte[17][];
            rounds[0] = ApplyPermutation(src, _map1);

            int roundKey = 15;
            for (int i = 0; i < 16; i++)
            {
                rounds[i + 1] = ApplyRound(roundKey, rounds[i]);
                roundKey--;
            }

            return ApplyPermutation(new byte[8]
            {
                rounds[16][4], rounds[16][5], rounds[16][6], rounds[16][7],
                rounds[16][0], rounds[16][1], rounds[16][2], rounds[16][3]
            }, _map2);
        }

        private static byte[] EncodeBlock(byte[] src)
        {
            byte[][] rounds = new byte[17][];
            rounds[0] = ApplyPermutation(src, _map1);

            int roundKey = 0;
            for (int i = 0; i < 16; i++)
            {
                rounds[i + 1] = ApplyRound(roundKey, rounds[i]);
                roundKey++;
            }

            return ApplyPermutation(new byte[8]
            {
                rounds[16][4], rounds[16][5], rounds[16][6], rounds[16][7],
                rounds[16][0], rounds[16][1], rounds[16][2], rounds[16][3]
            }, _map2);
        }

        private static byte[] ApplyPermutation(byte[] input, byte[] map)
        {
            byte[] output = new byte[8];
            int byteIndex = 0;
            int bitGroup = 0;

            while (bitGroup < 16)
            {
                byte b = input[byteIndex];
                int high = b >> 4;
                int low = b % 16;

                for (int i = 0; i < 8; i++)
                {
                    int offset = bitGroup * 128 + i;
                    output[i] |= (byte)(map[offset + high * 8] | map[offset + (16 + low) * 8]);
                }

                bitGroup += 2;
                byteIndex++;
            }

            return output;
        }

        private static byte[] ApplyRound(int roundKey, byte[] input)
        {
            byte[] right = new byte[4];
            Array.Copy(input, 4, right, 0, 4);
            byte[] transformed = ApplyFunction(right, roundKey);

            return new byte[8]
            {
                input[4], input[5], input[6], input[7],
                (byte)(transformed[0] ^ input[0]),
                (byte)(transformed[1] ^ input[1]),
                (byte)(transformed[2] ^ input[2]),
                (byte)(transformed[3] ^ input[3])
            };
        }

        private static byte[] ApplyFunction(byte[] input, int roundKey)
        {
            byte[] expanded = ExpandInput(input);
            int keyOffset = roundKey * 6;

            byte[] xored = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                xored[i] = (byte)(expanded[i] ^ _map5[keyOffset + i]);
            }

            return ApplyFinalPermutation(ApplySBox(xored));
        }

        private static byte[] ExpandInput(byte[] input)
        {
            return new byte[6]
            {
                (byte)(input[3] << 7 | ((input[0] & 249 | input[0] >> 2 & 6) >> 1)),
                (byte)(((input[0] & 1 | input[0] << 2) << 3) | ((input[1] >> 2 | input[1] & 135) >> 3)),
                (byte)(input[2] >> 7 | ((input[1] & 31 | (input[1] & 248) << 2) << 1)),
                (byte)(input[1] << 7 | ((input[2] & 249 | input[2] >> 2 & 6) >> 1)),
                (byte)(((input[2] & 1 | input[2] << 2) << 3) | ((input[3] >> 2 | input[3] & 135) >> 3)),
                (byte)(input[0] >> 7 | ((input[3] & 31 | (input[3] & 248) << 2) << 1))
            };
        }

        private static byte[] ApplySBox(byte[] input)
        {
            return new byte[4]
            {
                _map4[input[0] * 16 | input[1] >> 4],
                _map4[4096 + (input[2] | input[1] % 16 * 256)],
                _map4[8192 + (input[3] * 16 | input[4] >> 4)],
                _map4[12288 + (input[5] | input[4] % 16 * 256)]
            };
        }

        private static byte[] ApplyFinalPermutation(byte[] input)
        {
            byte[] output = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                int offset = (i * 256 + input[i]) * 4;
                output[0] |= _map3[offset];
                output[1] |= _map3[offset + 1];
                output[2] |= _map3[offset + 2];
                output[3] |= _map3[offset + 3];
            }
            return output;
        }
    }

    /// <summary>
    /// PAK 索引記錄
    /// </summary>
    public struct IndexRecord
    {
        /// <summary>
        /// 檔案在 PAK 中的偏移量 (使用 long 支援超過 2GB 的 PAK 讀取)
        /// </summary>
        public long Offset;
        public string FileName;
        public int FileSize;
        public string SourcePak;
        /// <summary>
        /// 壓縮後大小 (ExtB / _EXT 格式使用)
        /// </summary>
        public int CompressedSize;
        /// <summary>
        /// 旗標 (_EXT 格式: 0=未壓縮, 2=已壓縮)
        /// </summary>
        public int Flags;

        /// <summary>
        /// 從舊格式 (28 bytes) 解析
        /// </summary>
        public IndexRecord(byte[] data, int index)
        {
            // 使用 unsigned 讀取以支援超過 2GB 的 PAK
            Offset = BitConverter.ToUInt32(data, index);
            FileName = Encoding.Default.GetString(data, index + 4, 20).TrimEnd('\0');
            FileSize = BitConverter.ToInt32(data, index + 24);
            SourcePak = null;
            CompressedSize = 0;
            Flags = 0;
        }

        public IndexRecord(string filename, int size, long offset)
        {
            Offset = offset;
            FileName = filename;
            FileSize = size;
            SourcePak = null;
            CompressedSize = 0;
            Flags = 0;
        }

        public IndexRecord(string filename, int size, long offset, string sourcePak)
        {
            Offset = offset;
            FileName = filename;
            FileSize = size;
            SourcePak = sourcePak;
            CompressedSize = 0;
            Flags = 0;
        }

        private static string ExtractNullTerminated(byte[] data, int offset, int maxLen)
        {
            int end = Array.IndexOf(data, (byte)0, offset, maxLen);
            int len = (end >= 0) ? end - offset : maxLen;
            return Encoding.Default.GetString(data, offset, len);
        }

        /// <summary>
        /// 從 _EXT 格式條目 (128 bytes) 解析
        /// </summary>
        public static IndexRecord FromExtEntry(byte[] data, int index)
        {
            return new IndexRecord
            {
                Offset = BitConverter.ToUInt32(data, index),
                FileSize = BitConverter.ToInt32(data, index + 4),
                CompressedSize = BitConverter.ToInt32(data, index + 8),
                Flags = BitConverter.ToInt32(data, index + 12),
                FileName = ExtractNullTerminated(data, index + 16, 112),
                SourcePak = null
            };
        }

        /// <summary>
        /// 序列化為 _EXT 格式 (128 bytes)
        /// </summary>
        public byte[] ToExtBytes()
        {
            byte[] data = new byte[128];
            BitConverter.GetBytes((uint)Offset).CopyTo(data, 0);
            BitConverter.GetBytes(FileSize).CopyTo(data, 4);
            BitConverter.GetBytes(CompressedSize).CopyTo(data, 8);
            BitConverter.GetBytes(Flags).CopyTo(data, 12);
            byte[] nameBytes = Encoding.Default.GetBytes(FileName);
            Array.Copy(nameBytes, 0, data, 16, Math.Min(nameBytes.Length, 111));
            return data;
        }

        /// <summary>
        /// 序列化為舊格式 (28 bytes)
        /// </summary>
        public byte[] ToOldBytes()
        {
            byte[] data = new byte[28];
            BitConverter.GetBytes((uint)Offset).CopyTo(data, 0);
            byte[] nameBytes = Encoding.Default.GetBytes(FileName);
            Array.Copy(nameBytes, 0, data, 4, Math.Min(nameBytes.Length, 20));
            BitConverter.GetBytes(FileSize).CopyTo(data, 24);
            return data;
        }

        /// <summary>
        /// 序列化為 _IDX 格式 (32 bytes): offset(4) + filename(20) + size(4) + flags(4)
        /// </summary>
        public byte[] ToIdxV2Bytes()
        {
            byte[] data = new byte[32];
            BitConverter.GetBytes((uint)Offset).CopyTo(data, 0);
            byte[] nameBytes = Encoding.Default.GetBytes(FileName);
            Array.Copy(nameBytes, 0, data, 4, Math.Min(nameBytes.Length, 20));
            BitConverter.GetBytes(FileSize).CopyTo(data, 24);
            BitConverter.GetBytes(Flags).CopyTo(data, 28);
            return data;
        }

        public override string ToString()
        {
            if (Flags == 2 && CompressedSize > 0)
                return $"{FileName} (size={FileSize}, compressed={CompressedSize}, offset=0x{Offset:X})";
            return $"{FileName} (size={FileSize}, offset=0x{Offset:X})";
        }
    }
}
