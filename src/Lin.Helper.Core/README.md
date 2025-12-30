# Lineage.FileFormats

Lineage 1 檔案格式處理函式庫，提供 PAK、SPR、DAT、IMG、TIL 等遊戲資源檔案的讀取與處理功能。

## 功能

- **PAK 檔案處理** - 讀取、解密、匯出 Lineage 1 PAK 封裝檔
- **SPR 圖檔解析** - 解析 Sprite 動畫格式
- **DAT 檔案解密** - Lineage M DAT 檔案 AES 解密
- **XML 加解密** - 遊戲設定檔 XML 加解密
- **圖片格式轉換** - IMG、TIL、TBT 等格式轉換

## 安裝

```bash
dotnet add package Lineage.FileFormats
```

## 使用範例

### PAK 檔案讀取

```csharp
using Lineage.FileFormats.Pak;

// 讀取 PAK 索引
var entries = PakTools.ReadIndex("path/to/file.idx");

// 讀取檔案內容
byte[] data = PakTools.ReadFile("path/to/file.idx", "filename.xml");
```

### DAT 檔案解密

```csharp
using Lineage.FileFormats.Dat;

// 開啟 DAT 檔案
var datFile = new DatFile("path/to/file.dat");
datFile.ParseEntries();

// 提取單一檔案
byte[] content = datFile.ExtractFile(datFile.Entries[0]);

// 匯出所有檔案
datFile.ExtractAll("output/folder");
```

### XML 解密

```csharp
using Lineage.FileFormats.Xml;

// 檢查是否加密
if (XmlCracker.IsEncrypted(data))
{
    byte[] decrypted = XmlCracker.Decrypt(data);
}
```

## 支援的檔案格式

| 格式 | 說明 | 類別 |
|------|------|------|
| `.idx` / `.pak` | PAK 封裝檔 | `PakTools` |
| `.spr` | Sprite 動畫 | `SprReader` |
| `.dat` | Lineage M 資源檔 | `DatFile` |
| `.img` | 圖片格式 | `ImageConverter` |
| `.til` | 地圖圖塊 | `ImageConverter` |
| `.xml` | 加密 XML | `XmlCracker` |

## 授權

MIT License
