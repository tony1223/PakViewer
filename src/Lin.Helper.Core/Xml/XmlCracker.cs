using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Lin.Helper.Core.Xml
{
    /// <summary>
    /// Lineage 1 XML 加解密工具
    /// </summary>
    public static class XmlCracker
    {
        /// <summary>
        /// 檢查資料是否為加密的 XML
        /// </summary>
        public static bool IsEncrypted(byte[] data)
        {
            return data != null && data.Length > 4 && data[0] == 0x58;
        }

        /// <summary>
        /// 檢查資料是否為解密的 XML
        /// </summary>
        public static bool IsDecryptedXml(byte[] data)
        {
            return data != null && data.Length > 4 && data[0] == 0x3C;
        }

        /// <summary>
        /// 從 XML 資料中解析 encoding 聲明，回傳對應的 Encoding
        /// </summary>
        public static Encoding GetXmlEncoding(byte[] data, string fallbackByFileName = null)
        {
            if (data == null || data.Length < 10)
                return GetFallbackEncoding(fallbackByFileName);

            try
            {
                int headerLen = Math.Min(200, data.Length);
                string header = Encoding.ASCII.GetString(data, 0, headerLen);

                var match = Regex.Match(header, @"encoding\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string encodingName = match.Groups[1].Value.ToLower();

                    return encodingName switch
                    {
                        "utf-8" => Encoding.UTF8,
                        "utf8" => Encoding.UTF8,
                        "big5" => Encoding.GetEncoding("big5"),
                        "euc-kr" => Encoding.GetEncoding("euc-kr"),
                        "euckr" => Encoding.GetEncoding("euc-kr"),
                        "shift_jis" => Encoding.GetEncoding("shift_jis"),
                        "shift-jis" => Encoding.GetEncoding("shift_jis"),
                        "shiftjis" => Encoding.GetEncoding("shift_jis"),
                        "gb2312" => Encoding.GetEncoding("gb2312"),
                        "gbk" => Encoding.GetEncoding("gbk"),
                        "gb18030" => Encoding.GetEncoding("gb18030"),
                        _ => Encoding.GetEncoding(encodingName)
                    };
                }
            }
            catch
            {
                // Parse failed, use fallback
            }

            return GetFallbackEncoding(fallbackByFileName);
        }

        private static Encoding GetFallbackEncoding(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return Encoding.UTF8;

            string fileNameLower = fileName.ToLower();
            if (fileNameLower.Contains("-k."))
                return Encoding.GetEncoding("euc-kr");
            else if (fileNameLower.Contains("-j."))
                return Encoding.GetEncoding("shift_jis");
            else if (fileNameLower.Contains("-h."))
                return Encoding.GetEncoding("gb2312");
            else if (fileNameLower.Contains("-c."))
                return Encoding.GetEncoding("big5");
            else
                return Encoding.UTF8;
        }

        private static char[] GetKey()
        {
            return new char[] { 'Ü', '\u0084', '\u0001', '!', '*', '@', ' ', '\n', 'Ý', '%', '¹', '§', '\r', '¹', 'É', 'N', 'Í', '\u0099', '\n', '&', '¹', '¾', '"', '\u0089', 'Î', '\u0000', '\u001a', '\u00ad', '\u007f', 'Ø', '\u0011', '\u0084' };
        }

        private static sbyte[] GetSkey()
        {
            return new sbyte[] { 62, 9, 120, -86, -60, -43, 48, 99, 48, 12, 95, -102, -128, 127, 34, 70 };
        }

        /// <summary>
        /// 解密 XML 資料
        /// </summary>
        public static byte[] Decrypt(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 4)
                return bytes;

            if (bytes[0] != 0x58)
                return bytes;

            char[] key = GetKey();
            var aes = new AesImpl(key);
            sbyte[] skey = (sbyte[])GetSkey().Clone();
            int len = bytes.Length;
            byte[] prex = new byte[len - 4];
            Array.Copy(bytes, 4, prex, 0, prex.Length);

            int i;
            for (i = 0; i < len - 4 && len - 4 - i >= 16; i += 16)
            {
                byte[] pre = new byte[16];
                byte[] temp = new byte[16];
                Array.Copy(prex, i, pre, 0, 16);
                Array.Copy(prex, i, temp, 0, 16);
                char[] tempChars = FromByteArray(temp);

                aes.InvCipher(tempChars);

                for (int j = 0; j < 16; j++)
                {
                    tempChars[j] = (char)(tempChars[j] ^ skey[j]);
                }

                temp = FromCharArray(tempChars);
                Array.Copy(temp, 0, prex, i, 16);
                for (int k = 0; k < 16; k++)
                {
                    skey[k] = (sbyte)pre[k];
                }
            }

            bytes[0] = 60; // '<'
            if (len - 4 > i)
            {
                for (int j = 0; j < 16 && i + j < len - 4; j++)
                {
                    prex[i + j] = (byte)(prex[i + j] ^ skey[j]);
                }
            }

            Array.Copy(prex, 0, bytes, 4, len - 4);
            return bytes;
        }

        /// <summary>
        /// 加密 XML 資料
        /// </summary>
        public static byte[] Encrypt(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 4)
                return bytes;

            char[] key = GetKey();
            var aes = new AesImpl(key);
            sbyte[] skey = (sbyte[])GetSkey().Clone();
            int len = bytes.Length;
            byte[] prex = new byte[len - 4];
            Array.Copy(bytes, 4, prex, 0, prex.Length);

            int i;
            for (i = 0; i < len - 4 && len - 4 - i >= 16; i += 16)
            {
                byte[] temp = new byte[16];
                Array.Copy(prex, i, temp, 0, 16);
                char[] tempChars = FromByteArray(temp);

                for (int j = 0; j < 16; j++)
                {
                    tempChars[j] = (char)(tempChars[j] ^ skey[j]);
                }

                aes.Cipher(tempChars);
                temp = FromCharArray(tempChars);
                for (int k = 0; k < 16; k++)
                {
                    skey[k] = (sbyte)temp[k];
                }
                Array.Copy(temp, 0, prex, i, 16);
            }

            bytes[0] = 88; // 'X'
            if (len - 4 > i)
            {
                for (int j = 0; j < 16 && i + j < len - 4; j++)
                {
                    prex[i + j] = (byte)(prex[i + j] ^ skey[j]);
                }
            }

            Array.Copy(prex, 0, bytes, 4, len - 4);
            return bytes;
        }

        private static byte[] FromCharArray(char[] chars)
        {
            byte[] bytes = new byte[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                bytes[i] = (byte)chars[i];
            }
            return bytes;
        }

        private static char[] FromByteArray(byte[] bytes)
        {
            char[] chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i] = (char)(sbyte)bytes[i];
            }
            return chars;
        }
    }

    /// <summary>
    /// AES 實作 (用於 XML 加解密)
    /// </summary>
    internal class AesImpl
    {
        private static readonly char[] SBOX = new char[] {
            '\u0063', '\u007C', '\u0077', '\u007B', '\u00F2', '\u006B', '\u006F', '\u00C5', '\u0030', '\u0001', '\u0067', '\u002B', '\u00FE', '\u00D7', '\u00AB', '\u0076',
            '\u00CA', '\u0082', '\u00C9', '\u007D', '\u00FA', '\u0059', '\u0047', '\u00F0', '\u00AD', '\u00D4', '\u00A2', '\u00AF', '\u009C', '\u00A4', '\u0072', '\u00C0',
            '\u00B7', '\u00FD', '\u0093', '\u0026', '\u0036', '\u003F', '\u00F7', '\u00CC', '\u0034', '\u00A5', '\u00E5', '\u00F1', '\u0071', '\u00D8', '\u0031', '\u0015',
            '\u0004', '\u00C7', '\u0023', '\u00C3', '\u0018', '\u0096', '\u0005', '\u009A', '\u0007', '\u0012', '\u0080', '\u00E2', '\u00EB', '\u0027', '\u00B2', '\u0075',
            '\u0009', '\u0083', '\u002C', '\u001A', '\u001B', '\u006E', '\u005A', '\u00A0', '\u0052', '\u003B', '\u00D6', '\u00B3', '\u0029', '\u00E3', '\u002F', '\u0084',
            '\u0053', '\u00D1', '\u0000', '\u00ED', '\u0020', '\u00FC', '\u00B1', '\u005B', '\u006A', '\u00CB', '\u00BE', '\u0039', '\u004A', '\u004C', '\u0058', '\u00CF',
            '\u00D0', '\u00EF', '\u00AA', '\u00FB', '\u0043', '\u004D', '\u0033', '\u0085', '\u0045', '\u00F9', '\u0002', '\u007F', '\u0050', '\u003C', '\u009F', '\u00A8',
            '\u0051', '\u00A3', '\u0040', '\u008F', '\u0092', '\u009D', '\u0038', '\u00F5', '\u00BC', '\u00B6', '\u00DA', '\u0021', '\u0010', '\u00FF', '\u00F3', '\u00D2',
            '\u00CD', '\u000C', '\u0013', '\u00EC', '\u005F', '\u0097', '\u0044', '\u0017', '\u00C4', '\u00A7', '\u007E', '\u003D', '\u0064', '\u005D', '\u0019', '\u0073',
            '\u0060', '\u0081', '\u004F', '\u00DC', '\u0022', '\u002A', '\u0090', '\u0088', '\u0046', '\u00EE', '\u00B8', '\u0014', '\u00DE', '\u005E', '\u000B', '\u00DB',
            '\u00E0', '\u0032', '\u003A', '\u000A', '\u0049', '\u0006', '\u0024', '\u005C', '\u00C2', '\u00D3', '\u00AC', '\u0062', '\u0091', '\u0095', '\u00E4', '\u0079',
            '\u00E7', '\u00C8', '\u0037', '\u006D', '\u008D', '\u00D5', '\u004E', '\u00A9', '\u006C', '\u0056', '\u00F4', '\u00EA', '\u0065', '\u007A', '\u00AE', '\u0008',
            '\u00BA', '\u0078', '\u0025', '\u002E', '\u001C', '\u00A6', '\u00B4', '\u00C6', '\u00E8', '\u00DD', '\u0074', '\u001F', '\u004B', '\u00BD', '\u008B', '\u008A',
            '\u0070', '\u003E', '\u00B5', '\u0066', '\u0048', '\u0003', '\u00F6', '\u000E', '\u0061', '\u0035', '\u0057', '\u00B9', '\u0086', '\u00C1', '\u001D', '\u009E',
            '\u00E1', '\u00F8', '\u0098', '\u0011', '\u0069', '\u00D9', '\u008E', '\u0094', '\u009B', '\u001E', '\u0087', '\u00E9', '\u00CE', '\u0055', '\u0028', '\u00DF',
            '\u008C', '\u00A1', '\u0089', '\u000D', '\u00BF', '\u00E6', '\u0042', '\u0068', '\u0041', '\u0099', '\u002D', '\u000F', '\u00B0', '\u0054', '\u00BB', '\u0016'
        };

        private static readonly char[] INVSBOX = new char[] {
            '\u0052', '\u0009', '\u006A', '\u00D5', '\u0030', '\u0036', '\u00A5', '\u0038', '\u00BF', '\u0040', '\u00A3', '\u009E', '\u0081', '\u00F3', '\u00D7', '\u00FB',
            '\u007C', '\u00E3', '\u0039', '\u0082', '\u009B', '\u002F', '\u00FF', '\u0087', '\u0034', '\u008E', '\u0043', '\u0044', '\u00C4', '\u00DE', '\u00E9', '\u00CB',
            '\u0054', '\u007B', '\u0094', '\u0032', '\u00A6', '\u00C2', '\u0023', '\u003D', '\u00EE', '\u004C', '\u0095', '\u000B', '\u0042', '\u00FA', '\u00C3', '\u004E',
            '\u0008', '\u002E', '\u00A1', '\u0066', '\u0028', '\u00D9', '\u0024', '\u00B2', '\u0076', '\u005B', '\u00A2', '\u0049', '\u006D', '\u008B', '\u00D1', '\u0025',
            '\u0072', '\u00F8', '\u00F6', '\u0064', '\u0086', '\u0068', '\u0098', '\u0016', '\u00D4', '\u00A4', '\u005C', '\u00CC', '\u005D', '\u0065', '\u00B6', '\u0092',
            '\u006C', '\u0070', '\u0048', '\u0050', '\u00FD', '\u00ED', '\u00B9', '\u00DA', '\u005E', '\u0015', '\u0046', '\u0057', '\u00A7', '\u008D', '\u009D', '\u0084',
            '\u0090', '\u00D8', '\u00AB', '\u0000', '\u008C', '\u00BC', '\u00D3', '\u000A', '\u00F7', '\u00E4', '\u0058', '\u0005', '\u00B8', '\u00B3', '\u0045', '\u0006',
            '\u00D0', '\u002C', '\u001E', '\u008F', '\u00CA', '\u003F', '\u000F', '\u0002', '\u00C1', '\u00AF', '\u00BD', '\u0003', '\u0001', '\u0013', '\u008A', '\u006B',
            '\u003A', '\u0091', '\u0011', '\u0041', '\u004F', '\u0067', '\u00DC', '\u00EA', '\u0097', '\u00F2', '\u00CF', '\u00CE', '\u00F0', '\u00B4', '\u00E6', '\u0073',
            '\u0096', '\u00AC', '\u0074', '\u0022', '\u00E7', '\u00AD', '\u0035', '\u0085', '\u00E2', '\u00F9', '\u0037', '\u00E8', '\u001C', '\u0075', '\u00DF', '\u006E',
            '\u0047', '\u00F1', '\u001A', '\u0071', '\u001D', '\u0029', '\u00C5', '\u0089', '\u006F', '\u00B7', '\u0062', '\u000E', '\u00AA', '\u0018', '\u00BE', '\u001B',
            '\u00FC', '\u0056', '\u003E', '\u004B', '\u00C6', '\u00D2', '\u0079', '\u0020', '\u009A', '\u00DB', '\u00C0', '\u00FE', '\u0078', '\u00CD', '\u005A', '\u00F4',
            '\u001F', '\u00DD', '\u00A8', '\u0033', '\u0088', '\u0007', '\u00C7', '\u0031', '\u00B1', '\u0012', '\u0010', '\u0059', '\u0027', '\u0080', '\u00EC', '\u005F',
            '\u0060', '\u0051', '\u007F', '\u00A9', '\u0019', '\u00B5', '\u004A', '\u000D', '\u002D', '\u00E5', '\u007A', '\u009F', '\u0093', '\u00C9', '\u009C', '\u00EF',
            '\u00A0', '\u00E0', '\u003B', '\u004D', '\u00AE', '\u002A', '\u00F5', '\u00B0', '\u00C8', '\u00EB', '\u00BB', '\u003C', '\u0083', '\u0053', '\u0099', '\u0061',
            '\u0017', '\u002B', '\u0004', '\u007E', '\u00BA', '\u0077', '\u00D6', '\u0026', '\u00E1', '\u0069', '\u0014', '\u0063', '\u0055', '\u0021', '\u000C', '\u007D'
        };

        private static readonly char[] RCON = new char[] { '\u0001', '\u0002', '\u0004', '\b', '\u0010', ' ', '@', '\u0080', '\u001b', '6' };
        private readonly char[][][] _expKey;

        public AesImpl(char[] key)
        {
            _expKey = new char[11][][];
            for (int i = 0; i < 11; i++)
            {
                _expKey[i] = new char[4][];
                for (int j = 0; j < 4; j++)
                {
                    _expKey[i][j] = new char[4];
                }
            }
            ExpandKey(key, _expKey);
        }

        public void Cipher(char[] input)
        {
            char[][] state = new char[4][];
            for (int i = 0; i < 4; i++) state[i] = new char[4];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = input[c * 4 + r];
                }
            }

            AddRoundKey(state, _expKey[0]);

            for (int i = 1; i <= 10; i++)
            {
                SubBytes(state);
                ShiftRows(state);
                if (i != 10) MixColumns(state);
                AddRoundKey(state, _expKey[i]);
            }

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    input[c * 4 + r] = state[r][c];
                }
            }
        }

        public void InvCipher(char[] input)
        {
            char[][] state = new char[4][];
            for (int i = 0; i < 4; i++) state[i] = new char[4];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = input[c * 4 + r];
                }
            }

            AddRoundKey(state, _expKey[10]);

            for (int i = 9; i >= 0; i--)
            {
                InvShiftRows(state);
                InvSubBytes(state);
                AddRoundKey(state, _expKey[i]);
                if (i != 0) InvMixColumns(state);
            }

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    input[c * 4 + r] = state[r][c];
                }
            }
        }

        private void ExpandKey(char[] key, char[][][] expKeyArray)
        {
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    expKeyArray[0][r][c] = key[r + c * 4];
                }
            }

            for (int i = 1; i <= 10; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    char[] t = new char[4];

                    for (int r = 0; r < 4; r++)
                    {
                        t[r] = j != 0 ? expKeyArray[i][r][j - 1] : expKeyArray[i - 1][r][3];
                    }

                    if (j == 0)
                    {
                        char temp = t[0];
                        for (int r = 0; r < 3; r++)
                        {
                            t[r] = SBOX[t[(r + 1) % 4]];
                        }
                        t[3] = SBOX[temp];
                        t[0] ^= RCON[i - 1];
                    }

                    for (int r = 0; r < 4; r++)
                    {
                        expKeyArray[i][r][j] = (char)(expKeyArray[i - 1][r][j] ^ t[r]);
                    }
                }
            }
        }

        private char FFMul(char a, char b)
        {
            char[] bw = new char[4];
            char res = '\0';
            bw[0] = b;

            for (int i = 1; i < 4; i++)
            {
                bw[i] = (char)(bw[i - 1] << 1);
                if ((bw[i - 1] & 128) != 0)
                {
                    bw[i] = (char)(bw[i] ^ 27);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if ((a >> i & 1) != 0)
                {
                    res ^= bw[i];
                }
            }

            return res;
        }

        private void AddRoundKey(char[][] state, char[][] k)
        {
            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    state[r][c] ^= k[r][c];
                }
            }
        }

        private void SubBytes(char[][] state)
        {
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = SBOX[state[r][c] & 255];
                }
            }
        }

        private void ShiftRows(char[][] state)
        {
            char[] t = new char[4];

            for (int r = 1; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    t[c] = state[r][(c + r) % 4];
                }
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = t[c];
                }
            }
        }

        private void MixColumns(char[][] state)
        {
            char[] t = new char[4];

            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    t[r] = state[r][c];
                }
                for (int r = 0; r < 4; r++)
                {
                    state[r][c] = (char)(FFMul('\u0002', t[r]) ^ FFMul('\u0003', t[(r + 1) % 4]) ^ FFMul('\u0001', t[(r + 2) % 4]) ^ FFMul('\u0001', t[(r + 3) % 4]));
                }
            }
        }

        private void InvSubBytes(char[][] state)
        {
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = INVSBOX[state[r][c] & 255];
                }
            }
        }

        private void InvShiftRows(char[][] state)
        {
            char[] t = new char[4];

            for (int r = 1; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    t[c] = state[r][(c - r + 4) % 4];
                }
                for (int c = 0; c < 4; c++)
                {
                    state[r][c] = t[c];
                }
            }
        }

        private void InvMixColumns(char[][] state)
        {
            char[] t = new char[4];

            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    t[r] = state[r][c];
                }
                for (int r = 0; r < 4; r++)
                {
                    state[r][c] = (char)(FFMul('\u000e', t[r]) ^ FFMul('\u000b', t[(r + 1) % 4]) ^ FFMul('\r', t[(r + 2) % 4]) ^ FFMul('\t', t[(r + 3) % 4]));
                }
            }
        }
    }
}
