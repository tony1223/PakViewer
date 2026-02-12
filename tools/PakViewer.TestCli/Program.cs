// PakViewer.TestCli - 測試用 scratch pad
using System.Text;
using Lin.Helper.Core.Dat;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// 正確的密碼 (來自 Python 腳本)
string password = Encoding.UTF8.GetString(Convert.FromBase64String(
    "c2UsdXpSV3lnWyo9bFxsPzErMHdkICJZNF1ULCRYXnBdcVgnY21sNkg+YSFyUDhPM3ZOdTEqQT9Lays3JXJ8NGBxK3JyWzd+RjFofGc2dSBhKTsxNiNzJERbKTp8XiNtVDMxZSZ4c1dUMFY6OC1wVVQjeWZGIiRKKC1qbS1pICc="));

Console.WriteLine($"Password length: {password.Length}\n");

string clientDir = @"C:\workspaces\lineage\v381\client_m";
var contentFiles = Directory.GetFiles(clientDir, "content*.dat").OrderBy(f => f).ToArray();
Console.WriteLine($"Found {contentFiles.Length} content .dat files\n");

foreach (var path in contentFiles)
{
    string name = Path.GetFileName(path);
    try
    {
        using var dat = new MDat(path, password);
        Console.Write($"[{dat.Status,-9}] {name,-20} entries={dat.Count,5}");

        if (dat.Count > 0)
        {
            var first = dat.Entries[0];
            var data = dat.Extract(0);
            Console.Write($"  first=[{first.FileName}] size={data.Length}");
        }
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL    ] {name,-20} {ex.GetType().Name}: {ex.Message}");
    }
}
