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
    /// 支援標準格式和壓縮格式（無 name）
    /// </summary>
    public static class SprListParser
    {
        // 主條目模式: #0 312=3225 prince 或 #2 1 axe 102.type(9)
        private static readonly Regex EntryHeaderPattern = new Regex(
            @"^#(\d+)\s+(\d+)(?:=(\d+))?\s+(.+)$",
            RegexOptions.Compiled);

        // 壓縮格式條目模式: #0 312=3225 (無 name)
        private static readonly Regex CompactEntryHeaderPattern = new Regex(
            @"^#(\d+)\s+(\d+)(?:=(\d+))?$",
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

        // 壓縮格式幀修飾符: 如 4<479, 4!, 4[248 等
        private static readonly Regex CompactFrameModifierPattern = new Regex(
            @"(\d+)((?:[<\[\]!>][\d]*)+)?",
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
        /// 解析 SPR List 內容 (自動偵測標準或壓縮格式)
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

            // 偵測是否為壓縮格式 (檢查第二行是否為 #id count 沒有 name)
            bool isCompactFormat = DetectCompactFormat(lines);

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
                        if (isCompactFormat)
                            ParseCompactEntryContent(currentEntry, currentEntryLines);
                        else
                            ParseEntryContent(currentEntry, currentEntryLines);
                        result.Entries.Add(currentEntry);
                    }

                    // 開始新條目
                    currentEntry = ParseEntryHeader(trimmedLine, isCompactFormat);
                    currentEntryLines.Clear();

                    // 如果主行還有其他內容 (動作或屬性)，加入內容列表
                    var restOfLine = GetRestOfEntryLine(trimmedLine, isCompactFormat);
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
                if (isCompactFormat)
                    ParseCompactEntryContent(currentEntry, currentEntryLines);
                else
                    ParseEntryContent(currentEntry, currentEntryLines);
                result.Entries.Add(currentEntry);
            }

            return result;
        }

        /// <summary>
        /// 偵測是否為壓縮格式
        /// </summary>
        private static bool DetectCompactFormat(string[] lines)
        {
            // 找第一個 # 開頭的行
            for (int i = 1; i < lines.Length && i < 10; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("#"))
                {
                    // 壓縮格式: #0 312=3225 (沒有 name，只有 #id count 或 #id count=linked)
                    // 標準格式: #0 312=3225 prince (有 name)
                    if (CompactEntryHeaderPattern.IsMatch(line))
                        return true;
                    if (EntryHeaderPattern.IsMatch(line))
                        return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 解析條目標頭
        /// </summary>
        private static SprListEntry ParseEntryHeader(string line, bool isCompactFormat = false)
        {
            var entry = new SprListEntry { RawText = line };

            if (isCompactFormat)
            {
                // 壓縮格式: #0 312=3225 或 #2 1 102 9 (後面可能接內嵌的動作或屬性)
                var match = CompactEntryHeaderPattern.Match(line);
                if (match.Success)
                {
                    entry.Id = int.Parse(match.Groups[1].Value);
                    entry.ImageCount = int.Parse(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                        entry.LinkedId = int.Parse(match.Groups[3].Value);
                    entry.Name = $"#{entry.Id}"; // 壓縮格式沒有名稱，用 ID 作為名稱
                }
                else
                {
                    // 嘗試簡化解析壓縮格式
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
                    entry.Name = $"#{entry.Id}";
                }
                return entry;
            }

            // 標準格式解析
            var stdMatch = EntryHeaderPattern.Match(line);
            if (stdMatch.Success)
            {
                entry.Id = int.Parse(stdMatch.Groups[1].Value);
                entry.ImageCount = int.Parse(stdMatch.Groups[2].Value);
                if (!string.IsNullOrEmpty(stdMatch.Groups[3].Value))
                    entry.LinkedId = int.Parse(stdMatch.Groups[3].Value);
                entry.Name = stdMatch.Groups[4].Value.Trim();
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
        private static string GetRestOfEntryLine(string line, bool isCompactFormat = false)
        {
            if (isCompactFormat)
            {
                // 壓縮格式: #2 1 102 9 -> 取得 "102 9" 部分 (如果 id >= 100 是屬性)
                // 或 #10 1 0 0 6 0 5 ... -> 取得 "0 0 6 0 5 ..." 部分 (動作/屬性內嵌)
                var parts = line.TrimStart('#').Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2)
                {
                    // 跳過 id 和 count (可能包含 =linked)
                    return string.Join(" ", parts, 2, parts.Length - 2);
                }
                return null;
            }

            // 標準格式: 找到第一個動作或屬性定義
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
        /// 解析條目內容 (動作和屬性) - 標準格式
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
        /// 解析條目內容 (動作和屬性) - 壓縮格式
        /// 壓縮格式範例:
        /// 0 1 4 0 0 4&lt;479 0 1 4 0 2 4&lt;478 0 3 4  (動作行)
        /// 101 3226  (屬性行)
        /// 102 5     (屬性行)
        /// 或者全部在同一行：
        /// 1 1 5 40 0 5 ... 3 1 4 8 0 6 ... 101 128 102 10
        /// </summary>
        private static void ParseCompactEntryContent(SprListEntry entry, List<string> contentLines)
        {
            // 合併所有內容行成一個字串來處理（因為壓縮格式可能把多個動作/屬性寫在同一行）
            var fullContent = string.Join(" ", contentLines);
            var parts = fullContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return;

            int i = 0;
            while (i < parts.Length)
            {
                // 嘗試解析第一個數字
                if (!int.TryParse(ExtractLeadingNumber(parts[i]), out int firstNum))
                {
                    i++;
                    continue;
                }

                // 判斷是動作還是屬性
                // 屬性 ID >= 100
                if (firstNum >= 100)
                {
                    // 解析屬性：AttributeId 後面跟著參數
                    // 已知的屬性格式：
                    //   100 x y z w - switch (4個參數，最後一個是 ImageCount)
                    //   101 shadowId - shadow (1個參數)
                    //   102 typeId - type (1個參數)
                    //   104 filterValues... - filter (不定參數)
                    //   105 count id1 id2... - attach (count + count 個 id)
                    //   106 weaponId - weapon (1個參數)
                    //   107 sizeValue - size (1個參數)
                    //   108 flyValue - fly (1個參數)
                    //   109 effectId - magic_effect (1個參數)
                    //   110 speedValue - speed (1個參數)
                    //   111 skipValue - skip (1個參數)
                    var attrParts = new List<string> { parts[i] };
                    i++;

                    // 根據屬性類型收集固定數量的參數
                    int expectedParams = GetAttributeParamCount(firstNum);

                    // 特殊處理 105 (attach): 第一個參數是數量，後面是該數量的 attachId
                    if (firstNum == 105 && i < parts.Length)
                    {
                        // 讀取 count
                        attrParts.Add(parts[i]);
                        if (int.TryParse(parts[i], out int attachCount))
                        {
                            i++;
                            // 讀取 attachCount 個 attachId
                            for (int j = 0; j < attachCount && i < parts.Length; j++)
                            {
                                attrParts.Add(parts[i]);
                                i++;
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }
                    // 特殊處理 100 (switch): 4個參數
                    else if (firstNum == 100)
                    {
                        for (int j = 0; j < 4 && i < parts.Length; j++)
                        {
                            attrParts.Add(parts[i]);
                            i++;
                        }
                    }
                    else
                    {
                        // 一般屬性：固定數量的參數
                        for (int j = 0; j < expectedParams && i < parts.Length; j++)
                        {
                            attrParts.Add(parts[i]);
                            i++;
                        }
                    }

                    var attr = ParseCompactAttribute(string.Join(" ", attrParts));
                    if (attr != null)
                        entry.Attributes.Add(attr);
                }
                else
                {
                    // 解析動作：ActionId Directional FrameCount [ImageId FrameIndex Duration...]
                    // 需要至少3個數字來確認是動作
                    if (i + 2 >= parts.Length)
                    {
                        i++;
                        continue;
                    }

                    if (!int.TryParse(parts[i + 1], out int directional) || (directional != 0 && directional != 1))
                    {
                        i++;
                        continue;
                    }

                    if (!int.TryParse(parts[i + 2], out int frameCount))
                    {
                        i++;
                        continue;
                    }

                    // 收集動作數據：ActionId Directional FrameCount
                    var actionParts = new List<string> { parts[i], parts[i + 1], parts[i + 2] };
                    i += 3;

                    // 依據 FrameCount 收集固定數量的幀數據
                    // 每幀是 3 個值: ImageId FrameIndex Duration[modifiers]
                    int framesCollected = 0;
                    while (i < parts.Length && framesCollected < frameCount)
                    {
                        // 收集 ImageId
                        if (i >= parts.Length) break;
                        actionParts.Add(parts[i]);
                        i++;

                        // 收集 FrameIndex
                        if (i >= parts.Length) break;
                        actionParts.Add(parts[i]);
                        i++;

                        // 收集 Duration (可能帶修飾符)
                        if (i >= parts.Length) break;
                        actionParts.Add(parts[i]);
                        i++;

                        framesCollected++;
                    }

                    var action = ParseCompactAction(string.Join(" ", actionParts));
                    if (action != null)
                        entry.Actions.Add(action);
                }
            }
        }

        /// <summary>
        /// 提取字串前面的數字部分
        /// </summary>
        private static string ExtractLeadingNumber(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsDigit(c) || (sb.Length == 0 && c == '-'))
                    sb.Append(c);
                else
                    break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 解析壓縮格式動作行
        /// 格式: ActionId Directional FrameCount [ImageId FrameIndex Duration[modifiers]...]
        /// 範例: 0 1 4 0 0 4&lt;479 0 1 4 0 2 4&lt;478 0 3 4
        /// </summary>
        private static SprAction ParseCompactAction(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return null;

            if (!int.TryParse(parts[0], out int actionId))
                return null;
            if (!int.TryParse(parts[1], out int directional))
                return null;
            if (!int.TryParse(parts[2], out int frameCount))
                return null;

            var action = new SprAction
            {
                RawText = line,
                ActionId = actionId,
                ActionName = GetActionNameById(actionId),
                Directional = directional,
                FrameCount = frameCount
            };

            // 解析幀: 每三個值為一組 (ImageId, FrameIndex, Duration[modifiers])
            int i = 3;
            while (i + 2 < parts.Length)
            {
                if (!int.TryParse(parts[i], out int imageId))
                    break;
                if (!int.TryParse(parts[i + 1], out int frameIndex))
                    break;

                // Duration 可能帶有修飾符，如 4<479 或 4! 或 4[248
                var durationPart = parts[i + 2];
                var frame = ParseCompactFrame(imageId, frameIndex, durationPart);
                if (frame != null)
                    action.Frames.Add(frame);

                i += 3;
            }

            return action;
        }

        /// <summary>
        /// 解析壓縮格式的單一幀
        /// </summary>
        private static SprFrame ParseCompactFrame(int imageId, int frameIndex, string durationPart)
        {
            var frame = new SprFrame
            {
                RawText = $"{imageId} {frameIndex} {durationPart}",
                ImageId = imageId,
                FrameIndex = frameIndex
            };

            // 提取 duration 數字
            var durationStr = ExtractLeadingNumber(durationPart);
            if (!int.TryParse(durationStr, out int duration))
                return null;
            frame.Duration = duration;

            // 解析修飾符 (duration 之後的部分)
            var modifiers = durationPart.Substring(durationStr.Length);
            ParseFrameModifiers(frame, modifiers);

            return frame;
        }

        /// <summary>
        /// 解析幀修飾符
        /// </summary>
        private static void ParseFrameModifiers(SprFrame frame, string modifiers)
        {
            if (string.IsNullOrEmpty(modifiers))
                return;

            // ! - 觸發命中
            if (modifiers.Contains("!"))
                frame.TriggerHit = true;

            // > - 跳格移動
            if (modifiers.Contains(">"))
                frame.SkipFrame = true;

            // [ - 聲音
            var soundMatches = Regex.Matches(modifiers, @"\[(\d+)");
            foreach (Match m in soundMatches)
                frame.SoundIds.Add(int.Parse(m.Groups[1].Value));

            // ] - 疊加圖檔
            var overlayMatches = Regex.Matches(modifiers, @"\](\d+)");
            foreach (Match m in overlayMatches)
                frame.OverlayIds.Add(int.Parse(m.Groups[1].Value));

            // < - 特效
            var effectMatches = Regex.Matches(modifiers, @"<(\d+)");
            foreach (Match m in effectMatches)
                frame.EffectIds.Add(int.Parse(m.Groups[1].Value));
        }

        /// <summary>
        /// 根據屬性 ID 取得預期的參數數量
        /// </summary>
        private static int GetAttributeParamCount(int attrId)
        {
            return attrId switch
            {
                100 => 4,   // switch: x y z imageCount
                101 => 1,   // shadow: shadowId
                102 => 1,   // type: typeId
                104 => 1,   // filter: value (可能有多個，但先假設1個)
                105 => 0,   // attach: 特殊處理 (count + ids)
                106 => 1,   // weapon: weaponId
                107 => 1,   // size: sizeValue
                108 => 1,   // fly: flyValue
                109 => 1,   // magic_effect: effectId
                110 => 1,   // speed: speedValue
                111 => 1,   // skip: skipValue
                128 => 1,   // unknown attr 128
                _ => 1      // 預設1個參數
            };
        }

        /// <summary>
        /// 根據動作 ID 取得動作名稱
        /// </summary>
        private static string GetActionNameById(int actionId)
        {
            return actionId switch
            {
                0 => "walk",
                1 => "attack",
                2 => "damage",
                3 => "idle",
                4 => "sword_walk",
                5 => "sword_attack",
                6 => "sword_damage",
                7 => "sword_idle",
                8 => "death",
                11 => "axe_walk",
                12 => "axe_attack",
                13 => "axe_damage",
                14 => "axe_idle",
                15 => "pickup",
                16 => "throw",
                17 => "staff_attack",
                18 => "dir_magic",
                19 => "magic",
                20 => "bow_walk",
                21 => "bow_attack",
                22 => "bow_damage",
                23 => "bow_idle",
                24 => "spear_walk",
                25 => "spear_attack",
                26 => "spear_damage",
                27 => "spear_idle",
                28 => "open_south",
                29 => "close_west",
                30 => "special",
                31 => "magic_special",
                40 => "wand_walk",
                41 => "wand_attack",
                42 => "wand_damage",
                43 => "wand_idle",
                44 => "fly_up",
                45 => "fly_down",
                46 => "dagger_walk",
                47 => "dagger_attack",
                48 => "dagger_damage",
                49 => "dagger_idle",
                50 => "greatsword_walk",
                51 => "greatsword_attack",
                52 => "greatsword_damage",
                53 => "greatsword_idle",
                54 => "dual_walk",
                55 => "dual_attack",
                56 => "dual_damage",
                57 => "dual_idle",
                58 => "claw_walk",
                59 => "claw_attack",
                60 => "claw_damage",
                61 => "claw_idle",
                62 => "dart_walk",
                63 => "dart_attack",
                64 => "dart_damage",
                65 => "dart_idle",
                66 => "shop",
                67 => "salute",
                68 => "wave",
                69 => "cheer",
                70 => "shop2",
                71 => "fishing",
                100 => "switch",
                _ => $"action_{actionId}"
            };
        }

        /// <summary>
        /// 解析壓縮格式屬性行
        /// 格式: AttributeId Values...
        /// 範例: 101 3226 或 102 5 或 105 1 6262
        /// </summary>
        private static SprAttribute ParseCompactAttribute(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            if (!int.TryParse(parts[0], out int attrId))
                return null;

            var attr = new SprAttribute
            {
                AttributeId = attrId,
                AttributeName = GetAttributeNameById(attrId),
                RawParameters = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : ""
            };

            // 儲存參數
            for (int i = 1; i < parts.Length; i++)
            {
                attr.Parameters.Add(parts[i]);
            }

            return attr;
        }

        /// <summary>
        /// 根據屬性 ID 取得屬性名稱
        /// </summary>
        private static string GetAttributeNameById(int attrId)
        {
            return attrId switch
            {
                100 => "switch",
                101 => "shadow",
                102 => "type",
                104 => "filter",
                105 => "attach",
                106 => "weapon",
                107 => "size",
                108 => "fly",
                109 => "magic_effect",
                110 => "speed",
                111 => "skip",
                _ => $"attr_{attrId}"
            };
        }

        /// <summary>
        /// 解析動作定義 - 標準格式
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
