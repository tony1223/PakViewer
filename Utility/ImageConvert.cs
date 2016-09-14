// Decompiled with JetBrains decompiler
// Type: PakViewer.Utility.ImageConvert
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace PakViewer.Utility
{
  public class ImageConvert
  {
    public static Bitmap CreateBMP(int width, int height, byte[] srcdata, int index, int MaskColor)
    {
      Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format16bppRgb555);
      Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
      BitmapData bitmapdata = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
      int num = bitmapdata.Stride / 1;
      byte[] source = new byte[num * height];
      if (srcdata.Length - index == source.Length)
      {
        Array.Copy((Array) srcdata, index, (Array) source, 0, source.Length);
      }
      else
      {
        for (int index1 = 0; index1 < height; ++index1)
          Array.Copy((Array) srcdata, index + index1 * width * 2, (Array) source, index1 * num, width * 2);
      }
      IntPtr scan0 = bitmapdata.Scan0;
      Marshal.Copy(source, 0, scan0, srcdata.Length - index);
      bitmap.UnlockBits(bitmapdata);
      if (MaskColor >= 0)
        bitmap.MakeTransparent(ImageConvert.Rgb555ToARGB(MaskColor));
      return bitmap;
    }

    public static Color Rgb555ToARGB(int Rgb555)
    {
      return Color.FromArgb((Rgb555 & 31744) >> 10, (Rgb555 & 992) >> 5, Rgb555 & 31);
    }

    public static Bitmap Load_IMG(byte[] imgdata)
    {
      int int16_1 = (int) BitConverter.ToInt16(imgdata, 0);
      int int16_2 = (int) BitConverter.ToInt16(imgdata, 2);
      int MaskColor = (int) BitConverter.ToInt16(imgdata, 4) == 1 ? (int) BitConverter.ToUInt16(imgdata, 6) : -1;
      return ImageConvert.CreateBMP(int16_1, int16_2, imgdata, 8, MaskColor);
    }

    public static ImageConvert.L1Image LoadImage(byte[] data)
    {
      BinaryReader binaryReader = new BinaryReader((Stream) new MemoryStream(data));
      ImageConvert.L1Image l1Image;
      l1Image.x_offset = (int) binaryReader.ReadByte();
      l1Image.y_offset = (int) binaryReader.ReadByte();
      int width = (int) binaryReader.ReadByte();
      int height = (int) binaryReader.ReadByte();
      if (width == 0 || height == 0)
      {
        l1Image.image = (Bitmap) null;
      }
      else
      {
        l1Image.image = new Bitmap(width, height, PixelFormat.Format16bppRgb555);
        Rectangle rect = new Rectangle(0, 0, width, height);
        BitmapData bitmapdata = l1Image.image.LockBits(rect, ImageLockMode.WriteOnly, l1Image.image.PixelFormat);
        int stride = bitmapdata.Stride;
        byte[] source = new byte[height * stride];
        for (int index1 = 0; index1 < height; ++index1)
        {
          int num1 = (int) binaryReader.ReadByte();
          int num2 = 0;
          for (int index2 = 0; index2 < num1; ++index2)
          {
            int num3 = num2 + (int) binaryReader.ReadByte();
            int num4 = (int) binaryReader.ReadByte() * 2;
            Array.Copy((Array) binaryReader.ReadBytes(num4), 0, (Array) source, index1 * stride + num3, num4);
            num2 = num3 + num4;
          }
        }
        binaryReader.Close();
        Marshal.Copy(source, 0, bitmapdata.Scan0, source.Length);
        l1Image.image.UnlockBits(bitmapdata);
      }
      return l1Image;
    }

    public static ImageConvert.L1Image LoadImage(byte[] data, int width, int height)
    {
      Bitmap bitmap = new Bitmap(width, height);
      Graphics graphics = Graphics.FromImage((Image) bitmap);
      ImageConvert.L1Image l1Image = ImageConvert.LoadImage(data);
      if (l1Image.image != null)
        graphics.DrawImageUnscaled((Image) l1Image.image, l1Image.x_offset, l1Image.y_offset);
      l1Image.image = bitmap;
      return l1Image;
    }

    public static Bitmap Load_TBT(byte[] tbtdata)
    {
      return ImageConvert.LoadImage(tbtdata).image;
    }

    public static Bitmap Load_TIL(byte[] tbtdata)
    {
      BinaryReader binaryReader = new BinaryReader((Stream) new MemoryStream(tbtdata));
      int num1 = (int) binaryReader.ReadInt16();
      int num2 = (int) binaryReader.ReadInt16();
      int[] numArray = new int[num1 + 1];
      int num3 = 4 + numArray.Length * 4;
      for (int index = 0; index <= num1; ++index)
        numArray[index] = num3 + binaryReader.ReadInt32();
      for (int index = 0; index < num1; ++index)
      {
        binaryReader.BaseStream.Seek((long) numArray[index], SeekOrigin.Begin);
        int num4 = (int) binaryReader.ReadByte();
        ImageConvert.L1Image l1Image = ImageConvert.LoadImage(binaryReader.ReadBytes(numArray[index + 1] - (int) binaryReader.BaseStream.Position), 24, 24);
        if (l1Image.image != null)
          l1Image.image.Save(string.Format("E:\\Temp\\{0:x4}.bmp", (object) index), ImageFormat.Bmp);
      }
      binaryReader.Close();
      return (Bitmap) null;
    }

    public struct L1Image
    {
      public int x_offset;
      public int y_offset;
      public Bitmap image;
    }
  }
}
