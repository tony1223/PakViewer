using System;
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

      // GUI 模式 (CLI 功能已移至 lin-pack.exe)
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run((Form) new frmMain());
    }
  }
}
