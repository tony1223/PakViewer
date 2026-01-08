using System.Text;

namespace LinPack
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Register Big5, GB2312, Shift_JIS, EUC-KR encoding support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Call PakReader CLI handler
            PakViewer.PakReader.Exec(args);
        }
    }
}
