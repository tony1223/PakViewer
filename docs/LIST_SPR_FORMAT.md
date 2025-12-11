# Lineage List.spr 格式說明

## 概述

`list.spr`（或 `wlist.spr`）是 Lineage 遊戲客戶端用來定義所有玩家、NPC、怪物的行為和動畫的設定檔。

## 檔案結構

### 標頭行
```
25036 0 60419
```
- 第一個數字：總條目數
- 第二、三個數字：用途不明

### 條目定義
```
#ID FRAME_COUNT=SPRITE_ID NAME
```
或（無 LinkedId）：
```
#ID FRAME_COUNT NAME
```

範例：
```
#0 312=3225 prince
#1 312=3227 princess
#11616 64 orc
```

- `ID`：條目編號
- `FRAME_COUNT`：總圖片數量
- `SPRITE_ID`（可選）：關聯的 sprite 檔案編號（`=` 後的數字）
- `NAME`：名稱

### Sprite 檔案對應
- 有 `=SPRITE_ID` 時：使用 `SPRITE_ID-{subId}.spr`
- 無 `=SPRITE_ID` 時：使用 `ID-{subId}.spr`

其中 `subId = ImageId + direction`

---

## 動作定義

### 格式
```
ACTION_ID.action_name(DIRECTIONAL FRAME_COUNT,FRAME_DATA...)
```

範例：
```
0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4)
1.attack(1 8,0.0:0 16.0:2 16.1:3 16.2:5 16.3:3 16.4:2[246 16.5:2! 16.6:3)
5.attack sword(1 5,24.0:4 32.0:3]215 32.1:7 32.2:2[248[701[702 32.3:6!)
```

### 參數說明
- `ACTION_ID`：動作編號（見下方動作 ID 對照表）
- `action_name`：動作名稱（僅供參考）
- `DIRECTIONAL`：
  - `1` = 8 方向動作
  - `0` = 單一方向
- `FRAME_COUNT`：每個方向的幀數

### 方向系統（8 方向）
方向編號從左上開始順時針：
```
0=左上(↖)  1=上(↑)   2=右上(↗)
7=左(←)    [中心]    3=右(→)
6=左下(↙)  5=下(↓)   4=右下(↘)
```

---

## 幀資料格式

### 基本格式
```
ImageId.FrameIndex:Duration
```

範例：
- `0.0:4` → ImageId=0, FrameIndex=0, Duration=4
- `16.3:5` → ImageId=16, FrameIndex=3, Duration=5

### Sprite 檔案計算
實際載入的 sprite 檔案：
```
{SpriteId}-{ImageId + Direction}.spr
```

範例（SpriteId=3225, Direction=4）：
- `0.0:4` → 載入 `3225-4.spr`，顯示第 0 幀
- `16.0:2` → 載入 `3225-20.spr`，顯示第 0 幀
- `32.2:7` → 載入 `3225-36.spr`，顯示第 2 幀

### 特殊修飾符

| 符號 | 格式 | 說明 | 範例 |
|------|------|------|------|
| `!` | `ImageId.Frame:Duration!` | 觸發對方被打動作及聲音 | `32.3:6!` |
| `[` | `ImageId.Frame:Duration[SoundId` | 播放音效（可多個） | `32.2:2[248[701[702` |
| `]` | `ImageId.Frame:Duration]SpriteId` | 疊加顯示圖檔 | `32.0:3]215` |
| `<` | `ImageId.Frame:Duration<EffectId` | 特效/參考 sprite | `0.0:4<479` |
| `>` | `ImageId.Frame:Duration>` | 跳過此幀 | `0.0:0>` |

**注意**：`]` 疊加圖檔不能放在動作的第一張圖。

---

## 屬性定義（100+）

屬性用於設定條目的額外資訊：

| ID | 名稱 | 說明 | 範例 |
|----|------|------|------|
| 100 | switch | 切換控制 | `100.switch(...)` |
| 101 | shadow | 影子 sprite ID | `101.shadow(3228)` |
| 102 | type | 物件類型 | `102.type(5)` |
| 104 | attr | 屬性過濾 | `104.attr(1)` |
| 105 | cloth | 附加服裝 | `105.cloth(1 2)` |
| 107 | size | 身體大小 | `107.size(24 24)` |
| 109 | effect | 魔法效果 | `109.effect(1 2)` |

### 物件類型（102.type）

| 值 | 類型 |
|----|------|
| 0 | 影子/法術特效 |
| 1 | 裝飾品 |
| 5 | 玩家/不能對話的 NPC |
| 6 | 可對話 NPC |
| 7 | 寶箱/開關 |
| 8 | 可開啟的門 |
| 9 | 可撿取的物品 |
| 10 | 怪物（會出現攻擊符號） |
| 11 | 城牆/城門 |
| 12 | 新 NPC（可對話） |
| 14 | 盟屋告示牌 |
| 15 | 拍賣告示板 |

### 屬性過濾（104.attr）

| 值 | 效果 |
|----|------|
| 1 | 半透明 |

---

## 動作 ID 對照表

| ID | 名稱 | 說明 |
|----|------|------|
| 0 | walk | 走路 |
| 1 | attack | 空手攻擊 |
| 2 | damage | 被打 |
| 3 | breath | 呼吸/待機 |
| 4-7 | attack sword | 劍攻擊變體 |
| 8 | death | 死亡 |
| 11-14 | attack axe | 斧攻擊 |
| 15 | pickup | 撿起物品 |
| 16 | throw | 投擲 |
| 17 | wand | 法杖 |
| 18-19 | spell | 施法 |
| 20-27 | attack bow/spear | 弓/矛攻擊 |
| 40-43 | attack staff | 杖攻擊 |
| 46-49 | attack dagger | 匕首攻擊 |

---

## 多行條目

當條目定義過長時，可以用 Tab 或空格開頭續行：

```
#100 256=5000 long_entry_name
	0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)
	1.attack(1 8,0.0:0 16.0:2 16.1:3...)
	101.shadow(5001)
	102.type(10)
```

---

## 完整範例

```
#1 312=3227 princess
0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4)
1.attack(1 8,0.0:0 16.0:2 16.1:3 16.2:5 16.3:3 16.4:2[246 16.5:2! 16.6:3)
2.damage(1 3,8.0:4 24.0:6[1546[1547 24.1:4)
3.breath(1 12,8.11:5 8.0:5 8.1:5 8.2:5 8.3:5 8.4:5 8.5:5 8.6:5 8.7:5 8.8:5 8.9:5 8.10:5)
101.shadow(3228)
102.type(5)
107.size(24 48)
```

解析：
- 條目 ID=1，名稱 "princess"，sprite 檔案編號 3227
- 4 個動作：walk、attack、damage、breath
- 影子使用 sprite 3228
- 類型為玩家角色 (5)
- 身體大小 24x48
