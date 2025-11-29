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
            int blockIdx = 0;
            for (i = 0; i < len - 4 && len - 4 - i >= 16; i += 16)
            {
                byte[] pre = new byte[16];
                byte[] temp = new byte[16];
                Array.Copy(prex, i, pre, 0, 16);
                Array.Copy(prex, i, temp, 0, 16);
                char[] tempChars = FromByteArray(temp);

                if (blockIdx < 2) {
                    Console.Write($"Block{blockIdx} input (chars): ");
                    foreach (char c in tempChars) Console.Write($"{(int)c:X4} ");
                    Console.WriteLine();
                }

                aes.InvCipher(tempChars, blockIdx);

                if (blockIdx < 2) {
                    Console.Write($"Block{blockIdx} after InvCipher: ");
                    foreach (char c in tempChars) Console.Write($"{(int)c:X4} ");
                    Console.WriteLine();
                    Console.Write($"Block{blockIdx} skey before XOR: ");
                    foreach (sbyte s in skey) Console.Write($"{s} ");
                    Console.WriteLine();
                }

                // Java: tempChars[j] = (char)(tempChars[j] ^ skey[j]);
                // skey[j] is signed byte, XOR with char
                for (int j = 0; j < 16; j++)
                {
                    tempChars[j] = (char)(tempChars[j] ^ skey[j]);
                }

                if (blockIdx < 2) {
                    Console.Write($"Block{blockIdx} after XOR: ");
                    foreach (char c in tempChars) Console.Write($"{(int)c:X4} ");
                    Console.WriteLine();
                }

                temp = FromCharArray(tempChars);
                Array.Copy(temp, 0, prex, i, 16);
                // Update skey with original ciphertext
                for (int k = 0; k < 16; k++)
                {
                    skey[k] = (sbyte)pre[k];
                }
                blockIdx++;
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
        private static readonly char[] SBOX = new char[] { 'c', '|', 'w', '{', 'ò', 'k', 'o', 'Å', '0', '\u0001', 'g', '+', 'þ', '×', '«', 'v', 'Ê', '\u0082', 'É', '}', 'ú', 'Y', 'G', 'ð', '\u00ad', 'Ô', '¢', '¯', '\u009c', '¤', 'r', 'À', '·', 'ý', '\u0093', '&', '6', '?', '÷', 'Ì', '4', '¥', 'å', 'ñ', 'q', 'Ø', '1', '\u0015', '\u0004', 'Ç', '#', 'Ã', '\u0018', '\u0096', '\u0005', '\u009a', '\u0007', '\u0012', '\u0080', 'â', 'ë', '\'', '²', 'u', '\t', '\u0083', ',', '\u001a', '\u001b', 'n', 'Z', ' ', 'R', ';', 'Ö', '³', ')', 'ã', '/', '\u0084', 'S', 'Ñ', '\u0000', 'í', ' ', 'ü', '±', '[', 'j', 'Ë', '¾', '9', 'J', 'L', 'X', 'Ï', 'Ð', 'ï', 'ª', 'û', 'C', 'M', '3', '\u0085', 'E', 'ù', '\u0002', '\u007f', 'P', '<', '\u009f', '¨', 'Q', '£', '@', '\u008f', '\u0092', '\u009d', '8', 'õ', '¼', '¶', 'Ú', '!', '\u0010', 'ÿ', 'ó', 'Ò', 'Í', '\f', '\u0013', 'ì', '_', '\u0097', 'D', '\u0017', 'Ä', '§', '~', '=', 'd', ']', '\u0019', 's', '`', '\u0081', 'O', 'Ü', '"', '*', '\u0090', '\u0088', 'F', 'î', '¸', '\u0014', 'Þ', '^', '\u000b', 'Û', 'à', '2', ':', '\n', 'I', '\u0006', '$', '\\', 'Â', 'Ó', '¬', 'b', '\u0091', '\u0095', 'ä', 'y', 'ç', 'È', '7', 'm', '\u008d', 'Õ', 'N', '©', 'l', 'V', 'ô', 'ê', 'e', 'z', '®', '\b', 'º', 'x', '%', '.', '\u001c', '¦', '´', 'Æ', 'è', 'Ý', 't', '\u001f', 'K', '½', '\u008b', '\u008a', 'p', '>', 'µ', 'f', 'H', '\u0003', 'ö', '\u000e', 'a', '5', 'W', '¹', '\u0086', 'Á', '\u001d', '\u009e', 'á', 'ø', '\u0098', '\u0011', 'i', 'Ù', '\u008e', '\u0094', '\u009b', '\u001e', '\u0087', 'é', 'Î', 'U', '(', 'ß', '\u008c', '¡', '\u0089', '\r', '¿', 'æ', 'B', 'h', 'A', '\u0099', '-', '\u000f', '°', 'T', '»', '\u0016' };
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

        public void InvCipher(char[] input, int blockIdx = -1)
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

            // Debug: print EXPKEY[10] for first 2 blocks
            if (blockIdx >= 0 && blockIdx < 2)
            {
                Console.Write($"Block{blockIdx} EXPKEY[10]: ");
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        Console.Write($"{(int)EXPKEY[10][r][c]:X4} ");
                Console.WriteLine();
            }

            AddRoundKey(state, EXPKEY[10]);

            if (blockIdx >= 0 && blockIdx < 2)
            {
                Console.Write($"Block{blockIdx} after AddRoundKey[10]: ");
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        Console.Write($"{(int)state[r][c]:X4} ");
                Console.WriteLine();
            }

            for (int i = 9; i >= 0; i--)
            {
                InvShiftRows(state);
                if (blockIdx == 1 && i == 9)
                {
                    Console.Write($"Block{blockIdx} round {i} after InvShiftRows: ");
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                            Console.Write($"{(int)state[r][c]:X4} ");
                    Console.WriteLine();
                }

                InvSubBytes(state);
                if (blockIdx == 1 && i == 9)
                {
                    Console.Write($"Block{blockIdx} round {i} after InvSubBytes: ");
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                            Console.Write($"{(int)state[r][c]:X4} ");
                    Console.WriteLine();
                }

                AddRoundKey(state, EXPKEY[i]);
                if (blockIdx == 1 && i == 9)
                {
                    Console.Write($"Block{blockIdx} round {i} after AddRoundKey: ");
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                            Console.Write($"{(int)state[r][c]:X4} ");
                    Console.WriteLine();
                }

                if (i != 0)
                {
                    InvMixColumns(state);
                }

                if (blockIdx == 1 && i == 9)
                {
                    Console.Write($"Block{blockIdx} round {i} after InvMixColumns: ");
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                            Console.Write($"{(int)state[r][c]:X4} ");
                    Console.WriteLine();
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
