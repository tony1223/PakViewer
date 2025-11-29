using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PakViewer.Utility;

namespace PakViewer
{
  internal static class Program
  {
    // Test version of FromByteArray that matches Java's DataUtil.fromByteArray
    private static char[] FromByteArrayTest(byte[] bytes)
    {
      char[] chars = new char[bytes.Length];
      for (int i = 0; i < bytes.Length; i++)
      {
        // Java: chars[i] = (char)bytes[i] where bytes[i] is signed
        chars[i] = (char)(sbyte)bytes[i];
      }
      return chars;
    }

    // Test version of FromCharArray that matches Java's DataUtil.fromCharArray
    private static byte[] FromCharArrayTest(char[] chars)
    {
      byte[] bytes = new byte[chars.Length];
      for (int i = 0; i < chars.Length; i++)
      {
        bytes[i] = (byte)chars[i];
      }
      return bytes;
    }

    private static readonly char[] INVSBOX_TEST = new char[] { 'R', '\t', 'j', 'Õ', '0', '6', '¥', '8', '¿', '@', '£', '\u009e', '\u0081', 'ó', '×', 'û', '|', 'ã', '9', '\u0082', '\u009b', '/', 'ÿ', '\u0087', '4', '\u008e', 'C', 'D', 'Ä', 'Þ', 'é', 'Ë', 'T', '{', '\u0094', '2', '¦', 'Â', '#', '=', 'î', 'L', '\u0095', '\u000b', 'B', 'ú', 'Ã', 'N', '\b', '.', '¡', 'f', '(', 'Ù', '$', '²', 'v', '[', '¢', 'I', 'm', '\u008b', 'Ñ', '%', 'r', 'ø', 'ö', 'd', '\u0086', 'h', '\u0098', '\u0016', 'Ô', '¤', '\\', 'Ì', ']', 'e', '¶', '\u0092', 'l', 'p', 'H', 'P', 'ý', 'í', '¹', 'Ú', '^', '\u0015', 'F', 'W', '§', '\u008d', '\u009d', '\u0084', '\u0090', 'Ø', '«', '\u0000', '\u008c', '¼', 'Ó', '\n', '÷', 'ä', 'X', '\u0005', '¸', '³', 'E', '\u0006', 'Ð', ',', '\u001e', '\u008f', 'Ê', '?', '\u000f', '\u0002', 'Á', '¯', '½', '\u0003', '\u0001', '\u0013', '\u008a', 'k', ':', '\u0091', '\u0011', 'A', 'O', 'g', 'Ü', 'ê', '\u0097', 'ò', 'Ï', 'Î', 'ð', '´', 'æ', 's', '\u0096', '¬', 't', '"', 'ç', '\u00ad', '5', '\u0085', 'â', 'ù', '7', 'è', '\u001c', 'u', 'ß', 'n', 'G', 'ñ', '\u001a', 'q', '\u001d', ')', 'Å', '\u0089', 'o', '·', 'b', '\u000e', 'ª', '\u0018', '¾', '\u001b', 'ü', 'V', '>', 'K', 'Æ', 'Ò', 'y', ' ', '\u009a', 'Û', 'À', 'þ', 'x', 'Í', 'Z', 'ô', '\u001f', 'Ý', '¨', '3', '\u0088', '\u0007', 'Ç', '1', '±', '\u0012', '\u0010', 'Y', '\'', '\u0080', 'ì', '_', '`', 'Q', '\u007f', '©', '\u0019', 'µ', 'J', '\r', '-', 'å', 'z', '\u009f', '\u0093', 'É', '\u009c', 'ï', ' ', 'à', ';', 'M', '®', '*', 'õ', '°', 'È', 'ë', '»', '<', '\u0083', 'S', '\u0099', 'a', '\u0017', '+', '\u0004', '~', 'º', 'w', 'Ö', '&', 'á', 'i', '\u0014', 'c', 'U', '!', '\f', '}' };

    private static char GetInvSbox72()
    {
      return INVSBOX_TEST[72];
    }

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
