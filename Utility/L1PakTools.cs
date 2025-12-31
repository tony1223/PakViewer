// L1PakTools - UI 適配層
// 實際加解密邏輯由 Lin.Helper.Core.Pak.PakTools 提供

using PakViewer.Properties;
using System;
using System.Text;
using System.Windows.Forms;
using CorePakTools = Lin.Helper.Core.Pak.PakTools;

namespace PakViewer.Utility
{
  /// <summary>
  /// PAK 工具 (UI 適配層)
  /// </summary>
  public static class L1PakTools
  {
    private static ToolStripProgressBar _progressbar;

    static L1PakTools()
    {
      // 初始化 Core 的加密表
      CorePakTools.SetMaps(Resources.Map1, Resources.Map2, Resources.Map3, Resources.Map4, Resources.Map5);
    }

    /// <summary>
    /// 設定進度條控件 (UI 專用)
    /// </summary>
    public static void ShowProgress(ToolStripProgressBar obj)
    {
      _progressbar = obj;
    }

    /// <summary>
    /// 加密資料
    /// </summary>
    public static byte[] Encode(byte[] src, int index)
    {
      var progress = CreateProgress(src.Length);
      return CorePakTools.Encode(src, index, progress);
    }

    /// <summary>
    /// 解密資料
    /// </summary>
    public static byte[] Decode(byte[] src, int index)
    {
      var progress = CreateProgress(src.Length);
      return CorePakTools.Decode(src, index, progress);
    }

    /// <summary>
    /// 解碼索引檔的第一筆記錄
    /// </summary>
    public static IndexRecord Decode_Index_FirstRecord(byte[] src)
    {
      byte[] src1 = new byte[36];
      Array.Copy(src, src1, src1.Length);
      return new IndexRecord(Decode(src1, 4), 0);
    }

    /// <summary>
    /// 建立 IProgress 適配器
    /// </summary>
    private static IProgress<int> CreateProgress(int totalSize)
    {
      if (_progressbar == null) return null;

      _progressbar.Maximum = 100;
      _progressbar.Value = 0;

      return new Progress<int>(percent =>
      {
        if (_progressbar != null && !_progressbar.IsDisposed)
        {
          _progressbar.Value = Math.Min(percent, 100);
        }
      });
    }

    /// <summary>
    /// PAK 索引記錄
    /// </summary>
    public struct IndexRecord
    {
      public int Offset;
      public string FileName;
      public int FileSize;
      public string SourcePak;

      public IndexRecord(byte[] data, int index)
      {
        Offset = BitConverter.ToInt32(data, index);
        FileName = Encoding.Default.GetString(data, index + 4, 20).TrimEnd('\0');
        FileSize = BitConverter.ToInt32(data, index + 24);
        SourcePak = null;
      }

      public IndexRecord(string filename, int size, int offset)
      {
        Offset = offset;
        FileName = filename;
        FileSize = size;
        SourcePak = null;
      }

      public IndexRecord(string filename, int size, int offset, string sourcePak)
      {
        Offset = offset;
        FileName = filename;
        FileSize = size;
        SourcePak = sourcePak;
      }
    }
  }
}
