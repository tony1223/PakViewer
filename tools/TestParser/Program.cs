using Lin.Helper.Core.Sprite;
using System;
using System.IO;
using System.Linq;

// Round-trip 測試

var files = new[] {
    @"C:\workspaces\lineage\PakViewer\resources\TW13081901 (1).txt",
    @"C:\workspaces\lineage\PakViewer\resources\TW13081901.txt",
    @"C:\workspaces\lineage\PakViewer\resources\SpriteList - 範例.txt"
};

foreach (var filePath in files)
{
    Console.WriteLine($"=== {Path.GetFileName(filePath)} ===\n");

    var content = File.ReadAllText(filePath);
    var parsed = SprListParser.Parse(content);

    // 顯示警告
    if (parsed.Warnings.Count > 0)
    {
        Console.WriteLine($"Warnings ({parsed.Warnings.Count}):");
        foreach (var w in parsed.Warnings.Take(5))
            Console.WriteLine($"  {w}");
        if (parsed.Warnings.Count > 5)
            Console.WriteLine($"  ... and {parsed.Warnings.Count - 5} more");
        Console.WriteLine();
    }

    Console.WriteLine($"Parsed: {parsed.Entries.Count} entries");

    // Serialize and re-parse
    var serialized = SprListWriter.ToStandardFormat(parsed);
    var reparsed = SprListParser.Parse(serialized);

    Console.WriteLine($"Re-parsed: {reparsed.Entries.Count} entries");

    // 比較
    int diffCount = 0;
    foreach (var e1 in parsed.Entries)
    {
        var e2 = reparsed.Entries.FirstOrDefault(e => e.Id == e1.Id);
        if (e2 == null) { diffCount++; continue; }

        if (e1.ImageCount != e2.ImageCount ||
            e1.LinkedId != e2.LinkedId ||
            e1.Actions.Count != e2.Actions.Count ||
            e1.Attributes.Count != e2.Attributes.Count)
        {
            diffCount++;
        }
    }

    int totalActions1 = parsed.Entries.Sum(e => e.Actions.Count);
    int totalActions2 = reparsed.Entries.Sum(e => e.Actions.Count);
    int totalAttrs1 = parsed.Entries.Sum(e => e.Attributes.Count);
    int totalAttrs2 = reparsed.Entries.Sum(e => e.Attributes.Count);

    Console.WriteLine($"\nActions: {totalActions1} → {totalActions2} {(totalActions1 == totalActions2 ? "✅" : "❌")}");
    Console.WriteLine($"Attrs: {totalAttrs1} → {totalAttrs2} {(totalAttrs1 == totalAttrs2 ? "✅" : "❌")}");
    Console.WriteLine($"Diff entries: {diffCount} {(diffCount == 0 ? "✅" : "❌")}");
    Console.WriteLine();
}

Console.WriteLine("=== 全部完成 ===");
