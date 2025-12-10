# SPR List 檔案解析器規劃 (list.spr / wlist.spr)

## 1. 目標功能

建立一個新的視覺化模式來瀏覽和編輯 `list.spr` / `wlist.spr` 檔案：
- **左側面板**: 顯示所有角色/物件條目清單
- **右側面板**: 顯示選中條目的所有動作，每個動作以圖塊方式呈現
- **播放功能**: 可以播放每個動作的完整動畫序列

## 2. 檔案格式分析

### 2.1 檔案結構
```
第一行: 總條目數 未知數1 未知數2
例如: 25036 0 60419

後續行: 角色/物件定義
```

### 2.2 條目格式 (Entry)

#### 主條目 (Header Line)
```
#編號 圖片數量[=關聯ID] 名稱 [屬性列表...]
```

例如:
- `#0 312=3225 prince` - 編號0, 312張圖, 關聯ID 3225, 名稱 prince
- `#2 1 axe 102.type(9)` - 編號2, 1張圖, 名稱 axe, 屬性 type=9
- `#138 208 elf male` - 編號138, 208張圖, 名稱 elf male

#### 動作定義 (Action Definition)
```
動作編號.動作名稱(方向 幀數,幀序列)
```

例如:
- `0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)` - walk動作, 有向(1), 4幀

### 2.3 幀序列格式 (Frame Sequence)

#### 基本幀格式
```
圖片ID.幀索引:時間單位
```
例如: `32.0:3` - 圖片32, 幀0, 持續3個時間單位

#### 幀修飾符
| 符號 | 意義 | 範例 |
|------|------|------|
| `!` | 觸發命中效果 (被打動作+聲音) | `32.3:6!` |
| `[數字` | 觸發聲音檔 | `32.2:2[248[701[702` - 觸發3個聲音 |
| `]數字` | 疊加圖檔特效 | `32.0:3]215` - 疊加#215圖檔 |
| `<數字` | 觸發特效/子圖檔 | `0.0:4<479` |
| `>` | 跳格移動標記 | `8.1:2<95>` |

### 2.4 屬性定義 (Attributes)

| 編號 | 名稱 | 格式 | 說明 |
|------|------|------|------|
| 100 | switch | `100.switch(開關數 參數組...)` | 開關控制 |
| 101 | shadow | `101.shadow(ID)` | 影子圖ID |
| 102 | type | `102.type(類型)` | 物件類型 |
| 104 | attr | `104.attr(值)` | 濾鏡屬性 |
| 105 | cloth/clothes | `105.cloth(數量 ID...)` | 附加物件 |
| 106 | weapon | `106.weapon(...)` | 武器定義 |
| 107 | size | `107.size(X Y)` | 體型大小 |
| 108 | flying type | `108.flying type(值)` | 飛行模式 |
| 109 | effect | `109.effect(類型 值)` | 魔法效果 |
| 110 | framerate | `110.framerate(值)` | 播放速度 |
| 111 | stride | `111.stride(值)` | 跳畫格移動 |

### 2.5 物件類型 (type) 對照
| 值 | 類型 |
|----|------|
| 0 | 特效/影子 |
| 4 | 告示牌 |
| 5 | 玩家角色 |
| 6 | 載具 |
| 8 | 門 |
| 9 | 物品 |
| 10 | 怪物/NPC |
| 12 | 女僕 |

### 2.6 動作編號對照表

| 編號 | 名稱 | 說明 |
|------|------|------|
| 0 | walk/sign/fire/move | 走路/符號/特效 |
| 1 | attack | 空手攻擊 |
| 2 | damage | 被打 |
| 3 | breath/idle | 呼吸/待機 |
| 4-7 | onehandsword | 單手劍系列 |
| 8 | death | 死亡 |
| 11-14 | axe | 斧頭系列 |
| 15 | get | 撿東西 |
| 16 | throw | 投擲 |
| 17 | wand/zap | 法杖攻擊 |
| 18 | spell dir | 方向性魔法 |
| 19 | spell nodir | 非方向性魔法 |
| 20-23 | bow | 弓箭系列 |
| 24-27 | spear | 長矛系列 |
| 28-29 | on/off, open/close | 開關/門 |
| 30 | alt attack | 必殺技 |
| 31 | spell direction extra | 魔法必殺技 |
| 40-43 | staff | 法杖系列 |
| 44-45 | moveup/movedown | 飛行 |
| 46-49 | dagger | 匕首系列 |
| 50-53 | largesword | 大劍系列 |
| 54-57 | double sword | 雙刀系列 |
| 58-61 | claw | 爪系列 |
| 62-65 | shuriken | 飛鏢系列 |
| 66-71 | act (shop, dual, wave, cheer, fishing) | 動作表情 |

## 3. 資料結構設計

```csharp
// === Models/SprListModels.cs ===

namespace PakViewer.Models
{
    // 主檔案結構
    public class SprListFile
    {
        public int TotalEntries { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public List<SprListEntry> Entries { get; set; } = new();
    }

    // 條目結構
    public class SprListEntry
    {
        public int Id { get; set; }
        public int ImageCount { get; set; }
        public int? LinkedId { get; set; }  // =後面的數字
        public string Name { get; set; }
        public List<SprAction> Actions { get; set; } = new();
        public List<SprAttribute> Attributes { get; set; } = new();

        // 方便取用的屬性
        public int? ShadowId => Attributes.FirstOrDefault(a => a.AttributeId == 101)?.IntValue;
        public int? TypeId => Attributes.FirstOrDefault(a => a.AttributeId == 102)?.IntValue;
    }

    // 動作結構
    public class SprAction
    {
        public int ActionId { get; set; }
        public string ActionName { get; set; }
        public int Directional { get; set; }  // 0=無向, 1=有向
        public int FrameCount { get; set; }
        public List<SprFrame> Frames { get; set; } = new();

        public string DisplayName => $"{ActionId}.{ActionName}";
        public int TotalDuration => Frames.Sum(f => f.Duration);
    }

    // 幀結構
    public class SprFrame
    {
        public int ImageId { get; set; }
        public int FrameIndex { get; set; }
        public int Duration { get; set; }
        public bool TriggerHit { get; set; }  // !
        public List<int> SoundIds { get; set; } = new();  // [數字
        public List<int> OverlayIds { get; set; } = new();  // ]數字
        public List<int> EffectIds { get; set; } = new();  // <數字
        public bool SkipFrame { get; set; }  // >

        public string RawText { get; set; }  // 原始文字，方便編輯
    }

    // 屬性結構
    public class SprAttribute
    {
        public int AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string RawParameters { get; set; }
        public List<string> Parameters { get; set; } = new();

        public int? IntValue => Parameters.Count > 0 && int.TryParse(Parameters[0], out int v) ? v : null;
    }
}
```

## 4. UI 設計

### 4.1 新增檔案
- `ucSprListViewer.cs` - 主要的 UserControl

### 4.2 介面佈局
```
┌─────────────────────────────────────────────────────────────────┐
│ [搜尋: ____________] [類型篩選: ▼全部]  [載入SPR檔案]           │
├────────────────────┬────────────────────────────────────────────┤
│ 條目清單 (左側)     │ 動作檢視區 (右側)                          │
│                    │                                            │
│ ┌────────────────┐ │ ┌──────────────────────────────────────┐  │
│ │ #0 prince      │ │ │ 動作: 0.walk                    [▶]  │  │
│ │ #1 princess    │ │ │ ┌─────┬─────┬─────┬─────┐           │  │
│ │ #29 floating e │ │ │ │ F0  │ F1  │ F2  │ F3  │           │  │
│ │ #30 skeleton   │ │ │ │ :4  │ :4  │ :4  │ :4  │           │  │
│ │ #31 slime      │ │ │ └─────┴─────┴─────┴─────┘           │  │
│ │ ...            │ │ └──────────────────────────────────────┘  │
│ │                │ │                                            │
│ │                │ │ ┌──────────────────────────────────────┐  │
│ │                │ │ │ 動作: 1.attack                  [▶]  │  │
│ │                │ │ │ ┌─────┬─────┬─────┬─────┬─────┐      │  │
│ │                │ │ │ │ F0  │ F1  │ F2! │ F3  │ F4  │      │  │
│ │                │ │ │ │ :3  │ :4  │ :2  │ :5  │ :3  │      │  │
│ │                │ │ │ │     │[248 │     │     │     │      │  │
│ │                │ │ │ └─────┴─────┴─────┴─────┴─────┘      │  │
│ └────────────────┘ │ └──────────────────────────────────────┘  │
│                    │                                            │
│ 條目資訊:          │ ┌──────────────────────────────────────┐  │
│ 圖片數: 312        │ │           動畫預覽區                  │  │
│ 類型: 5 (玩家)     │ │                                      │  │
│ 影子: #3226        │ │         [  動畫播放中...  ]          │  │
│ 關聯: #3225        │ │                                      │  │
│                    │ └──────────────────────────────────────┘  │
└────────────────────┴────────────────────────────────────────────┘
```

### 4.3 功能說明

1. **左側條目清單**
   - 顯示所有條目 (#ID + 名稱)
   - 支援搜尋過濾
   - 支援類型篩選 (玩家、怪物、物品等)
   - 點擊選中顯示詳細資訊

2. **右側動作檢視區**
   - 每個動作一個區塊
   - 顯示幀序列 (橫向排列的小方塊)
   - 每個幀方塊顯示:
     - 圖片ID.幀索引
     - 持續時間
     - 特殊標記 (!, [聲音, ]圖檔, <特效)
   - 播放按鈕 [▶] 可播放該動作動畫

3. **動畫預覽區**
   - 使用現有的 `ucSprViewer` 顯示動畫
   - 需要載入對應的 .spr 檔案

## 5. 實作步驟

### 步驟 1: 建立資料模型
- 新增 `Models/SprListModels.cs`

### 步驟 2: 建立解析器
- 新增 `Utility/SprListParser.cs`
- 實作逐行解析邏輯
- 處理多行條目 (tab/空格 開頭的續行)

### 步驟 3: 建立 UI 控制項
- 新增 `ucSprListViewer.cs`
- 實作左右分割面板
- 實作條目清單 (ListView)
- 實作動作區塊 (FlowLayoutPanel + 自訂 Panel)
- 實作幀序列顯示 (自訂繪製)

### 步驟 4: 整合到主視窗
- 在 `frmMain` 加入選單項目「開啟 SPR 列表檔」
- 新增顯示模式切換

### 步驟 5: 動畫播放功能
- 連結到現有 `ucSprViewer`
- 根據動作定義的幀序列播放

### 步驟 6: CLI 整合 (可選)
```
PakViewer.exe -cli sprlist "path/to/list.spr" list
PakViewer.exe -cli sprlist "path/to/list.spr" search "prince"
PakViewer.exe -cli sprlist "path/to/list.spr" show 0
PakViewer.exe -cli sprlist "path/to/list.spr" actions 0
```

## 6. 技術細節

### 6.1 解析器正規表達式

```csharp
// 主條目
// #0 312=3225 prince
// #2 1 axe 102.type(9)
var entryPattern = @"^#(\d+)\s+(\d+)(?:=(\d+))?\s+(.+)$";

// 動作定義
// 0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)
var actionPattern = @"(\d+)\.([a-zA-Z_\s]+)\((\d+)\s+(\d+),(.+)\)";

// 幀定義
// 32.0:3 或 32.0:3! 或 32.0:3[248 或 32.0:3]215 或 32.0:3<479
var framePattern = @"(\d+)\.(\d+):(\d+)([!])?(?:\[(\d+))*(?:\](\d+))*(?:<(\d+))*([>])?";

// 屬性定義
// 101.shadow(3226) 或 102.type(5) 或 105.cloth(1 6262)
var attrPattern = @"(\d+)\.([a-zA-Z_\s]+)\(([^)]+)\)";
```

### 6.2 與現有 SPR 檔案整合

list.spr 定義了動畫序列，但實際圖片在 .spr 檔案中：
- 條目 ID 對應 Sprite 模式中的圖片編號
- ImageId.FrameIndex 對應 .spr 檔案中的幀

```csharp
// 取得對應的 SPR 檔案
public byte[] GetSpriteData(int entryId)
{
    // 從已載入的 pak 檔案中讀取對應的 spr
    var sprFileName = $"{entryId}.spr";
    // ...
}
```

## 7. 檔案清單

新增檔案:
1. `Models/SprListModels.cs` - 資料模型
2. `Utility/SprListParser.cs` - 解析器
3. `ucSprListViewer.cs` - UI 控制項

修改檔案:
1. `frmMain.cs` - 加入選單和模式切換
2. `PakReader.cs` - 加入 CLI 支援 (可選)
