using System;
using System.IO;
using System.IO.Compression;
using System.Text;

class CheckIdx
{
    static void Main()
    {
        string idxPath = @"C:\workspaces\lineage\v381\client_815_1705042503\_compressed\Sprite.idx";
        byte[] idxData = File.ReadAllBytes(idxPath);
        
        Console.WriteLine($"IDX 檔案大小: {idxData.Length} bytes");
        Console.WriteLine($"Magic: {Encoding.ASCII.GetString(idxData, 0, 4)}");
        
        int count = BitConverter.ToInt32(idxData, 4);
        Console.WriteLine($"Record count: {count}");
        
        // 解壓縮 (跳過 2 bytes zlib header)
        byte[] compressedData = new byte[idxData.Length - 8];
        Array.Copy(idxData, 8, compressedData, 0, compressedData.Length);
        
        byte[] decompressed;
        using (var input = new MemoryStream(compressedData, 2, compressedData.Length - 2))
        using (var output = new MemoryStream())
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        {
            deflate.CopyTo(output);
            decompressed = output.ToArray();
        }
        
        Console.WriteLine($"解壓後大小: {decompressed.Length} bytes");
        Console.WriteLine($"預期大小: {count * 32} bytes");
        Console.WriteLine();
        
        // 顯示前 5 筆記錄
        Console.WriteLine("前 5 筆記錄:");
        Console.WriteLine($"{"#",-5} {"Offset",-12} {"Filename",-22} {"Uncomp",-12} {"Comp",-12}");
        Console.WriteLine(new string('-', 65));
        
        for (int i = 0; i < Math.Min(5, count); i++)
        {
            int offset = i * 32;
            uint fileOffset = BitConverter.ToUInt32(decompressed, offset);
            string fileName = Encoding.Default.GetString(decompressed, offset + 4, 20).TrimEnd('\0');
            int uncompSize = BitConverter.ToInt32(decompressed, offset + 24);
            int compSize = BitConverter.ToInt32(decompressed, offset + 28);
            
            Console.WriteLine($"{i,-5} 0x{fileOffset:X8}   {fileName,-22} {uncompSize,-12} {compSize,-12}");
        }
        
        // 顯示最後 5 筆記錄
        Console.WriteLine();
        Console.WriteLine("最後 5 筆記錄:");
        Console.WriteLine($"{"#",-5} {"Offset",-12} {"Filename",-22} {"Uncomp",-12} {"Comp",-12}");
        Console.WriteLine(new string('-', 65));
        
        for (int i = Math.Max(0, count - 5); i < count; i++)
        {
            int offset = i * 32;
            uint fileOffset = BitConverter.ToUInt32(decompressed, offset);
            string fileName = Encoding.Default.GetString(decompressed, offset + 4, 20).TrimEnd('\0');
            int uncompSize = BitConverter.ToInt32(decompressed, offset + 24);
            int compSize = BitConverter.ToInt32(decompressed, offset + 28);
            
            Console.WriteLine($"{i,-5} 0x{fileOffset:X8}   {fileName,-22} {uncompSize,-12} {compSize,-12}");
        }
        
        // 檢查 PAK 檔案大小
        string pakPath = Path.ChangeExtension(idxPath, ".pak");
        var pakInfo = new FileInfo(pakPath);
        Console.WriteLine($"\nPAK 檔案大小: {pakInfo.Length} bytes");
        
        // 計算最後一筆記錄的結束位置
        int lastOffset = (count - 1) * 32;
        uint lastFileOffset = BitConverter.ToUInt32(decompressed, lastOffset);
        int lastUncompSize = BitConverter.ToInt32(decompressed, lastOffset + 24);
        int lastCompSize = BitConverter.ToInt32(decompressed, lastOffset + 28);
        long expectedEnd = lastFileOffset + (lastCompSize > 0 ? lastCompSize : lastUncompSize);
        Console.WriteLine($"預期 PAK 結束位置: {expectedEnd} bytes");
    }
}
