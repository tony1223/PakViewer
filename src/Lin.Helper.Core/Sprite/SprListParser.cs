using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Lin.Helper.Core.Sprite
{
    /// <summary>
    /// SPR List 檔案解析器 (list.spr / wlist.spr)
    /// 支援標準格式和壓縮格式
    /// </summary>
    public static class SprListParser
    {
        private static readonly Regex EntryHeaderPattern = new Regex(
            @"^#(\d+)\s+(\d+)(?:=(\d+))?\s+(.+)$",
            RegexOptions.Compiled);

        private static readonly Regex CompactEntryHeaderPattern = new Regex(
            @"^#(\d+)\s+(\d+)(?:=(\d+))?$",
            RegexOptions.Compiled);

        private static readonly Regex ActionPattern = new Regex(
            @"(\d+)\.([a-zA-Z_][a-zA-Z0-9_\s]*)\((\d+)\s+(\d+),([^)]+)\)",
            RegexOptions.Compiled);

        private static readonly Regex AttributePattern = new Regex(
            @"(\d+)\.([a-zA-Z_][a-zA-Z0-9_\s]*)\(([^)]*)\)",
            RegexOptions.Compiled);

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

            var headerParts = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (headerParts.Length >= 1 && int.TryParse(headerParts[0], out int total))
                result.TotalEntries = total;
            if (headerParts.Length >= 2 && int.TryParse(headerParts[1], out int u1))
                result.Unknown1 = u1;
            if (headerParts.Length >= 3 && int.TryParse(headerParts[2], out int u2))
                result.Unknown2 = u2;

            bool isCompactFormat = DetectCompactFormat(lines);

            SprListEntry currentEntry = null;
            var currentEntryLines = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.TrimStart();

                if (trimmedLine.StartsWith("#"))
                {
                    if (currentEntry != null)
                    {
                        if (isCompactFormat)
                            ParseCompactEntryContent(currentEntry, currentEntryLines);
                        else
                            ParseEntryContent(currentEntry, currentEntryLines);
                        result.Entries.Add(currentEntry);
                    }

                    currentEntry = ParseEntryHeader(trimmedLine, isCompactFormat);
                    currentEntryLines.Clear();

                    var restOfLine = GetRestOfEntryLine(trimmedLine, isCompactFormat);
                    if (!string.IsNullOrWhiteSpace(restOfLine))
                    {
                        currentEntryLines.Add(restOfLine);
                    }
                }
                else if (currentEntry != null && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    currentEntryLines.Add(trimmedLine);
                }
            }

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

        private static bool DetectCompactFormat(string[] lines)
        {
            for (int i = 1; i < lines.Length && i < 10; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("#"))
                {
                    if (CompactEntryHeaderPattern.IsMatch(line))
                        return true;
                    if (EntryHeaderPattern.IsMatch(line))
                        return false;
                }
            }
            return false;
        }

        private static SprListEntry ParseEntryHeader(string line, bool isCompactFormat = false)
        {
            var entry = new SprListEntry { RawText = line };

            if (isCompactFormat)
            {
                var match = CompactEntryHeaderPattern.Match(line);
                if (match.Success)
                {
                    entry.Id = int.Parse(match.Groups[1].Value);
                    entry.ImageCount = int.Parse(match.Groups[2].Value);
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                        entry.LinkedId = int.Parse(match.Groups[3].Value);
                    entry.Name = $"#{entry.Id}";
                }
                else
                {
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

            var stdMatch = EntryHeaderPattern.Match(line);
            if (stdMatch.Success)
            {
                entry.Id = int.Parse(stdMatch.Groups[1].Value);
                entry.ImageCount = int.Parse(stdMatch.Groups[2].Value);
                if (!string.IsNullOrEmpty(stdMatch.Groups[3].Value))
                    entry.LinkedId = int.Parse(stdMatch.Groups[3].Value);

                // 名稱可能後接動作或屬性定義，需要在第一個 "數字." 模式前截斷
                var rawName = stdMatch.Groups[4].Value.Trim();
                var nameParts = rawName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var nameBuilder = new StringBuilder();
                foreach (var part in nameParts)
                {
                    if (Regex.IsMatch(part, @"^\d+\."))
                        break;
                    if (nameBuilder.Length > 0) nameBuilder.Append(" ");
                    nameBuilder.Append(part);
                }
                entry.Name = nameBuilder.Length > 0 ? nameBuilder.ToString() : rawName;
            }
            else
            {
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

        private static string GetRestOfEntryLine(string line, bool isCompactFormat = false)
        {
            if (isCompactFormat)
            {
                var parts = line.TrimStart('#').Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2)
                {
                    return string.Join(" ", parts, 2, parts.Length - 2);
                }
                return null;
            }

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

        private static void ParseEntryContent(SprListEntry entry, List<string> contentLines)
        {
            var fullContent = string.Join(" ", contentLines);

            var actionMatches = ActionPattern.Matches(fullContent);
            foreach (Match match in actionMatches)
            {
                var action = ParseAction(match);
                if (action != null)
                    entry.Actions.Add(action);
            }

            var attrMatches = AttributePattern.Matches(fullContent);
            foreach (Match match in attrMatches)
            {
                int attrId = int.Parse(match.Groups[1].Value);
                if (attrId >= 100)
                {
                    var attr = ParseAttribute(match);
                    if (attr != null)
                        entry.Attributes.Add(attr);
                }
            }
        }

        private static void ParseCompactEntryContent(SprListEntry entry, List<string> contentLines)
        {
            var fullContent = string.Join(" ", contentLines);
            var parts = fullContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return;

            int i = 0;
            while (i < parts.Length)
            {
                if (!int.TryParse(ExtractLeadingNumber(parts[i]), out int firstNum))
                {
                    i++;
                    continue;
                }

                if (firstNum >= 100)
                {
                    var attrParts = new List<string> { parts[i] };
                    i++;

                    int expectedParams = GetAttributeParamCount(firstNum);

                    if (firstNum == 105 && i < parts.Length)
                    {
                        attrParts.Add(parts[i]);
                        if (int.TryParse(parts[i], out int attachCount))
                        {
                            i++;
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

                    var actionParts = new List<string> { parts[i], parts[i + 1], parts[i + 2] };
                    i += 3;

                    int framesCollected = 0;
                    while (i < parts.Length && framesCollected < frameCount)
                    {
                        if (i >= parts.Length) break;
                        actionParts.Add(parts[i]);
                        i++;

                        if (i >= parts.Length) break;
                        actionParts.Add(parts[i]);
                        i++;

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

            int i = 3;
            while (i + 2 < parts.Length)
            {
                if (!int.TryParse(parts[i], out int imageId))
                    break;
                if (!int.TryParse(parts[i + 1], out int frameIndex))
                    break;

                var durationPart = parts[i + 2];
                var frame = ParseCompactFrame(imageId, frameIndex, durationPart);
                if (frame != null)
                    action.Frames.Add(frame);

                i += 3;
            }

            return action;
        }

        private static SprActionFrame ParseCompactFrame(int imageId, int frameIndex, string durationPart)
        {
            var frame = new SprActionFrame
            {
                RawText = $"{imageId} {frameIndex} {durationPart}",
                ImageId = imageId,
                FrameIndex = frameIndex
            };

            var durationStr = ExtractLeadingNumber(durationPart);
            if (!int.TryParse(durationStr, out int duration))
                return null;
            frame.Duration = duration;

            var modifiers = durationPart.Substring(durationStr.Length);
            ParseFrameModifiers(frame, modifiers);

            return frame;
        }

        private static void ParseFrameModifiers(SprActionFrame frame, string modifiers)
        {
            if (string.IsNullOrEmpty(modifiers))
                return;

            if (modifiers.Contains("!"))
                frame.TriggerHit = true;

            if (modifiers.Contains(">"))
                frame.SkipFrame = true;

            var soundMatches = Regex.Matches(modifiers, @"\[(\d+)");
            foreach (Match m in soundMatches)
                frame.SoundIds.Add(int.Parse(m.Groups[1].Value));

            var overlayMatches = Regex.Matches(modifiers, @"\](\d+)");
            foreach (Match m in overlayMatches)
                frame.OverlayIds.Add(int.Parse(m.Groups[1].Value));

            var effectMatches = Regex.Matches(modifiers, @"<(\d+)");
            foreach (Match m in effectMatches)
                frame.EffectIds.Add(int.Parse(m.Groups[1].Value));
        }

        private static int GetAttributeParamCount(int attrId)
        {
            return attrId switch
            {
                100 => 4,
                101 => 1,
                102 => 1,
                104 => 1,
                105 => 0,
                106 => 1,
                107 => 1,
                108 => 1,
                109 => 1,
                110 => 1,
                111 => 1,
                128 => 1,
                _ => 1
            };
        }

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

            for (int i = 1; i < parts.Length; i++)
            {
                attr.Parameters.Add(parts[i]);
            }

            return attr;
        }

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

            var framesStr = match.Groups[5].Value;
            action.Frames = ParseFrames(framesStr);

            return action;
        }

        private static List<SprActionFrame> ParseFrames(string framesStr)
        {
            var frames = new List<SprActionFrame>();
            var frameParts = framesStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in frameParts)
            {
                var frame = ParseFrame(part);
                if (frame != null)
                    frames.Add(frame);
            }

            return frames;
        }

        private static SprActionFrame ParseFrame(string frameStr)
        {
            var frame = new SprActionFrame { RawText = frameStr };

            var basicMatch = FramePattern.Match(frameStr);
            if (!basicMatch.Success)
                return null;

            frame.ImageId = int.Parse(basicMatch.Groups[1].Value);
            frame.FrameIndex = int.Parse(basicMatch.Groups[2].Value);
            frame.Duration = int.Parse(basicMatch.Groups[3].Value);

            var restStr = frameStr.Substring(basicMatch.Length);

            if (restStr.Contains("!"))
                frame.TriggerHit = true;

            if (restStr.Contains(">"))
                frame.SkipFrame = true;

            var soundMatches = Regex.Matches(restStr, @"\[(\d+)");
            foreach (Match m in soundMatches)
                frame.SoundIds.Add(int.Parse(m.Groups[1].Value));

            var overlayMatches = Regex.Matches(restStr, @"\](\d+)");
            foreach (Match m in overlayMatches)
                frame.OverlayIds.Add(int.Parse(m.Groups[1].Value));

            var effectMatches = Regex.Matches(restStr, @"<(\d+)");
            foreach (Match m in effectMatches)
                frame.EffectIds.Add(int.Parse(m.Groups[1].Value));

            return frame;
        }

        private static SprAttribute ParseAttribute(Match match)
        {
            var attr = new SprAttribute
            {
                AttributeId = int.Parse(match.Groups[1].Value),
                AttributeName = match.Groups[2].Value.Trim(),
                RawParameters = match.Groups[3].Value
            };

            var paramsStr = match.Groups[3].Value;
            if (!string.IsNullOrWhiteSpace(paramsStr))
            {
                var parts = paramsStr.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                attr.Parameters.AddRange(parts);
            }

            return attr;
        }
    }
}
