using System.Collections.Generic;
using System.Linq;

namespace PakViewer.Models
{
    /// <summary>
    /// SPR List 檔案結構 (list.spr / wlist.spr)
    /// </summary>
    public class SprListFile
    {
        public int TotalEntries { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public List<SprListEntry> Entries { get; set; } = new List<SprListEntry>();
    }

    /// <summary>
    /// 條目結構 - 代表一個角色/物件/特效
    /// </summary>
    public class SprListEntry
    {
        public int Id { get; set; }
        public int ImageCount { get; set; }
        public int? LinkedId { get; set; }  // =後面的數字
        public string Name { get; set; }
        public List<SprAction> Actions { get; set; } = new List<SprAction>();
        public List<SprAttribute> Attributes { get; set; } = new List<SprAttribute>();
        public string RawText { get; set; }  // 原始文字

        // 方便取用的屬性
        public int? ShadowId => GetAttributeIntValue(101);
        public int? TypeId => GetAttributeIntValue(102);
        public int? AttrValue => GetAttributeIntValue(104);

        /// <summary>
        /// 圖檔編號：有指定 LinkedId 時用 LinkedId，否則用自身 Id
        /// </summary>
        public int SpriteId => LinkedId ?? Id;

        public string TypeName
        {
            get
            {
                var typeId = TypeId;
                if (!typeId.HasValue) return "未知";
                return typeId.Value switch
                {
                    0 => "特效/影子",
                    4 => "告示牌",
                    5 => "玩家角色",
                    6 => "載具",
                    8 => "門",
                    9 => "物品",
                    10 => "怪物/NPC",
                    12 => "女僕",
                    _ => $"未知({typeId})"
                };
            }
        }

        private int? GetAttributeIntValue(int attrId)
        {
            var attr = Attributes.FirstOrDefault(a => a.AttributeId == attrId);
            return attr?.IntValue;
        }

        public override string ToString()
        {
            return $"#{Id} {Name}";
        }
    }

    /// <summary>
    /// 動作結構 - 代表一個動畫動作 (walk, attack, damage 等)
    /// </summary>
    public class SprAction
    {
        public int ActionId { get; set; }
        public string ActionName { get; set; }
        public int Directional { get; set; }  // 0=無向, 1=有向
        public int FrameCount { get; set; }
        public List<SprFrame> Frames { get; set; } = new List<SprFrame>();
        public string RawText { get; set; }  // 原始文字

        public string DisplayName => $"{ActionId}.{ActionName}";
        public int TotalDuration => Frames.Sum(f => f.Duration);
        public bool IsDirectional => Directional == 1;

        public string ActionTypeName
        {
            get
            {
                return ActionId switch
                {
                    0 => "走路/符號/特效",
                    1 => "空手攻擊",
                    2 => "被打",
                    3 => "呼吸/待機",
                    4 => "持劍走路",
                    5 => "持劍攻擊",
                    6 => "持劍被打",
                    7 => "持劍呼吸",
                    8 => "死亡",
                    11 => "持斧走路",
                    12 => "持斧攻擊",
                    13 => "持斧被打",
                    14 => "持斧呼吸",
                    15 => "撿東西",
                    16 => "投擲",
                    17 => "法杖攻擊",
                    18 => "方向魔法",
                    19 => "非方向魔法",
                    20 => "持弓走路",
                    21 => "持弓攻擊",
                    22 => "持弓被打",
                    23 => "持弓呼吸",
                    24 => "持矛走路",
                    25 => "持矛攻擊",
                    26 => "持矛被打",
                    27 => "持矛呼吸",
                    28 => "開啟/南",
                    29 => "關閉/西",
                    30 => "必殺技",
                    31 => "魔法必殺技",
                    40 => "持杖走路",
                    41 => "持杖攻擊",
                    42 => "持杖被打",
                    43 => "持杖呼吸",
                    44 => "飛向天空",
                    45 => "降落",
                    46 => "持匕首走路",
                    47 => "持匕首攻擊",
                    48 => "持匕首被打",
                    49 => "持匕首呼吸",
                    50 => "持大劍走路",
                    51 => "持大劍攻擊",
                    52 => "持大劍被打",
                    53 => "持大劍呼吸",
                    54 => "雙刀走路",
                    55 => "雙刀攻擊",
                    56 => "雙刀被打",
                    57 => "雙刀呼吸",
                    58 => "持爪走路",
                    59 => "持爪攻擊",
                    60 => "持爪被打",
                    61 => "持爪呼吸",
                    62 => "持飛鏢走路",
                    63 => "持飛鏢攻擊",
                    64 => "持飛鏢被打",
                    65 => "持飛鏢呼吸",
                    66 => "商店動作",
                    67 => "敬禮動作",
                    68 => "揮手動作",
                    69 => "歡呼動作",
                    70 => "商店動作",
                    71 => "釣魚動作",
                    100 => "開關控制",
                    _ => ActionName
                };
            }
        }

        public override string ToString()
        {
            return $"{DisplayName} ({FrameCount}幀, {(IsDirectional ? "有向" : "無向")})";
        }
    }

    /// <summary>
    /// 幀結構 - 代表動畫中的一幀
    /// </summary>
    public class SprFrame
    {
        public int ImageId { get; set; }
        public int FrameIndex { get; set; }
        public int Duration { get; set; }
        public bool TriggerHit { get; set; }  // !
        public List<int> SoundIds { get; set; } = new List<int>();  // [數字
        public List<int> OverlayIds { get; set; } = new List<int>();  // ]數字
        public List<int> EffectIds { get; set; } = new List<int>();  // <數字
        public bool SkipFrame { get; set; }  // >
        public string RawText { get; set; }  // 原始文字

        public string DisplayText => $"{ImageId}.{FrameIndex}:{Duration}";

        public bool HasModifiers => TriggerHit || SoundIds.Count > 0 || OverlayIds.Count > 0 || EffectIds.Count > 0 || SkipFrame;

        public string ModifiersText
        {
            get
            {
                var parts = new List<string>();
                if (TriggerHit) parts.Add("!");
                foreach (var s in SoundIds) parts.Add($"[{s}");
                foreach (var o in OverlayIds) parts.Add($"]{o}");
                foreach (var e in EffectIds) parts.Add($"<{e}");
                if (SkipFrame) parts.Add(">");
                return string.Join("", parts);
            }
        }

        public override string ToString()
        {
            return RawText ?? DisplayText + ModifiersText;
        }
    }

    /// <summary>
    /// 屬性結構 - 代表條目的屬性 (shadow, type, cloth 等)
    /// </summary>
    public class SprAttribute
    {
        public int AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string RawParameters { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();

        public int? IntValue
        {
            get
            {
                if (Parameters.Count > 0 && int.TryParse(Parameters[0], out int v))
                    return v;
                return null;
            }
        }

        public string AttributeTypeName
        {
            get
            {
                return AttributeId switch
                {
                    100 => "開關控制",
                    101 => "影子圖",
                    102 => "物件類型",
                    104 => "濾鏡屬性",
                    105 => "附加物件",
                    106 => "武器",
                    107 => "體型大小",
                    108 => "飛行模式",
                    109 => "魔法效果",
                    110 => "播放速度",
                    111 => "跳格移動",
                    _ => AttributeName
                };
            }
        }

        public override string ToString()
        {
            return $"{AttributeId}.{AttributeName}({RawParameters})";
        }
    }
}
