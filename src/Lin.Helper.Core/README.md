# Lin.Helper.Core

Lineage 1 遊戲資源檔案格式處理函式庫，提供 PAK、SPR、DAT、IMG、TIL、XML 等格式的讀取與處理功能。

## 安裝

```bash
dotnet add package Lin.Helper.Core
```

## 功能總覽

| 功能 | 類別 | 說明 |
|------|------|------|
| PAK 加解密 | `PakTools` | PAK/IDX 封裝檔加解密 |
| SPR 解析 | `SprReader` | Sprite 動畫圖檔解析 |
| SPR List 解析 | `SprListParser` | list.spr / wlist.spr 解析 |
| DAT 解密 | `DatFile` | Lineage M DAT 資源檔解密 |
| 圖片轉換 | `ImageConverter` | IMG/TIL/TBT 格式轉換 |
| TIL 進階處理 | `L1Til` | TIL 解析、版本檢測、降採樣 |
| XML 加解密 | `XmlCracker` | 遊戲設定 XML 加解密 |

---

## 使用範例

### PAK 檔案加解密

```csharp
using Lin.Helper.Core.Pak;

// 初始化加密表 (從資源檔載入)
PakTools.Initialize();

// 或手動設定加密表
// PakTools.SetMaps(map1, map2, map3, map4, map5);

// 解密資料 (index: 從第幾個 byte 開始解密)
byte[] decrypted = PakTools.Decode(encryptedData, index: 0);

// 加密資料
byte[] encrypted = PakTools.Encode(plainData, index: 0);

// 支援進度回報
var progress = new Progress<int>(percent => Console.WriteLine($"{percent}%"));
byte[] result = PakTools.Decode(data, 0, progress);
```

### SPR 精靈圖解析

```csharp
using Lin.Helper.Core.Sprite;

// 載入 SPR 檔案
byte[] sprData = File.ReadAllBytes("monster.spr");
SprFrame[] frames = SprReader.Load(sprData);

// 取得每一幀
for (int i = 0; i < frames.Length; i++)
{
    var frame = frames[i];
    Console.WriteLine($"Frame: {frame.Width}x{frame.Height}, Type: {frame.Type}");

    // frame.Image 是 ImageSharp Image<Rgba32>
    frame.Image?.SaveAsPng($"frame_{i}.png");
}
```

### SPR List 解析 (list.spr / wlist.spr)

```csharp
using Lin.Helper.Core.Sprite;

// 從檔案載入
SprListFile sprList = SprListParser.LoadFromFile("list.spr");

// 或從 byte[] 載入
SprListFile sprList = SprListParser.LoadFromBytes(data);

// 遍歷條目
foreach (var entry in sprList.Entries)
{
    Console.WriteLine($"#{entry.Id} {entry.Name} - {entry.ImageCount} images");

    // 動作資訊
    foreach (var action in entry.Actions)
    {
        Console.WriteLine($"  Action: {action.ActionName}, Frames: {action.FrameCount}");
    }

    // 屬性資訊
    foreach (var attr in entry.Attributes)
    {
        Console.WriteLine($"  Attr: {attr.AttributeName} = {attr.RawParameters}");
    }
}
```

### DAT 檔案解密 (Lineage M)

```csharp
using Lin.Helper.Core.Dat;

// 開啟 DAT 檔案
var datFile = new DatFile("resource.dat");
datFile.ParseEntries();

Console.WriteLine($"檔案數量: {datFile.Entries.Count}");

// 遍歷檔案
foreach (var entry in datFile.Entries)
{
    Console.WriteLine($"{entry.Path} ({entry.Size} bytes)");
}

// 提取單一檔案
byte[] content = datFile.ExtractFile(datFile.Entries[0]);

// 匯出所有檔案到資料夾
datFile.ExtractAll("output/folder");
```

### 圖片格式轉換

```csharp
using Lin.Helper.Core.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// IMG 格式 (未壓縮 RGB555)
Image<Rgba32> img = ImageConverter.LoadImg(imgData);

// TBT 格式 (單一圖塊，RLE 壓縮)
Image<Rgba32> tbt = ImageConverter.LoadTbt(tbtData);

// TIL 格式 (地圖圖塊集)
TileSet tileSet = ImageConverter.LoadTil(tilData);
Console.WriteLine($"圖塊數量: {tileSet.TileCount}");

for (int i = 0; i < tileSet.Tiles.Length; i++)
{
    tileSet.Tiles[i]?.SaveAsPng($"tile_{i}.png");
}

// L1Image 格式 (RLE 壓縮圖片)
L1Image l1img = ImageConverter.LoadL1Image(data);
Console.WriteLine($"Offset: ({l1img.XOffset}, {l1img.YOffset})");
Console.WriteLine($"Size: {l1img.Image.Width}x{l1img.Image.Height}");

// 放到指定大小的畫布上
L1Image canvas = ImageConverter.LoadL1Image(data, canvasWidth: 24, canvasHeight: 24);

// RGB555 顏色轉換
Rgba32 color = ImageConverter.Rgb555ToRgba32(0x7C00); // 紅色
```

### 圖片格式轉換 (byte[] 版本 - 無 ImageSharp 依賴)

如果不想使用 ImageSharp，可以使用 `*Raw` 系列方法取得原始 RGBA byte[]：

```csharp
using Lin.Helper.Core.Image;

// IMG 格式
RawImage img = ImageConverter.LoadImgRaw(imgData);
Console.WriteLine($"Size: {img.Width}x{img.Height}");
// img.Pixels 是 RGBA byte[] (Width * Height * 4 bytes)

// TBT 格式
RawImage tbt = ImageConverter.LoadTbtRaw(tbtData);

// TIL 格式
RawTileSet tileSet = ImageConverter.LoadTilRaw(tilData);
foreach (var tile in tileSet.Tiles)
{
    // tile.Pixels 是 RGBA byte[]
}

// L1Image 格式
RawL1Image l1img = ImageConverter.LoadL1ImageRaw(data);
Console.WriteLine($"Offset: ({l1img.XOffset}, {l1img.YOffset})");

// RGB555 轉 RGBA
byte[] rgba = new byte[4];
ImageConverter.Rgb555ToRgba(0x7C00, rgba, 0); // r=255, g=0, b=0, a=255
```

### SPR 精靈圖解析 (byte[] 版本)

```csharp
using Lin.Helper.Core.Sprite;

byte[] sprData = File.ReadAllBytes("monster.spr");
RawSprFrame[] frames = SprReader.LoadRaw(sprData);

foreach (var frame in frames)
{
    Console.WriteLine($"Frame: {frame.Width}x{frame.Height}");
    // frame.Pixels 是 RGBA byte[] (Width * Height * 4 bytes)
}
```

### TIL 進階處理 (版本檢測、降採樣)

```csharp
using Lin.Helper.Core.Tile;

byte[] tilData = File.ReadAllBytes("map.til");

// 自動解壓縮 (支援 Brotli/Zlib)
byte[] decompressed = L1Til.Decompress(tilData);

// 檢測版本
L1Til.TileVersion version = L1Til.GetVersion(tilData);
Console.WriteLine($"Version: {version}"); // Classic, Remaster, Hybrid, Unknown

// 取得 tile 尺寸
int tileSize = L1Til.GetTileSize(tilData); // 24 or 48

// 判斷是否為 Remaster (48x48)
if (L1Til.IsRemaster(tilData))
{
    Console.WriteLine("This is a Remaster (48x48) tile file");
}

// 解析為 block 列表
List<byte[]> blocks = L1Til.Parse(tilData);
Console.WriteLine($"Block count: {blocks.Count}");

// 解析為 TileBlocks (支援共用 block 優化)
L1Til.TileBlocks tileBlocks = L1Til.ParseToTileBlocks(tilData);
Console.WriteLine($"Total: {tileBlocks.Count}, Unique: {tileBlocks.UniqueCount}");

// 分析 block 資訊
var analysis = L1Til.AnalyzeBlock(blocks[0]);
Console.WriteLine($"Type: {analysis.Type}, Format: {analysis.Format}");

// 分析整個 til 檔案
var (classic, remaster, hybrid, unknown) = L1Til.AnalyzeTilBlocks(tilData);
Console.WriteLine($"Classic: {classic}, Remaster: {remaster}, Hybrid: {hybrid}");

// 將 Remaster (48x48) 降採樣為 Classic (24x24)
byte[] downscaled = L1Til.DownscaleTil(tilData);

// 從 block 列表組裝 til 檔案
byte[] rebuilt = L1Til.BuildTil(blocks);

// 從 TileBlocks 組裝 (保留共用結構)
byte[] rebuiltFromBlocks = L1Til.BuildTilFromTileBlocks(tileBlocks);
```

### XML 加解密

```csharp
using Lin.Helper.Core.Xml;

byte[] data = File.ReadAllBytes("config.xml");

// 檢查是否加密
if (XmlCracker.IsEncrypted(data))
{
    // 解密
    byte[] decrypted = XmlCracker.Decrypt(data);

    // 取得正確的編碼
    Encoding encoding = XmlCracker.GetXmlEncoding(decrypted, "config-c.xml");
    string xml = encoding.GetString(decrypted);
}

// 檢查是否為解密後的 XML
if (XmlCracker.IsDecryptedXml(data))
{
    // 加密
    byte[] encrypted = XmlCracker.Encrypt(data);
}
```

---

## 支援的檔案格式

| 格式 | 說明 | 類別 |
|------|------|------|
| `.idx` / `.pak` | PAK 封裝檔索引與資料 | `PakTools` |
| `.spr` | Sprite 動畫圖檔 | `SprReader` |
| `list.spr` / `wlist.spr` | Sprite 清單定義 | `SprListParser` |
| `.dat` | Lineage M 資源封裝檔 | `DatFile` |
| `.img` | 未壓縮圖片 (RGB555) | `ImageConverter` |
| `.tbt` | 單一圖塊 (RLE 壓縮) | `ImageConverter` |
| `.til` | 地圖圖塊集 | `ImageConverter` (轉圖片) / `L1Til` (解析/降採樣) |
| `.xml` | 加密 XML 設定檔 | `XmlCracker` |

---

## 平台支援

- .NET 8.0+
- .NET 9.0+
- .NET 10.0+

支援 Windows、Linux、macOS 等所有 .NET 支援的平台。

> 自 v1.1.0 起，圖片處理改用 [ImageSharp](https://github.com/SixLabors/ImageSharp)，實現完整跨平台支援。

---

## 授權

MIT License

## 作者

Flyworld
