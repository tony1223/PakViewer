// Decompiled with JetBrains decompiler
// Type: PakViewer.Program
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PakViewer
{
  internal static class Program
  {
    [STAThread]
    private static void Main(string[] args)
    {
      // 註冊 Big5, GB2312, Shift_JIS, EUC-KR 等編碼支援
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      // CLI 模式
      if (args.Length > 0 && args[0] == "-cli")
      {
        // 移除 -cli 參數，傳遞剩餘參數給 PakReader
        var cliArgs = args.Skip(1).ToArray();
        PakReader.Exec(cliArgs);
        return;
      }

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run((Form) new frmMain());
    }
  }
}
