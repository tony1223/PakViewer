
# PakViewer

Cross-platform Lineage 1 PAK file editor.

## Installation

### Windows
Download `*-win-x64.zip` from [Releases](../../releases), extract and run `PakViewer.exe`.

### macOS
Download `*-osx-universal.zip` from [Releases](../../releases).

**Note:** The application is not signed with an Apple Developer certificate. You need to bypass Gatekeeper:

```bash
# After extracting, remove the quarantine attribute
xattr -cr PakViewer

# Or right-click the app and select "Open", then click "Open" in the dialog
```

Alternatively, go to **System Preferences > Security & Privacy > General** and click "Open Anyway" after the first blocked launch attempt.

### Linux
Download `*-linux-x64.zip` from [Releases](../../releases).

```bash
# Extract and set executable permission
unzip PakViewer-*-linux-x64.zip
chmod +x PakViewer
./PakViewer
```

## Features

### GUI Mode

Support pak file operations:

1. Add file
2. Read content
3. Update content in application
4. Delete file
5. Search and filter files
6. Edit text files with encoding-aware save
7. SPR sprite preview with animation
8. TIL tile map viewer
9. Gallery browsing mode with thumbnails
10. Multi-IDX browsing (All IDX mode)

### CLI Mode

Run with `-cli` flag for command-line operations:

```
PakViewer -cli <command> [arguments]
```

#### Commands

**list** - List files in PAK
```
PakViewer -cli list <idx_file> [filter]
```
Example:
```
PakViewer -cli list Text.idx
PakViewer -cli list Text.idx html
```

**read** - Read and display file content
```
PakViewer -cli read <idx_file> <filename> [encoding]
```
Example:
```
PakViewer -cli read Text.idx 07bearNPC-c.html
```

**export** - Export file from PAK to disk
```
PakViewer -cli export <idx_file> <filename> <output_file> [encoding]
```
Example:
```
PakViewer -cli export Text.idx 07bearNPC-c.html output.html
```

**import** - Import file from disk into PAK
```
PakViewer -cli import <idx_file> <filename> <input_file> [encoding]
```
Note: File must be same size as original.

Example:
```
PakViewer -cli import Text.idx 07bearNPC-c.html modified.html
```

**info** - Show PAK file information
```
PakViewer -cli info <idx_file>
```

#### Encodings

Supported encodings: `big5`, `euc-kr`, `shift_jis`, `gb2312`, `utf-8`

Auto-detection by filename:
- `-c.` suffix: Big5 (Traditional Chinese)
- `-k.` suffix: EUC-KR (Korean)
- `-j.` suffix: Shift_JIS (Japanese)
- `-h.` suffix: GB2312 (Simplified Chinese)

## Third-Party Libraries

- [oxipng](https://github.com/oxipng/oxipng) v10.0.0 - Lossless PNG compression tool (MIT License)
  - Used for optimizing PNG files without quality loss
  - Embedded as a resource and extracted on first use

## Credits

Original project was from 99net's moore.
