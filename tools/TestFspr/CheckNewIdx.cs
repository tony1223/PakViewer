using System;
using System.IO;
using System.IO.Compression;
using System.Text;

class CheckNewIdx
{
    static void Main()
    {
        string idxPath = @"C:\workspaces\lineage\v381\client_815_1705042503\_compressed\Sprite.idx";
        byte[] idxData = File.ReadAllBytes(idxPath);
        
        Console.WriteLine($"Magic: {Encoding.ASCII.GetString(idxData, 0, 4)}");
        int count = BitConverter.ToInt32(idxData, 4);
        Console.WriteLine($"Record count: {count}");
        
        // 解壓縮
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
        
        Console.WriteLine($"\n新格式 Entry (32 bytes / 0x20):");
        Console.WriteLine($"+0x00: filename (20 bytes)");
        Console.WriteLine($"+0x14: file_offset (4 bytes)");
        Console.WriteLine($"+0x18: uncompressed_size (4 bytes)");
        Console.WriteLine($"+0x1C: compressed_size (4 bytes)");
        Console.WriteLine();
        Console.WriteLine($"{"#",-5} {"Filename",-22} {"Offset",-12} {"Uncomp",-12} {"Comp",-12}");
        Console.WriteLine(new string('-', 70));
        
        for (int i = 0; i < Math.Min(5, count); i++)
        {
            int offset = i * 32;
            string fileName = Encoding.Default.GetString(decompressed, offset, 20).TrimEnd('\0');
            uint fileOffset = BitConverter.ToUInt32(decompressed, offset + 0x14);
            int uncompSize = BitConverter.ToInt32(decompressed, offset + 0x18);
            int compSize = BitConverter.ToInt32(decompressed, offset + 0x1C);
            
            Console.WriteLine($"{i,-5} {fileName,-22} 0x{fileOffset:X8}   {uncompSize,-12} {compSize,-12}");
        }
        
        // Hex dump of first record
        Console.WriteLine($"\n第一筆記錄 hex dump:");
        for (int i = 0; i < 32; i++)
        {
            Console.Write($"{decompressed[i]:X2} ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
    }
}
