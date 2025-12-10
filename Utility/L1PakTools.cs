// Decompiled with JetBrains decompiler
// Type: PakViewer.Utility.L1PakTools
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using PakViewer.Properties;
using System;
using System.Text;
using System.Windows.Forms;

namespace PakViewer.Utility
{
  public static class L1PakTools
  {
    private static ToolStripProgressBar progressbar = (ToolStripProgressBar) null;
    private static byte[] Map1 = Resources.Map1;
    private static byte[] Map2 = Resources.Map2;
    private static byte[] Map5 = Resources.Map5;
    private static byte[] Map4 = Resources.Map4;
    private static byte[] Map3 = Resources.Map3;

    public static void ShowProgress(ToolStripProgressBar obj)
    {
      L1PakTools.progressbar = obj;
    }

    public static byte[] Encode(byte[] src, int index)
    {
      return L1PakTools.Coder(src, index, true);
    }

    public static byte[] Decode(byte[] src, int index)
    {
      return L1PakTools.Coder(src, index, false);
    }

    public static L1PakTools.IndexRecord Decode_Index_FirstRecord(byte[] src)
    {
      byte[] src1 = new byte[36];
      Array.Copy((Array) src, (Array) src1, src1.Length);
      return new L1PakTools.IndexRecord(L1PakTools.Decode(src1, 4), 0);
    }

    private static byte[] Coder(byte[] src, int index, bool IsEncode)
    {
      byte[] numArray = new byte[src.Length - index];
      if (L1PakTools.progressbar != null)
      {
        L1PakTools.progressbar.Maximum = src.Length;
        L1PakTools.progressbar.Value = 0;
      }
      if (numArray.Length >= 8)
      {
        byte[] src1 = new byte[8];
        int num = numArray.Length / 8;
        int destinationIndex = 0;
        for (; num > 0; --num)
        {
          Array.Copy((Array) src, destinationIndex + index, (Array) src1, 0, 8);
          if (IsEncode)
            Array.Copy((Array) L1PakTools.sub_403160(src1), 0, (Array) numArray, destinationIndex, 8);
          else
            Array.Copy((Array) L1PakTools.sub_403220(src1), 0, (Array) numArray, destinationIndex, 8);
          destinationIndex += 8;
          if (L1PakTools.progressbar != null)
            L1PakTools.progressbar.Increment(8);
        }
      }
      int length = numArray.Length % 8;
      if (length > 0)
      {
        int destinationIndex = numArray.Length - length;
        Array.Copy((Array) src, destinationIndex + index, (Array) numArray, destinationIndex, length);
      }
      if (L1PakTools.progressbar != null)
        L1PakTools.progressbar.Value = src.Length;
      return numArray;
    }

    private static byte[] sub_403220(byte[] src)
    {
      byte[][] numArray = new byte[17][];
      numArray[0] = L1PakTools.sub_4032E0(src, L1PakTools.Map1);
      int index = 0;
      int a1 = 15;
      while (a1 >= 0)
      {
        numArray[index + 1] = L1PakTools.sub_403340(a1, numArray[index]);
        --a1;
        ++index;
      }
      return L1PakTools.sub_4032E0(new byte[8]
      {
        numArray[16][4],
        numArray[16][5],
        numArray[16][6],
        numArray[16][7],
        numArray[16][0],
        numArray[16][1],
        numArray[16][2],
        numArray[16][3]
      }, L1PakTools.Map2);
    }

    private static byte[] sub_403160(byte[] src)
    {
      byte[][] numArray = new byte[17][];
      numArray[0] = L1PakTools.sub_4032E0(src, L1PakTools.Map1);
      int index = 0;
      int a1 = 0;
      while (a1 <= 15)
      {
        numArray[index + 1] = L1PakTools.sub_403340(a1, numArray[index]);
        ++a1;
        ++index;
      }
      return L1PakTools.sub_4032E0(new byte[8]
      {
        numArray[16][4],
        numArray[16][5],
        numArray[16][6],
        numArray[16][7],
        numArray[16][0],
        numArray[16][1],
        numArray[16][2],
        numArray[16][3]
      }, L1PakTools.Map2);
    }

    private static byte[] sub_4032E0(byte[] a1, byte[] a2)
    {
      byte[] numArray = new byte[8];
      int index1 = 0;
      int num1 = 0;
      while (num1 < 16)
      {
        byte num2 = a1[index1];
        int num3 = (int) num2 >> 4;
        int num4 = (int) num2 % 16;
        for (int index2 = 0; index2 < 8; ++index2)
        {
          int num5 = num1 * 128 + index2;
          numArray[index2] |= (byte) ((uint) a2[num5 + num3 * 8] | (uint) a2[num5 + (16 + num4) * 8]);
        }
        num1 += 2;
        ++index1;
      }
      return numArray;
    }

    private static byte[] sub_403340(int a1, byte[] a2)
    {
      byte[] a1_1 = new byte[4];
      Array.Copy((Array) a2, 4, (Array) a1_1, 0, 4);
      byte[] numArray = L1PakTools.sub_4033B0(a1_1, a1);
      return new byte[8]
      {
        a2[4],
        a2[5],
        a2[6],
        a2[7],
        (byte) ((uint) numArray[0] ^ (uint) a2[0]),
        (byte) ((uint) numArray[1] ^ (uint) a2[1]),
        (byte) ((uint) numArray[2] ^ (uint) a2[2]),
        (byte) ((uint) numArray[3] ^ (uint) a2[3])
      };
    }

    private static byte[] sub_4033B0(byte[] a1, int a2)
    {
      byte[] numArray = L1PakTools.sub_403450(a1);
      int index = a2 * 6;
      return L1PakTools.sub_4035A0(L1PakTools.sub_403520(new byte[6]
      {
        (byte) ((uint) numArray[0] ^ (uint) L1PakTools.Map5[index]),
        (byte) ((uint) numArray[1] ^ (uint) L1PakTools.Map5[index + 1]),
        (byte) ((uint) numArray[2] ^ (uint) L1PakTools.Map5[index + 2]),
        (byte) ((uint) numArray[3] ^ (uint) L1PakTools.Map5[index + 3]),
        (byte) ((uint) numArray[4] ^ (uint) L1PakTools.Map5[index + 4]),
        (byte) ((uint) numArray[5] ^ (uint) L1PakTools.Map5[index + 5])
      }));
    }

    private static byte[] sub_403450(byte[] a1)
    {
      return new byte[6]
      {
        (byte) ((int) a1[3] << 7 | ((int) a1[0] & 249 | (int) a1[0] >> 2 & 6) >> 1),
        (byte) (((int) a1[0] & 1 | (int) a1[0] << 2) << 3 | ((int) a1[1] >> 2 | (int) a1[1] & 135) >> 3),
        (byte) ((int) a1[2] >> 7 | ((int) a1[1] & 31 | ((int) a1[1] & 248) << 2) << 1),
        (byte) ((int) a1[1] << 7 | ((int) a1[2] & 249 | (int) a1[2] >> 2 & 6) >> 1),
        (byte) (((int) a1[2] & 1 | (int) a1[2] << 2) << 3 | ((int) a1[3] >> 2 | (int) a1[3] & 135) >> 3),
        (byte) ((int) a1[0] >> 7 | ((int) a1[3] & 31 | ((int) a1[3] & 248) << 2) << 1)
      };
    }

    private static byte[] sub_403520(byte[] a1)
    {
      return new byte[4]
      {
        L1PakTools.Map4[(int) a1[0] * 16 | (int) a1[1] >> 4],
        L1PakTools.Map4[4096 + ((int) a1[2] | (int) a1[1] % 16 * 256)],
        L1PakTools.Map4[8192 + ((int) a1[3] * 16 | (int) a1[4] >> 4)],
        L1PakTools.Map4[12288 + ((int) a1[5] | (int) a1[4] % 16 * 256)]
      };
    }

    private static byte[] sub_4035A0(byte[] a1)
    {
      byte[] numArray = new byte[4];
      for (int index1 = 0; index1 < 4; ++index1)
      {
        int index2 = (index1 * 256 + (int) a1[index1]) * 4;
        numArray[0] |= L1PakTools.Map3[index2];
        numArray[1] |= L1PakTools.Map3[index2 + 1];
        numArray[2] |= L1PakTools.Map3[index2 + 2];
        numArray[3] |= L1PakTools.Map3[index2 + 3];
      }
      return numArray;
    }

    public struct IndexRecord
    {
      public int Offset;
      public string FileName;
      public int FileSize;
      public string SourcePak;  // 來源 PAK 檔案路徑（用於 Sprite 模式）

      public IndexRecord(byte[] data, int index)
      {
        this.Offset = BitConverter.ToInt32(data, index);
        this.FileName = Encoding.Default.GetString(data, index + 4, 20).TrimEnd(new char[1]);
        this.FileSize = BitConverter.ToInt32(data, index + 24);
        this.SourcePak = null;
      }

      public IndexRecord(string filename, int size, int offset)
      {
        this.Offset = offset;
        this.FileName = filename;
        this.FileSize = size;
        this.SourcePak = null;
      }

      public IndexRecord(string filename, int size, int offset, string sourcePak)
      {
        this.Offset = offset;
        this.FileName = filename;
        this.FileSize = size;
        this.SourcePak = sourcePak;
      }
    }
  }
}
