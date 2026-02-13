// PakViewer.TestCli - 分析 oldtils 差異: 挑 2524.til 看哪裡不同
using System.Text;
using Lin.Helper.Core.Dat;
using Lin.Helper.Core.Tile;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

string oldTilsDir = @"C:\workspaces\lineage\PakViewer\resources\oldtils";
string datDir = @"C:\workspaces\lineage\v381\client_m";

// 建索引
var datFiles = Directory.GetFiles(datDir, "Tile*.dat");
var ti2Index = new Dictionary<string, (string datFile, MDatEntry entry)>(StringComparer.OrdinalIgnoreCase);
foreach (var datFile in datFiles)
{
    try
    {
        using var mdat = new MDat(datFile);
        foreach (var entry in mdat.Entries)
        {
            var fn = Path.GetFileName(entry.FileName);
            if (fn.EndsWith(".ti2", StringComparison.OrdinalIgnoreCase))
                ti2Index[Path.GetFileNameWithoutExtension(fn)] = (datFile, entry);
        }
    }
    catch { }
}

// 比對 2524
string id = "2524";
var correctData = File.ReadAllBytes(Path.Combine(oldTilsDir, $"{id}.til"));
var (df, ent) = ti2Index[id];
byte[] ti2Data;
using (var mdat = new MDat(df)) { ti2Data = mdat.Extract(ent); }

var tileBlocks = MTil.ConvertToL1Til(ti2Data);
var tilBytes = L1Til.BuildTilFromTileBlocks(tileBlocks);

Console.WriteLine($"Converted: {tilBytes.Length}, Correct: {correctData.Length}");

var newTb = L1Til.ParseToTileBlocks(tilBytes, validateFormat: false);
var correctTb = L1Til.ParseToTileBlocks(correctData, validateFormat: false);

// Block-by-block
int same = 0, typeDiff = 0, contentDiff = 0, sizeDiff = 0;
for (int i = 0; i < 256; i++)
{
    var nb = newTb.Get(i);
    var cb = correctTb.Get(i);
    if (nb.SequenceEqual(cb)) { same++; continue; }

    if (nb.Length != cb.Length)
    {
        sizeDiff++;
        if (sizeDiff <= 3)
            Console.WriteLine($"  [{i}] SIZE DIFF: new={nb.Length} correct={cb.Length}");
        continue;
    }

    // Same length, check if only first byte differs
    bool onlyFirst = true;
    for (int j = 1; j < nb.Length; j++)
        if (nb[j] != cb[j]) { onlyFirst = false; break; }

    if (onlyFirst)
    {
        typeDiff++;
        if (typeDiff <= 5)
            Console.WriteLine($"  [{i}] TYPE DIFF: new=0x{nb[0]:X2} correct=0x{cb[0]:X2} diff=0x{(nb[0]^cb[0]):X2}");
    }
    else
    {
        contentDiff++;
        if (contentDiff <= 3)
        {
            // 找出差異位置
            int diffBytes = 0;
            int firstDiffPos = -1;
            for (int j = 0; j < nb.Length; j++)
            {
                if (nb[j] != cb[j])
                {
                    diffBytes++;
                    if (firstDiffPos < 0) firstDiffPos = j;
                }
            }
            Console.WriteLine($"  [{i}] CONTENT DIFF: size={nb.Length} diffBytes={diffBytes} firstAt={firstDiffPos} newType=0x{nb[0]:X2} correctType=0x{cb[0]:X2}");
            Console.WriteLine($"    new:     {BitConverter.ToString(nb, 0, Math.Min(20, nb.Length))}...");
            Console.WriteLine($"    correct: {BitConverter.ToString(cb, 0, Math.Min(20, cb.Length))}...");
        }
    }
}

Console.WriteLine($"\nSame: {same}, TypeOnly: {typeDiff}, ContentDiff: {contentDiff}, SizeDiff: {sizeDiff}");
