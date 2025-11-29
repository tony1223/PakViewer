using System;

namespace PakViewer.Utility
{
    public class XmlCracker
    {
        public static bool IsEncrypted(byte[] data)
        {
            return data != null && data.Length > 4 && data[0] == 0x58;
        }

        public static bool IsDecryptedXml(byte[] data)
        {
            return data != null && data.Length > 4 && data[0] == 0x3C;
        }

        private static char[] GetKey()
        {
            return new char[] { 'Ü', '\u0084', '\u0001', '!', '*', '@', ' ', '\n', 'Ý', '%', '¹', '§', '\r', '¹', 'É', 'N', 'Í', '\u0099', '\n', '&', '¹', '¾', '"', '\u0089', 'Î', '\u0000', '\u001a', '\u00ad', '\u007f', 'Ø', '\u0011', '\u0084' };
        }

        // Java signed bytes
        private static sbyte[] GetSkey()
        {
            return new sbyte[] { 62, 9, 120, -86, -60, -43, 48, 99, 48, 12, 95, -102, -128, 127, 34, 70 };
        }

        public static byte[] Decrypt(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 4)
                return bytes;

            if (bytes[0] != 0x58)
                return bytes;

            char[] key = GetKey();
            Aes aes = new Aes(key);
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

        public static byte[] Encrypt(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 4)
                return bytes;

            char[] key = GetKey();
            Aes aes = new Aes(key);
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

        // Java: DataUtil.fromCharArray
        private static byte[] FromCharArray(char[] chars)
        {
            byte[] bytes = new byte[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                bytes[i] = (byte)chars[i];
            }
            return bytes;
        }

        // Java: DataUtil.fromByteArray
        // Java byte is signed, (char)byte does sign extension
        private static char[] FromByteArray(byte[] bytes)
        {
            char[] chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                // Simulate Java: chars[i] = (char)bytes[i] where bytes[i] is signed
                chars[i] = (char)(sbyte)bytes[i];
            }
            return chars;
        }
    }

    internal class Aes
    {
        // SBOX copied from Java - hex values: 63 7C 77 7B F2 6B 6F C5 30 01 67 2B FE D7 AB 76 CA 82 C9 7D FA 59 47 F0 AD D4 A2 AF 9C A4 72 C0 B7 FD 93 26 36 3F F7 CC 34 A5 E5 F1 71 D8 31 15 04 C7 23 C3 18 96 05 9A 07 12 80 E2 EB 27 B2 75 09 83 2C 1A 1B 6E 5A A0 52 3B D6 B3 29 E3 2F 84 53 D1 00 ED 20 FC B1 5B 6A CB BE 39 4A 4C 58 CF D0 EF AA FB 43 4D 33 85 45 F9 02 7F 50 3C 9F A8 51 A3 40 8F 92 9D 38 F5 BC B6 DA 21 10 FF F3 D2 CD 0C 13 EC 5F 97 44 17 C4 A7 7E 3D 64 5D 19 73 60 81 4F DC 22 2A 90 88 46 EE B8 14 DE 5E 0B DB E0 32 3A 0A 49 06 24 5C C2 D3 AC 62 91 95 E4 79 E7 C8 37 6D 8D D5 4E A9 6C 56 F4 EA 65 7A AE 08 BA 78 25 2E 1C A6 B4 C6 E8 DD 74 1F 4B BD 8B 8A 70 3E B5 66 48 03 F6 0E 61 35 57 B9 86 C1 1D 9E E1 F8 98 11 69 D9 8E 94 9B 1E 87 E9 CE 55 28 DF 8C A1 89 0D BF E6 42 68 41 99 2D 0F B0 54 BB 16
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
        // INVSBOX copied from Java - hex values: 52 09 6A D5 30 36 A5 38 BF 40 A3 9E 81 F3 D7 FB 7C E3 39 82 9B 2F FF 87 34 8E 43 44 C4 DE E9 CB 54 7B 94 32 A6 C2 23 3D EE 4C 95 0B 42 FA C3 4E 08 2E A1 66 28 D9 24 B2 76 5B A2 49 6D 8B D1 25 72 F8 F6 64 86 68 98 16 D4 A4 5C CC 5D 65 B6 92 6C 70 48 50 FD ED B9 DA 5E 15 46 57 A7 8D 9D 84 90 D8 AB 00 8C BC D3 0A F7 E4 58 05 B8 B3 45 06 D0 2C 1E 8F CA 3F 0F 02 C1 AF BD 03 01 13 8A 6B 3A 91 11 41 4F 67 DC EA 97 F2 CF CE F0 B4 E6 73 96 AC 74 22 E7 AD 35 85 E2 F9 37 E8 1C 75 DF 6E 47 F1 1A 71 1D 29 C5 89 6F B7 62 0E AA 18 BE 1B FC 56 3E 4B C6 D2 79 20 9A DB C0 FE 78 CD 5A F4 1F DD A8 33 88 07 C7 31 B1 12 10 59 27 80 EC 5F 60 51 7F A9 19 B5 4A 0D 2D E5 7A 9F 93 C9 9C EF A0 E0 3B 4D AE 2A F5 B0 C8 EB BB 3C 83 53 99 61 17 2B 04 7E BA 77 D6 26 E1 69 14 63 55 21 0C 7D
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
        private static readonly char[][][] EXPKEY;

        static Aes()
        {
            EXPKEY = new char[11][][];
            for (int i = 0; i < 11; i++)
            {
                EXPKEY[i] = new char[4][];
                for (int j = 0; j < 4; j++)
                {
                    EXPKEY[i][j] = new char[4];
                }
            }
        }

        public Aes(char[] key)
        {
            ExpandKey(key, EXPKEY);
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

            AddRoundKey(state, EXPKEY[0]);

            for (int i = 1; i <= 10; i++)
            {
                SubBytes(state);
                ShiftRows(state);
                if (i != 10)
                {
                    MixColumns(state);
                }
                AddRoundKey(state, EXPKEY[i]);
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

            AddRoundKey(state, EXPKEY[10]);

            for (int i = 9; i >= 0; i--)
            {
                InvShiftRows(state);
                InvSubBytes(state);
                AddRoundKey(state, EXPKEY[i]);
                if (i != 0)
                {
                    InvMixColumns(state);
                }
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
