// Decompiled with JetBrains decompiler
// Type: PakViewer.Utility.L1Spr
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PakViewer.Utility
{
  public static class L1Spr
  {
    public static L1Spr.Frame[] Load(byte[] sprdata)
    {
      ushort[] numArray1 = (ushort[]) null;
      ushort num1 = 32768;
      ushort[] array = new ushort[7]
      {
        (ushort) 31744,
        (ushort) 768,
        (ushort) 31,
        (ushort) 32736,
        (ushort) 1023,
        (ushort) 31775,
        (ushort) short.MaxValue
      };
      BinaryReader binaryReader = new BinaryReader((Stream) new MemoryStream(sprdata));
      if (binaryReader == null)
        return (L1Spr.Frame[]) null;
      bool flag = false;
      byte num2 = binaryReader.ReadByte();
      if ((int) num2 == (int) byte.MaxValue)
      {
        flag = true;
        int length = (int) binaryReader.ReadByte();
        if (length == 0)
          length = 256;
        numArray1 = new ushort[length];
        for (int index1 = 0; index1 < length; ++index1)
        {
          numArray1[index1] = binaryReader.ReadUInt16();
          int index2 = Array.IndexOf<ushort>(array, numArray1[index1]);
          if (index2 >= 0)
            array[index2] = (ushort) 0;
        }
        foreach (ushort num3 in array)
        {
          if ((int) num3 != 0)
          {
            num1 = num3;
            break;
          }
        }
        num2 = binaryReader.ReadByte();
      }
      L1Spr.Frame[] frameArray = new L1Spr.Frame[(int) num2];
      L1Spr.BlockDef[][] blockDefArray = new L1Spr.BlockDef[(int) num2][];
      for (int index1 = 0; index1 < (int) num2; ++index1)
      {
        frameArray[index1].x_offset = (int) binaryReader.ReadInt16();
        frameArray[index1].y_offset = (int) binaryReader.ReadInt16();
        frameArray[index1].width = (int) binaryReader.ReadInt16() - frameArray[index1].x_offset + 1;
        frameArray[index1].height = (int) binaryReader.ReadInt16() - frameArray[index1].y_offset + 1;
        frameArray[index1].unknow_1 = binaryReader.ReadUInt16();
        frameArray[index1].unknow_2 = binaryReader.ReadUInt16();
        int length = (int) binaryReader.ReadUInt16();
        if (length > 0)
        {
          blockDefArray[index1] = new L1Spr.BlockDef[length];
          for (int index2 = 0; index2 < length; ++index2)
          {
            blockDefArray[index1][index2].a = (int) binaryReader.ReadSByte();
            blockDefArray[index1][index2].b = (int) binaryReader.ReadSByte();
            blockDefArray[index1][index2].FrameType = (int) binaryReader.ReadByte();
            blockDefArray[index1][index2].BlockID = (int) binaryReader.ReadUInt16();
          }
          frameArray[index1].type = blockDefArray[index1][0].FrameType;
        }
      }
      Console.WriteLine(binaryReader.BaseStream.Position.ToString("X4"));
      int length1 = binaryReader.ReadInt32();
      int[] numArray2 = new int[length1];
      for (int index = 0; index < length1; ++index)
        numArray2[index] = binaryReader.ReadInt32();
      Console.WriteLine("Data after BlockOffset: {0}", (object) binaryReader.ReadInt32().ToString("x"));
      int position = (int) binaryReader.BaseStream.Position;
      ushort[][,] numArray3 = new ushort[length1][,];
      for (int index1 = 0; index1 < length1; ++index1)
      {
        numArray3[index1] = new ushort[24, 24];
        for (int index2 = 0; index2 < 24; ++index2)
        {
          for (int index3 = 0; index3 < 24; ++index3)
            numArray3[index1][index2, index3] = (ushort) 32768;
        }
        binaryReader.BaseStream.Seek((long) (position + numArray2[index1]), SeekOrigin.Begin);
        byte num3 = binaryReader.ReadByte();
        byte num4 = binaryReader.ReadByte();
        int num5 = (int) binaryReader.ReadByte();
        byte num6 = binaryReader.ReadByte();
        for (int index2 = 0; index2 < (int) num6; ++index2)
        {
          int index3 = (int) num3;
          byte num7 = binaryReader.ReadByte();
          for (int index4 = 0; index4 < (int) num7; ++index4)
          {
            index3 += (int) binaryReader.ReadByte() / 2;
            int num8 = (int) binaryReader.ReadByte();
            for (int index5 = 0; index5 < num8; ++index5)
            {
              if (flag)
              {
                numArray3[index1][index2 + (int) num4, index3] = numArray1[(int) binaryReader.ReadByte()];
              }
              else
              {
                ushort num9 = binaryReader.ReadUInt16();
                numArray3[index1][index2 + (int) num4, index3] = num9;
                int index6 = Array.IndexOf<ushort>(array, num9);
                if (index6 >= 0)
                  array[index6] = (ushort) 0;
              }
              ++index3;
            }
          }
        }
      }
      Console.WriteLine(binaryReader.BaseStream.Position.ToString("X4"));
      if ((int) num1 == 32768)
      {
        foreach (ushort num3 in array)
        {
          if ((int) num3 != 0)
          {
            num1 = num3;
            break;
          }
        }
        if ((int) num1 == 32768)
        {
          int num4 = (int) MessageBox.Show("太神奇了吧! 所有預設的遮罩色被用掉了!!!");
        }
      }
      for (int index1 = 0; index1 < (int) num2; ++index1)
      {
        if (blockDefArray[index1] != null)
        {
          L1Spr.Frame frame = frameArray[index1];
          frameArray[index1].maskcolor = num1;
          byte[] bmpdata = new byte[frame.height * (frame.width + frame.width % 2) * 2];
          int num3;
          for (int index2 = 0; index2 < bmpdata.Length; index2 = num3 + 1)
          {
            bmpdata[index2] = (byte) ((uint) num1 & (uint) byte.MaxValue);
            bmpdata[num3 = index2 + 1] = (byte) (((int) num1 & 65280) >> 8);
          }
          for (int index2 = 0; index2 < blockDefArray[index1].Length; ++index2)
          {
            int a = blockDefArray[index1][index2].a;
            if (a < 0)
              --a;
            int num4 = 24 * (blockDefArray[index1][index2].b + blockDefArray[index1][index2].a - a / 2);
            int num5 = 12 * (blockDefArray[index1][index2].b - a / 2);
            ushort[,] numArray4 = numArray3[blockDefArray[index1][index2].BlockID];
            for (int index3 = 0; index3 < 24; ++index3)
            {
              for (int index4 = 0; index4 < 24; ++index4)
              {
                ushort num6 = numArray4[index3, index4];
                if ((int) num6 != 32768)
                {
                  int num7 = num4 + index4;
                  int num8 = num5 + index3;
                  if (num7 >= frame.x_offset && num7 < frame.width + frame.x_offset && (num8 >= frame.y_offset && num8 < frame.height + frame.y_offset))
                  {
                    int index5 = ((num8 - frame.y_offset) * (frame.width + frame.width % 2) + (num7 - frame.x_offset)) * 2;
                    bmpdata[index5] = (byte) ((uint) num6 & (uint) byte.MaxValue);
                    int num9;
                    bmpdata[num9 = index5 + 1] = (byte) (((int) num6 & 65280) >> 8);
                  }
                }
              }
            }
          }
          frameArray[index1].image = (Image) L1Spr.CreateBitmap(frameArray[index1], bmpdata);
        }
      }
      return frameArray;
    }

    public static Bitmap CreateBitmap(L1Spr.Frame FrameData, byte[] bmpdata)
    {
      if (bmpdata == null)
        return (Bitmap) null;
      return ImageConvert.CreateBMP(FrameData.width, FrameData.height, bmpdata, 0, (int) FrameData.maskcolor);
    }

    public struct Frame
    {
      public int x_offset;
      public int y_offset;
      public int width;
      public int height;
      public ushort unknow_1;
      public ushort unknow_2;
      public int type;
      public ushort maskcolor;
      public Image image;
    }

    private struct BlockDef
    {
      public int a;
      public int b;
      public int FrameType;
      public int BlockID;
    }
  }
}
