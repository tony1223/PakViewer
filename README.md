
# PakViewer

L1j's pak file editor.

## Features

### GUI Mode

Support pak file operations:

1. Add file
2. Read content
3. Update content in application
4. Delete file
5. Search and filter files
6. Edit text files with encoding-aware save

### CLI Mode

Run with `-cli` flag for command-line operations:

```
PakViewer.exe -cli <command> [arguments]
```

#### Commands

**list** - List files in PAK
```
PakViewer.exe -cli list <idx_file> [filter]
```
Example:
```
PakViewer.exe -cli list Text.idx
PakViewer.exe -cli list Text.idx html
```

**read** - Read and display file content
```
PakViewer.exe -cli read <idx_file> <filename> [encoding]
```
Example:
```
PakViewer.exe -cli read Text.idx 07bearNPC-c.html
```

**export** - Export file from PAK to disk
```
PakViewer.exe -cli export <idx_file> <filename> <output_file> [encoding]
```
Example:
```
PakViewer.exe -cli export Text.idx 07bearNPC-c.html output.html
```

**import** - Import file from disk into PAK
```
PakViewer.exe -cli import <idx_file> <filename> <input_file> [encoding]
```
Note: File must be same size as original.

Example:
```
PakViewer.exe -cli import Text.idx 07bearNPC-c.html modified.html
```

**info** - Show PAK file information
```
PakViewer.exe -cli info <idx_file>
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
