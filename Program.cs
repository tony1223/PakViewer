using System;
using System.Text;
using Eto;
using Eto.Forms;

namespace PakViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Register Big5, GB2312, Shift_JIS, EUC-KR etc. encoding support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // CLI mode - not implemented in cross-platform version
            // Use the AnalyzeMTil tool or Lin.Helper.Core library directly for CLI operations

            // GUI mode - use Eto.Forms for cross-platform
            new Application(Platform.Detect).Run(new MainForm());
        }
    }
}
