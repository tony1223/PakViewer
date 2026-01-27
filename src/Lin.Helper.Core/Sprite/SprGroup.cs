using System.Collections.Generic;
using System.Linq;
using Lin.Helper.Core.Pak;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// SPR 群組 - 合併 xxx-xxx.spr 為一個群組
    /// </summary>
    public class SprGroup
    {
        public int SpriteId { get; set; }           // 主 ID (例: 0)
        public List<SprPart> Parts { get; set; } = new();
        public int TotalFrames => Parts.Sum(p => p.FrameCount);
        public int PartsCount => Parts.Count;
        public long TotalSize => Parts.Sum(p => p.FileSize);
    }

    /// <summary>
    /// SPR 部分 - 單一 xxx-xxx.spr 檔案
    /// </summary>
    public class SprPart
    {
        public string FileName { get; set; }        // "0-0.spr"
        public int PartIndex { get; set; }          // 0, 1, 2...
        public int FrameCount { get; set; }
        public long FileSize { get; set; }          // 檔案大小
        public PakFile SourcePak { get; set; }      // 來源 PAK 檔
        public int FileIndex { get; set; }          // 在 PAK 中的索引
    }

    /// <summary>
    /// SPR 群組列表項目 (用於 GridView 顯示)
    /// </summary>
    public class SprGroupItem
    {
        public bool? IsChecked { get; set; } = false;
        public int Id { get; set; }
        public int Parts { get; set; }
        public int Frames { get; set; }
        public long Size { get; set; }
        public string SizeText => Size < 1024 ? $"{Size} B" :
                                  Size < 1024 * 1024 ? $"{Size / 1024.0:F1} KB" :
                                  $"{Size / (1024.0 * 1024.0):F1} MB";
        public SprGroup Group { get; set; }
    }
}
