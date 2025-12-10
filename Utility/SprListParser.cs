using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PakViewer.Models;

namespace PakViewer.Utility
{
    /// <summary>
    /// SPR List 檔案解析器 (list.spr / wlist.spr)
    /// </summary>
    public static class SprListParser
    {
        // 主條目模式: #0 312=3225 prince 或 #2 1 axe 102.type(9)
        private static readonly Regex EntryHeaderPattern = new Regex(
            @"^#(\d+)\s+(\d+)(?:=(\d+))?\s+(.+)$",
            RegexOptions.Compiled);

        // 動作定義模式: 0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)
        private static readonly Regex ActionPattern = new Regex(
            @"(\d+)\.([a-zA-Z_][a-zA-Z0-9_\s]*)\((\d+)\s+(\d+),([^)]+)\)",
            RegexOptions.Compiled);

        // 屬性定義模式: 101.shadow(3226) 或 102.type(5)
        private static readonly Regex AttributePattern = new Regex(
            @"(\d+)\.([a-zA-Z_][a-zA-Z0-9_\s]*)\(([^)]*)\)",
            RegexOptions.Compiled);

        // 幀定義模式: 32.0:3 或 32.0:3! 或 32.0:3[248 等
        private static readonly Regex FramePattern = new Regex(
            @"(\d+)\.(\d+):(\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// 從檔案路徑載入並解析 SPR List 檔案
        /// </summary>
        public static SprListFile LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到檔案", filePath);

            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return Parse(content);
        }

        /// <summary>
        /// 從 byte[] 載入並解析 SPR List 檔案
        /// </summary>
        public static SprListFile LoadFromBytes(byte[] data)
        {
            // 嘗試不同編碼
            string content;
            try
            {
                content = Encoding.UTF8.GetString(data);
            }
            catch
            {
                content = Encoding.GetEncoding("big5").GetString(data);
            }
            return Parse(content);
        }

        /// <summary>
        /// 解析 SPR List 內容
        /// </summary>
        public static SprListFile Parse(string content)
        {
            var result = new SprListFile();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                return result;

            // 解析第一行: 總條目數 未知1 未知2
            var headerParts = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (headerParts.Length >= 1 && int.TryParse(headerParts[0], out int total))
                result.TotalEntries = total;
            if (headerParts.Length >= 2 && int.TryParse(headerParts[1], out int u1))
                result.Unknown1 = u1;
            if (headerParts.Length >= 3 && int.TryParse(headerParts[2], out int u2))
                result.Unknown2 = u2;

            // 解析條目
            SprListEntry currentEntry = null;
            var currentEntryLines = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.TrimStart();

                // 檢查是否為新條目開始 (以 # 開頭)
                if (trimmedLine.StartsWith("#"))
                {
                    // 儲存前一個條目
                    if (currentEntry != null)
                    {
                        ParseEntryContent(currentEntry, currentEntryLines);
                        result.Entries.Add(currentEntry);
                    }

                    // 開始新條目
                    currentEntry = ParseEntryHeader(trimmedLine);
                    currentEntryLines.Clear();

                    // 如果主行還有其他內容 (動作或屬性)，加入內容列表
                    var restOfLine = GetRestOfEntryLine(trimmedLine);
                    if (!string.IsNullOrWhiteSpace(restOfLine))
                    {
                        currentEntryLines.Add(restOfLine);
                    }
                }
                else if (currentEntry != null && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // 續行 (以 tab 或空格開頭)
                    currentEntryLines.Add(trimmedLine);
                }
            }

            // 儲存最後一個條目
            if (currentEntry != null)
            {
                ParseEntryContent(currentEntry, currentEntryLines);
                result.Entries.Add(currentEntry);
            }

            return result;
        }

        /// <summary>
        /// 解析條目標頭
        /// </summary>
        private static SprListEntry ParseEntryHeader(string line)
        {
            var entry = new SprListEntry { RawText = line };

            // 嘗試匹配標準格式
            var match = EntryHeaderPattern.Match(line);
            if (match.Success)
            {
                entry.Id = int.Parse(match.Groups[1].Value);
                entry.ImageCount = int.Parse(match.Groups[2].Value);
                if (!string.IsNullOrEmpty(match.Groups[3].Value))
                    entry.LinkedId = int.Parse(match.Groups[3].Value);
                entry.Name = match.Groups[4].Value.Trim();
            }
            else
            {
                // 嘗試簡化解析
                var parts = line.TrimStart('#').Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && int.TryParse(parts[0], out int id))
                    entry.Id = id;
                if (parts.Length >= 2)
                {
                    var countPart = parts[1];
                    if (countPart.Contains("="))
                    {
                        var countParts = countPart.Split('=');
                        if (int.TryParse(countParts[0], out int count))
                            entry.ImageCount = count;
                        if (countParts.Length > 1 && int.TryParse(countParts[1], out int linked))
                            entry.LinkedId = linked;
                    }
                    else if (int.TryParse(countPart, out int count))
                    {
                        entry.ImageCount = count;
                    }
                }
                if (parts.Length >= 3)
                {
                    // 名稱可能包含空格，找到第一個動作或屬性定義之前的部分
                    var nameBuilder = new StringBuilder();
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (Regex.IsMatch(parts[i], @"^\d+\."))
                            break;
                        if (nameBuilder.Length > 0) nameBuilder.Append(" ");
                        nameBuilder.Append(parts[i]);
                    }
                    entry.Name = nameBuilder.ToString();
                }
            }

            return entry;
        }

        /// <summary>
        /// 取得條目行中除了 #id count name 之外的部分
        /// </summary>
        private static string GetRestOfEntryLine(string line)
        {
            // 找到第一個動作或屬性定義
            var actionMatch = ActionPattern.Match(line);
            var attrMatch = AttributePattern.Match(line);

            int startPos = -1;
            if (actionMatch.Success)
                startPos = actionMatch.Index;
            if (attrMatch.Success && (startPos < 0 || attrMatch.Index < startPos))
                startPos = attrMatch.Index;

            if (startPos > 0)
                return line.Substring(startPos);

            return null;
        }

        /// <summary>
        /// 解析條目內容 (動作和屬性)
        /// </summary>
        private static void ParseEntryContent(SprListEntry entry, List<string> contentLines)
        {
            // 合併所有內容行
            var fullContent = string.Join(" ", contentLines);

            // 解析動作
            var actionMatches = ActionPattern.Matches(fullContent);
            foreach (Match match in actionMatches)
            {
                var action = ParseAction(match);
                if (action != null)
                    entry.Actions.Add(action);
            }

            // 解析屬性 (排除已被解析為動作的部分)
            var attrMatches = AttributePattern.Matches(fullContent);
            foreach (Match match in attrMatches)
            {
                int attrId = int.Parse(match.Groups[1].Value);
                // 屬性 ID 通常 >= 100
                if (attrId >= 100)
                {
                    var attr = ParseAttribute(match);
                    if (attr != null)
                        entry.Attributes.Add(attr);
                }
            }
        }

        /// <summary>
        /// 解析動作定義
        /// </summary>
        private static SprAction ParseAction(Match match)
        {
            var action = new SprAction
            {
                RawText = match.Value,
                ActionId = int.Parse(match.Groups[1].Value),
                ActionName = match.Groups[2].Value.Trim(),
                Directional = int.Parse(match.Groups[3].Value),
                FrameCount = int.Parse(match.Groups[4].Value)
            };

            // 解析幀序列
            var framesStr = match.Groups[5].Value;
            action.Frames = ParseFrames(framesStr);

            return action;
        }

        /// <summary>
        /// 解析幀序列
        /// </summary>
        private static List<SprFrame> ParseFrames(string framesStr)
        {
            var frames = new List<SprFrame>();

            // 分割幀 (以空格分隔)
            var frameParts = framesStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in frameParts)
            {
                var frame = ParseFrame(part);
                if (frame != null)
                    frames.Add(frame);
            }

            return frames;
        }

        /// <summary>
        /// 解析單一幀
        /// </summary>
        private static SprFrame ParseFrame(string frameStr)
        {
            var frame = new SprFrame { RawText = frameStr };

            // 基本格式: ImageId.FrameIndex:Duration
            var basicMatch = FramePattern.Match(frameStr);
            if (!basicMatch.Success)
                return null;

            frame.ImageId = int.Parse(basicMatch.Groups[1].Value);
            frame.FrameIndex = int.Parse(basicMatch.Groups[2].Value);
            frame.Duration = int.Parse(basicMatch.Groups[3].Value);

            // 解析修飾符
            var restStr = frameStr.Substring(basicMatch.Length);

            // ! - 觸發命中
            if (restStr.Contains("!"))
                frame.TriggerHit = true;

            // > - 跳格移動
            if (restStr.Contains(">"))
                frame.SkipFrame = true;

            // [ - 聲音
            var soundMatches = Regex.Matches(restStr, @"\[(\d+)");
            foreach (Match m in soundMatches)
                frame.SoundIds.Add(int.Parse(m.Groups[1].Value));

            // ] - 疊加圖檔
            var overlayMatches = Regex.Matches(restStr, @"\](\d+)");
            foreach (Match m in overlayMatches)
                frame.OverlayIds.Add(int.Parse(m.Groups[1].Value));

            // < - 特效
            var effectMatches = Regex.Matches(restStr, @"<(\d+)");
            foreach (Match m in effectMatches)
                frame.EffectIds.Add(int.Parse(m.Groups[1].Value));

            return frame;
        }

        /// <summary>
        /// 解析屬性定義
        /// </summary>
        private static SprAttribute ParseAttribute(Match match)
        {
            var attr = new SprAttribute
            {
                AttributeId = int.Parse(match.Groups[1].Value),
                AttributeName = match.Groups[2].Value.Trim(),
                RawParameters = match.Groups[3].Value
            };

            // 解析參數
            var paramsStr = match.Groups[3].Value;
            if (!string.IsNullOrWhiteSpace(paramsStr))
            {
                var parts = paramsStr.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                attr.Parameters.AddRange(parts);
            }

            return attr;
        }

        /// <summary>
        /// 將 SprListFile 輸出為字串 (用於儲存)
        /// </summary>
        public static string ToFileContent(SprListFile file)
        {
            var sb = new StringBuilder();

            // 輸出標頭
            sb.AppendLine($"{file.TotalEntries} {file.Unknown1} {file.Unknown2}");

            // 輸出條目
            foreach (var entry in file.Entries)
            {
                sb.Append($"#{entry.Id}\t{entry.ImageCount}");
                if (entry.LinkedId.HasValue)
                    sb.Append($"={entry.LinkedId.Value}");
                sb.Append($"\t{entry.Name}");

                // 輸出動作
                foreach (var action in entry.Actions)
                {
                    sb.AppendLine();
                    sb.Append($"\t{action.ActionId}.{action.ActionName}({action.Directional} {action.FrameCount},");
                    sb.Append(string.Join(" ", action.Frames.ConvertAll(f => f.RawText ?? f.ToString())));
                    sb.Append(")");
                }

                // 輸出屬性
                foreach (var attr in entry.Attributes)
                {
                    sb.Append($" {attr.AttributeId}.{attr.AttributeName}({attr.RawParameters})");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
