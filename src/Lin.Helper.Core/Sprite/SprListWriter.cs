using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// SPR List 檔案輸出器
    /// 支援標準格式和壓縮格式輸出
    /// </summary>
    public static class SprListWriter
    {
        #region Frame 層級

        /// <summary>
        /// 輸出 Frame 的 modifiers 字串
        /// </summary>
        public static string FrameModifiersToString(SprActionFrame frame)
        {
            var sb = new StringBuilder();
            if (frame.TriggerHit) sb.Append("!");
            foreach (var s in frame.SoundIds) sb.Append($"[{s}");
            foreach (var o in frame.OverlayIds) sb.Append($"]{o}");
            foreach (var e in frame.EffectIds) sb.Append($"<{e}");
            if (frame.SkipFrame) sb.Append(">");
            return sb.ToString();
        }

        /// <summary>
        /// 輸出 Frame 為標準格式 (例: 0.1:4!)
        /// </summary>
        public static string FrameToStandardFormat(SprActionFrame frame)
        {
            return $"{frame.ImageId}.{frame.FrameIndex}:{frame.Duration}{FrameModifiersToString(frame)}";
        }

        /// <summary>
        /// 輸出 Frame 為壓縮格式 (例: 0 1 4!)
        /// </summary>
        public static string FrameToCompactFormat(SprActionFrame frame)
        {
            return $"{frame.ImageId} {frame.FrameIndex} {frame.Duration}{FrameModifiersToString(frame)}";
        }

        #endregion

        #region Action 層級

        /// <summary>
        /// 輸出 Action 為標準格式
        /// 例: 0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)
        /// </summary>
        public static string ActionToStandardFormat(SprAction action)
        {
            var frames = string.Join(" ", action.Frames.Select(FrameToStandardFormat));
            return $"{action.ActionId}.{action.ActionName}({action.Directional} {action.FrameCount},{frames})";
        }

        /// <summary>
        /// 輸出 Action 為壓縮格式
        /// 例: 0 1 4 0 0 4 0 1 4 0 2 4 0 3 4
        /// </summary>
        public static string ActionToCompactFormat(SprAction action)
        {
            var sb = new StringBuilder();
            sb.Append($"{action.ActionId} {action.Directional} {action.FrameCount}");
            foreach (var frame in action.Frames)
            {
                sb.Append($" {FrameToCompactFormat(frame)}");
            }
            return sb.ToString();
        }

        #endregion

        #region Attribute 層級

        /// <summary>
        /// 輸出 Attribute 為標準格式
        /// 例: 101.shadow(100)
        /// </summary>
        public static string AttributeToStandardFormat(SprAttribute attr)
        {
            var paramsStr = attr.Parameters.Count > 0
                ? string.Join(" ", attr.Parameters)
                : attr.RawParameters ?? "";
            return $"{attr.AttributeId}.{attr.AttributeName}({paramsStr})";
        }

        /// <summary>
        /// 輸出 Attribute 為壓縮格式
        /// 例: 101 100
        /// </summary>
        public static string AttributeToCompactFormat(SprAttribute attr)
        {
            var paramsStr = attr.Parameters.Count > 0
                ? string.Join(" ", attr.Parameters)
                : attr.RawParameters ?? "";
            return string.IsNullOrEmpty(paramsStr)
                ? $"{attr.AttributeId}"
                : $"{attr.AttributeId} {paramsStr}";
        }

        #endregion

        #region Entry 層級

        /// <summary>
        /// 從 entry.Name 中提取純名稱（移除可能混入的 actions/attributes 文字）
        /// </summary>
        private static string ExtractCleanName(SprListEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Name))
                return $"#{entry.Id}";

            var name = entry.Name;

            // 找到第一個 action/attribute pattern (數字.字母)
            var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+\.[a-zA-Z]");
            if (match.Success)
                name = name.Substring(0, match.Index).Trim();

            return string.IsNullOrWhiteSpace(name) ? $"#{entry.Id}" : name;
        }

        /// <summary>
        /// 輸出 Entry 為標準格式（多行）
        /// </summary>
        public static string EntryToStandardFormat(SprListEntry entry)
        {
            var sb = new StringBuilder();

            // Header: #0 312=3225 prince
            sb.Append($"#{entry.Id}\t{entry.ImageCount}");
            if (entry.LinkedId.HasValue)
                sb.Append($"={entry.LinkedId.Value}");
            sb.Append($"\t{ExtractCleanName(entry)}");

            // Actions (每個一行，縮排)
            foreach (var action in entry.Actions)
            {
                sb.AppendLine();
                sb.Append($"\t{ActionToStandardFormat(action)}");
            }

            // Attributes (接在最後，空格分隔)
            foreach (var attr in entry.Attributes)
            {
                sb.Append($" {AttributeToStandardFormat(attr)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 輸出 Entry 為壓縮格式（單行）
        /// </summary>
        public static string EntryToCompactFormat(SprListEntry entry)
        {
            var sb = new StringBuilder();

            // Header: #0 312=3225
            sb.Append($"#{entry.Id} {entry.ImageCount}");
            if (entry.LinkedId.HasValue)
                sb.Append($"={entry.LinkedId.Value}");

            // Actions (空格分隔)
            foreach (var action in entry.Actions)
            {
                sb.Append($" {ActionToCompactFormat(action)}");
            }

            // Attributes (空格分隔)
            foreach (var attr in entry.Attributes)
            {
                sb.Append($" {AttributeToCompactFormat(attr)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 儲存單一 Entry 到 .sprtxt 檔案
        /// </summary>
        public static void SaveEntry(SprListEntry entry, string filePath, bool compact = false)
        {
            var content = compact
                ? EntryToCompactFormat(entry)
                : EntryToStandardFormat(entry);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        /// <summary>
        /// 從 .sprtxt 檔案載入單一 Entry
        /// </summary>
        public static SprListEntry LoadEntry(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到檔案", filePath);

            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseEntry(content);
        }

        /// <summary>
        /// 從字串解析單一 Entry
        /// </summary>
        public static SprListEntry ParseEntry(string entryText)
        {
            // 建立一個臨時的 SprListFile 來解析
            var tempContent = $"1 0 0\n{entryText}";
            var file = SprListParser.Parse(tempContent);
            return file.Entries.FirstOrDefault();
        }

        #endregion

        #region File 層級

        /// <summary>
        /// 輸出整個檔案為標準格式
        /// </summary>
        public static string ToStandardFormat(SprListFile file)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine($"{file.TotalEntries} {file.Unknown1} {file.Unknown2}");

            // Entries
            foreach (var entry in file.Entries)
            {
                sb.AppendLine(EntryToStandardFormat(entry));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 輸出整個檔案為壓縮格式
        /// </summary>
        public static string ToCompactFormat(SprListFile file)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine($"{file.TotalEntries} {file.Unknown1} {file.Unknown2}");

            // Entries (每個一行)
            foreach (var entry in file.Entries)
            {
                sb.AppendLine(EntryToCompactFormat(entry));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 儲存到檔案
        /// </summary>
        public static void SaveToFile(SprListFile file, string filePath, bool compact = false)
        {
            var content = compact ? ToCompactFormat(file) : ToStandardFormat(file);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        /// <summary>
        /// 輸出為 byte[]
        /// </summary>
        public static byte[] ToBytes(SprListFile file, bool compact = false)
        {
            var content = compact ? ToCompactFormat(file) : ToStandardFormat(file);
            return Encoding.UTF8.GetBytes(content);
        }

        #endregion

        #region 組裝功能

        /// <summary>
        /// 從多個 Entry 組裝成 SprListFile
        /// </summary>
        public static SprListFile AssembleFromEntries(IEnumerable<SprListEntry> entries)
        {
            var entryList = entries.ToList();
            return new SprListFile
            {
                TotalEntries = entryList.Count,
                Unknown1 = 0,
                Unknown2 = 0,
                Entries = entryList
            };
        }

        /// <summary>
        /// 合併多個 SprListFile（相同 ID 會被後面的覆蓋）
        /// </summary>
        public static SprListFile Merge(params SprListFile[] files)
        {
            var entryDict = new Dictionary<int, SprListEntry>();

            foreach (var file in files)
            {
                foreach (var entry in file.Entries)
                {
                    entryDict[entry.Id] = entry;
                }
            }

            var mergedEntries = entryDict.Values.OrderBy(e => e.Id).ToList();

            return new SprListFile
            {
                TotalEntries = mergedEntries.Count,
                Unknown1 = files.FirstOrDefault()?.Unknown1 ?? 0,
                Unknown2 = files.FirstOrDefault()?.Unknown2 ?? 0,
                Entries = mergedEntries
            };
        }

        /// <summary>
        /// 合併多個 SprListFile，可選擇衝突處理策略
        /// </summary>
        public static SprListFile Merge(MergeConflictStrategy strategy, params SprListFile[] files)
        {
            var entryDict = new Dictionary<int, SprListEntry>();

            foreach (var file in files)
            {
                foreach (var entry in file.Entries)
                {
                    if (entryDict.ContainsKey(entry.Id))
                    {
                        switch (strategy)
                        {
                            case MergeConflictStrategy.Overwrite:
                                entryDict[entry.Id] = entry;
                                break;
                            case MergeConflictStrategy.Skip:
                                // 保留原有的，跳過新的
                                break;
                            case MergeConflictStrategy.Rename:
                                // 找一個新的 ID
                                int newId = entryDict.Keys.Max() + 1;
                                var clonedEntry = CloneEntry(entry);
                                clonedEntry.Id = newId;
                                entryDict[newId] = clonedEntry;
                                break;
                        }
                    }
                    else
                    {
                        entryDict[entry.Id] = entry;
                    }
                }
            }

            var mergedEntries = entryDict.Values.OrderBy(e => e.Id).ToList();

            return new SprListFile
            {
                TotalEntries = mergedEntries.Count,
                Unknown1 = files.FirstOrDefault()?.Unknown1 ?? 0,
                Unknown2 = files.FirstOrDefault()?.Unknown2 ?? 0,
                Entries = mergedEntries
            };
        }

        /// <summary>
        /// 複製 Entry（深拷貝）
        /// </summary>
        public static SprListEntry CloneEntry(SprListEntry source)
        {
            return new SprListEntry
            {
                Id = source.Id,
                ImageCount = source.ImageCount,
                LinkedId = source.LinkedId,
                Name = source.Name,
                RawText = source.RawText,
                Actions = source.Actions.Select(a => new SprAction
                {
                    ActionId = a.ActionId,
                    ActionName = a.ActionName,
                    Directional = a.Directional,
                    FrameCount = a.FrameCount,
                    RawText = a.RawText,
                    Frames = a.Frames.Select(f => new SprActionFrame
                    {
                        ImageId = f.ImageId,
                        FrameIndex = f.FrameIndex,
                        Duration = f.Duration,
                        TriggerHit = f.TriggerHit,
                        SkipFrame = f.SkipFrame,
                        RawText = f.RawText,
                        SoundIds = new List<int>(f.SoundIds),
                        OverlayIds = new List<int>(f.OverlayIds),
                        EffectIds = new List<int>(f.EffectIds)
                    }).ToList()
                }).ToList(),
                Attributes = source.Attributes.Select(attr => new SprAttribute
                {
                    AttributeId = attr.AttributeId,
                    AttributeName = attr.AttributeName,
                    RawParameters = attr.RawParameters,
                    Parameters = new List<string>(attr.Parameters)
                }).ToList()
            };
        }

        #endregion
    }

    /// <summary>
    /// 合併衝突處理策略
    /// </summary>
    public enum MergeConflictStrategy
    {
        /// <summary>覆蓋：後面的覆蓋前面的</summary>
        Overwrite,
        /// <summary>跳過：保留前面的，跳過後面的</summary>
        Skip,
        /// <summary>重新編號：給衝突的 Entry 分配新 ID</summary>
        Rename
    }
}
