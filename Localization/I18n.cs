using System;
using System.Collections.Generic;
using System.Globalization;

namespace PakViewer.Localization
{
    /// <summary>
    /// 國際化 (i18n) 管理器
    /// </summary>
    public static class I18n
    {
        private static string _currentLanguage = "zh-TW";
        private static readonly Dictionary<string, Dictionary<string, string>> _translations;

        public static event Action LanguageChanged;

        public static string CurrentLanguage => _currentLanguage;

        public static string[] AvailableLanguages => new[] { "zh-TW", "ja-JP", "en-US", "ko-KR" };

        public static Dictionary<string, string> LanguageNames => new()
        {
            { "zh-TW", "Chinese Traditional" },
            { "ja-JP", "Japanese" },
            { "en-US", "English" },
            { "ko-KR", "Korean" }
        };

        static I18n()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>
            {
                { "zh-TW", GetChineseTraditional() },
                { "ja-JP", GetJapanese() },
                { "en-US", GetEnglish() },
                { "ko-KR", GetKorean() }
            };
        }

        /// <summary>
        /// 設定當前語言
        /// </summary>
        public static void SetLanguage(string langCode)
        {
            if (_translations.ContainsKey(langCode))
            {
                _currentLanguage = langCode;
                LanguageChanged?.Invoke();
            }
        }

        /// <summary>
        /// 取得翻譯字串
        /// </summary>
        public static string T(string key)
        {
            if (_translations.TryGetValue(_currentLanguage, out var dict))
            {
                if (dict.TryGetValue(key, out var value))
                    return value;
            }
            // Fallback to English
            if (_translations.TryGetValue("en-US", out var enDict))
            {
                if (enDict.TryGetValue(key, out var enValue))
                    return enValue;
            }
            return key; // Return key if not found
        }

        /// <summary>
        /// 取得帶參數的翻譯字串
        /// </summary>
        public static string T(string key, params object[] args)
        {
            var template = T(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        #region 繁體中文
        private static Dictionary<string, string> GetChineseTraditional() => new()
        {
            // Window
            { "AppTitle", "PakViewer (跨平台)" },

            // Menu - File
            { "Menu.File", "檔案(&F)" },
            { "Menu.File.OpenFolder", "開啟資料夾(&O)..." },
            { "Menu.File.OpenIdx", "開啟 IDX 檔案(&I)..." },
            { "Menu.File.OpenSprList", "開啟 SPR List (list.spr)(&L)..." },
            { "Menu.File.OpenDat", "開啟 Lineage M Icon/Image DAT(&M)..." },
            { "Menu.File.Quit", "結束(&Q)" },

            // Menu - Tools
            { "Menu.Tools", "工具(&T)" },
            { "Menu.Tools.ExportSelected", "匯出選取(&E)..." },
            { "Menu.Tools.ExportAll", "匯出全部(&A)..." },
            { "Menu.Tools.DeleteSelected", "刪除選取(&D)" },

            // Menu - Edit
            { "Menu.Edit", "編輯(&E)" },
            { "Menu.Edit.Language", "語言(&L)" },

            // Menu - Language (top-level)
            { "Menu.Language", "語言(&L)" },

            // Labels
            { "Label.Folder", "資料夾:" },
            { "Label.IDX", "IDX:" },
            { "Label.Ext", "副檔名:" },
            { "Label.Lang", "語言:" },
            { "Label.Type", "類型:" },
            { "Label.Filter", "篩選:" },
            { "Label.Search", "搜尋:" },
            { "Label.Find", "尋找:" },
            { "Label.Mode", "模式:" },
            { "Label.Zoom", "縮放:" },
            { "Label.Background", "背景:" },
            { "Color.Black", "黑色" },
            { "Color.Red", "紅色" },
            { "Color.Transparent", "透明" },
            { "Color.White", "白色" },
            { "Color.Green", "綠色" },
            { "Color.Blue", "藍色" },
            { "Color.Gray", "灰色" },
            { "Label.Scale", "縮放:" },

            // Mode
            { "Mode.Normal", "一般" },
            { "Mode.SPR", "SPR" },
            { "Mode.SPRList", "SPR List" },

            // Buttons
            { "Button.Open", "開啟..." },
            { "Button.Search", "搜尋" },
            { "Button.Clear", "清除" },
            { "Button.Play", "▶ 播放" },
            { "Button.Stop", "停止播放" },
            { "Button.Save", "儲存變更" },
            { "Button.Export", "匯出" },
            { "Button.LoadMore", "顯示更多 (+500)" },

            // Grid Headers
            { "Grid.No", "編號" },
            { "Grid.FileName", "檔案名稱" },
            { "Grid.Size", "大小" },
            { "Grid.IDX", "IDX" },
            { "Grid.ID", "ID" },
            { "Grid.Name", "名稱" },
            { "Grid.SpriteId", "圖檔" },
            { "Grid.ImageCount", "圖數" },
            { "Grid.Type", "類型" },
            { "Grid.Actions", "動作" },

            // Filters
            { "Filter.All", "全部" },
            { "Filter.AllTypes", "全部類型" },
            { "Filter.Unreferenced", "未引用" },

            // SPR Types
            { "SprType.0", "影子/法術" },
            { "SprType.1", "裝飾品" },
            { "SprType.5", "玩家/NPC" },
            { "SprType.6", "可對話NPC" },
            { "SprType.7", "寶箱/開關" },
            { "SprType.8", "門" },
            { "SprType.9", "物品" },
            { "SprType.10", "怪物" },
            { "SprType.11", "城牆/城門" },
            { "SprType.12", "新NPC" },

            // Checkboxes
            { "Check.SprListMode", "SPR List 模式" },
            { "Check.SprMode", "SPR 模式" },

            // SPR Mode
            { "SprMode.NotSet", "(未設定)" },
            { "SprMode.AllSprIdx", "[全部SPR (sprite.idx sprite00-15.idx)]" },
            { "SprMode.AllSprite", "[全部 sprite]" },

            // View modes
            { "View.List", "列表" },
            { "View.Gallery", "相簿" },

            // Context Menu
            { "Context.OpenInNewTab", "在新分頁開啟" },
            { "Context.ExportSelected", "匯出選取" },
            { "Context.ExportSelectedTo", "匯出選取至..." },
            { "Context.DeleteSelected", "刪除選取" },
            { "Context.CopyFilename", "複製檔名" },
            { "Context.SelectAll", "全選" },
            { "Context.UnselectAll", "取消全選" },
            { "Context.CloseTab", "關閉分頁" },
            { "Context.Delete", "刪除(&D)" },
            { "Context.SaveAs", "另存新檔(&S)..." },

            // Status
            { "Status.Ready", "就緒" },
            { "Status.Loading", "載入中: {0}..." },
            { "Status.Loaded", "已載入: {0}" },
            { "Status.LoadedSprList", "已載入 SPR List: {0} ({1} 條目)" },
            { "Status.Records", "記錄: {0} / {1}" },
            { "Status.Exported", "已匯出 {0} 個檔案" },
            { "Status.ExportFailed", "匯出失敗: {0} 個檔案" },
            { "Status.Deleted", "已刪除 {0} 個檔案" },
            { "Status.SaveSuccess", "儲存成功" },
            { "Status.SaveFailed", "儲存失敗" },
            { "Status.Selected", "已選取" },
            { "Status.SelectionCleared", "已清除選取" },
            { "Status.ItemsDeleted", "已刪除 {0} 個條目" },
            { "Status.Saved", "已儲存" },

            // Confirm
            { "Confirm.DeleteItems", "確定要刪除 {0} 個選取的條目嗎？" },
            { "Confirm.DeleteSprFiles", "以下 SPR 檔案將從 sprite*.idx 中刪除:\n\n{0}" },

            // Status
            { "Status.AndMore", "還有 {0} 個" },

            // Error
            { "Error.NoItemSelected", "請先勾選要操作的條目" },
            { "Error.NoFileLoaded", "尚未載入檔案" },
            { "Error.SaveFailed", "儲存失敗" },

            // Dialog
            { "Dialog.SaveAs", "另存新檔" },

            // Filter
            { "Filter.SprList", "SPR List 檔案" },

            // Viewer
            { "Viewer.SelectEntry", "請在左側選擇一個條目" },
            { "Viewer.NoActions", "此條目沒有動作" },
            { "Viewer.NoFrames", "此動作沒有幀" },
            { "Viewer.Images", "張" },
            { "Viewer.Frames", "幀" },
            { "Viewer.TotalDurationValue", "總時長" },
            { "Viewer.NoImageData", "(無圖片資料)" },
            { "Viewer.AnimPreview", "動畫預覽" },
            { "Viewer.Stopped", "已停止" },
            { "Viewer.Playing", "播放: {0} [{1}/{2}]" },
            { "Viewer.Frame", "幀: {0}/{1}" },
            { "Viewer.Directional", "有向" },
            { "Viewer.NonDirectional", "無向" },
            { "Viewer.Direction", "方向" },
            { "Viewer.Speed", "播放速度" },
            { "Speed.Standard", "1/24 (標準)" },
            { "Speed.Slow", "10/24 (慢動作)" },
            { "Speed.Custom", "自訂" },
            { "Viewer.TotalDuration", "總時長" },
            { "Viewer.File", "檔案" },
            { "Viewer.SprFrame", "SPR幀" },

            // TIL Viewer
            { "Til.Block", "Block {0}" },
            { "Til.Flags", "Flags: {0}" },
            { "Til.Modified", "已修改" },
            { "Til.SaveHint", "變更已套用。請使用 Export 功能儲存檔案。" },

            // Dialogs
            { "Dialog.OpenFolder", "選擇資料夾" },
            { "Dialog.OpenFile", "開啟檔案" },
            { "Dialog.SaveFile", "儲存檔案" },
            { "Dialog.ExportFolder", "選擇匯出資料夾" },
            { "Dialog.Confirm", "確認" },
            { "Dialog.Error", "錯誤" },
            { "Dialog.Warning", "警告" },
            { "Dialog.Info", "資訊" },
            { "Dialog.DeleteConfirm", "確定要刪除選取的 {0} 個檔案嗎？" },

            // Toast
            { "Toast.SaveSuccess", "儲存成功" },
            { "Toast.FileSaved", "檔案已儲存至 PAK" },
            // Tab/Browser
            { "Tab.LinClient", "Lin Client" },
            { "Tab.CloseOthers", "關閉其他分頁" },
            { "Tab.CloseAll", "關閉所有分頁" },

            // Grid Headers (additional)
            { "Grid.Offset", "偏移" },
            { "Grid.Parts", "部件" },

            // Placeholder
            { "Placeholder.SearchInText", "搜尋文字..." },
            { "Placeholder.TypeToFilter", "輸入篩選..." },
            { "Placeholder.Search", "搜尋..." },
            { "Placeholder.ID", "ID..." },

            // Status (additional)
            { "Status.SearchingContent", "搜尋內容中..." },
            { "Status.ContentSearchCleared", "已清除內容搜尋" },
            { "Status.UseViewerSearch", "請使用檢視器內建搜尋" },
            { "Status.NotFound", "找不到" },
            { "Status.BinaryFile", "二進制檔案" },
            { "Status.Encrypted", "已加密" },
            { "Status.Saving", "儲存中..." },

            // Errors
            { "Error.LoadImage", "無法載入圖片" },
            { "Error.LoadSpr", "無法載入 SPR 檔案" },
            { "Error.LoadL1Image", "無法載入 L1Image" },
            { "Error.LoadTil", "無法載入 TIL 檔案" },

            // TIL Viewer (additional)
            { "Til.Background", "背景:" },
            { "Til.TransparentBg", "□透明背景" },

            // Buttons (additional)
            { "Button.Pause", "⏸ 暫停" },
            { "Button.Cancel", "取消" },
            { "Button.OK", "確定" },
            { "Button.Find", "尋找" },
            { "Button.Prev", "上一個" },
            { "Button.Next", "下一個" },

            // Right panel mode
            { "RightPanel.Preview", "預覽" },
            { "RightPanel.Gallery", "相簿" },
            { "RightPanel.ThumbnailSize", "縮圖:" },

            // Viewer Dialog
            { "ViewerDialog.Title", "詳細檢視" },
        };
        #endregion

        #region 日本語
        private static Dictionary<string, string> GetJapanese() => new()
        {
            // Window
            { "AppTitle", "PakViewer (クロスプラットフォーム)" },

            // Menu - File
            { "Menu.File", "ファイル(&F)" },
            { "Menu.File.OpenFolder", "フォルダを開く(&O)..." },
            { "Menu.File.OpenIdx", "IDX ファイルを開く(&I)..." },
            { "Menu.File.OpenSprList", "SPR List を開く (list.spr)(&L)..." },
            { "Menu.File.OpenDat", "Lineage M Icon/Image DAT を開く(&M)..." },
            { "Menu.File.Quit", "終了(&Q)" },

            // Menu - Tools
            { "Menu.Tools", "ツール(&T)" },
            { "Menu.Tools.ExportSelected", "選択をエクスポート(&E)..." },
            { "Menu.Tools.ExportAll", "すべてエクスポート(&A)..." },
            { "Menu.Tools.DeleteSelected", "選択を削除(&D)" },

            // Menu - Edit
            { "Menu.Edit", "編集(&E)" },
            { "Menu.Edit.Language", "言語(&L)" },

            // Menu - Language (top-level)
            { "Menu.Language", "言語(&L)" },

            // Labels
            { "Label.Folder", "フォルダ:" },
            { "Label.IDX", "IDX:" },
            { "Label.Ext", "拡張子:" },
            { "Label.Lang", "言語:" },
            { "Label.Type", "タイプ:" },
            { "Label.Filter", "フィルタ:" },
            { "Label.Search", "検索:" },
            { "Label.Find", "検索:" },
            { "Label.Mode", "モード:" },
            { "Label.Zoom", "拡大:" },
            { "Label.Background", "背景:" },
            { "Color.Black", "黒" },
            { "Color.Red", "赤" },
            { "Color.Transparent", "透明" },
            { "Color.White", "白" },
            { "Color.Green", "緑" },
            { "Color.Blue", "青" },
            { "Color.Gray", "灰色" },
            { "Label.Scale", "拡大:" },

            // Mode
            { "Mode.Normal", "通常" },
            { "Mode.SPR", "SPR" },
            { "Mode.SPRList", "SPR List" },

            // Buttons
            { "Button.Open", "開く..." },
            { "Button.Search", "検索" },
            { "Button.Clear", "クリア" },
            { "Button.Play", "▶ 再生" },
            { "Button.Stop", "停止" },
            { "Button.Save", "保存" },
            { "Button.Export", "エクスポート" },
            { "Button.LoadMore", "もっと表示 (+500)" },

            // Grid Headers
            { "Grid.No", "番号" },
            { "Grid.FileName", "ファイル名" },
            { "Grid.Size", "サイズ" },
            { "Grid.IDX", "IDX" },
            { "Grid.ID", "ID" },
            { "Grid.Name", "名前" },
            { "Grid.SpriteId", "スプライト" },
            { "Grid.ImageCount", "画像数" },
            { "Grid.Type", "タイプ" },
            { "Grid.Actions", "アクション" },

            // Filters
            { "Filter.All", "すべて" },
            { "Filter.AllTypes", "すべてのタイプ" },
            { "Filter.Unreferenced", "未参照" },

            // SPR Types
            { "SprType.0", "影/魔法" },
            { "SprType.1", "装飾品" },
            { "SprType.5", "プレイヤー/NPC" },
            { "SprType.6", "対話NPC" },
            { "SprType.7", "宝箱/スイッチ" },
            { "SprType.8", "ドア" },
            { "SprType.9", "アイテム" },
            { "SprType.10", "モンスター" },
            { "SprType.11", "城壁/城門" },
            { "SprType.12", "新NPC" },

            // Checkboxes
            { "Check.SprListMode", "SPR List モード" },
            { "Check.SprMode", "SPR モード" },

            // SPR Mode
            { "SprMode.NotSet", "(未設定)" },
            { "SprMode.AllSprIdx", "[全SPR (sprite.idx sprite00-15.idx)]" },
            { "SprMode.AllSprite", "[全スプライト]" },

            // View modes
            { "View.List", "リスト" },
            { "View.Gallery", "ギャラリー" },

            // Context Menu
            { "Context.OpenInNewTab", "新しいタブで開く" },
            { "Context.ExportSelected", "選択をエクスポート" },
            { "Context.ExportSelectedTo", "選択をエクスポート先..." },
            { "Context.DeleteSelected", "選択を削除" },
            { "Context.CopyFilename", "ファイル名をコピー" },
            { "Context.SelectAll", "すべて選択" },
            { "Context.UnselectAll", "選択解除" },
            { "Context.CloseTab", "タブを閉じる" },
            { "Context.Delete", "削除(&D)" },
            { "Context.SaveAs", "名前を付けて保存(&S)..." },

            // Status
            { "Status.Ready", "準備完了" },
            { "Status.Loading", "読み込み中: {0}..." },
            { "Status.Loaded", "読み込み完了: {0}" },
            { "Status.LoadedSprList", "SPR List 読み込み完了: {0} ({1} エントリ)" },
            { "Status.Records", "レコード: {0} / {1}" },
            { "Status.Exported", "{0} ファイルをエクスポートしました" },
            { "Status.ExportFailed", "エクスポート失敗: {0} ファイル" },
            { "Status.Deleted", "{0} ファイルを削除しました" },
            { "Status.SaveSuccess", "保存成功" },
            { "Status.SaveFailed", "保存失敗" },
            { "Status.Selected", "選択中" },
            { "Status.SelectionCleared", "選択を解除しました" },
            { "Status.ItemsDeleted", "{0} エントリを削除しました" },
            { "Status.Saved", "保存しました" },

            // Confirm
            { "Confirm.DeleteItems", "{0} 個の選択されたエントリを削除しますか？" },
            { "Confirm.DeleteSprFiles", "以下のSPRファイルがsprite*.idxから削除されます:\n\n{0}" },

            // Status
            { "Status.AndMore", "他 {0} 個" },

            // Error
            { "Error.NoItemSelected", "操作するエントリをチェックしてください" },
            { "Error.NoFileLoaded", "ファイルが読み込まれていません" },
            { "Error.SaveFailed", "保存に失敗しました" },

            // Dialog
            { "Dialog.SaveAs", "名前を付けて保存" },

            // Filter
            { "Filter.SprList", "SPR List ファイル" },

            // Viewer
            { "Viewer.SelectEntry", "左側でエントリを選択してください" },
            { "Viewer.NoActions", "このエントリにはアクションがありません" },
            { "Viewer.NoFrames", "このアクションにはフレームがありません" },
            { "Viewer.Images", "枚" },
            { "Viewer.Frames", "フレーム" },
            { "Viewer.TotalDurationValue", "合計時間" },
            { "Viewer.NoImageData", "(画像データなし)" },
            { "Viewer.AnimPreview", "アニメーションプレビュー" },
            { "Viewer.Stopped", "停止" },
            { "Viewer.Playing", "再生中: {0} [{1}/{2}]" },
            { "Viewer.Frame", "フレーム: {0}/{1}" },
            { "Viewer.Directional", "方向あり" },
            { "Viewer.NonDirectional", "方向なし" },
            { "Viewer.Direction", "方向" },
            { "Viewer.Speed", "再生速度" },
            { "Speed.Standard", "1/24 (標準)" },
            { "Speed.Slow", "10/24 (スロー)" },
            { "Speed.Custom", "カスタム" },
            { "Viewer.TotalDuration", "合計時間" },
            { "Viewer.File", "ファイル" },
            { "Viewer.SprFrame", "SPRフレーム" },

            // TIL Viewer
            { "Til.Block", "ブロック {0}" },
            { "Til.Flags", "フラグ: {0}" },
            { "Til.Modified", "変更済み" },
            { "Til.SaveHint", "変更が適用されました。Export 機能でファイルを保存してください。" },

            // Dialogs
            { "Dialog.OpenFolder", "フォルダを選択" },
            { "Dialog.OpenFile", "ファイルを開く" },
            { "Dialog.SaveFile", "ファイルを保存" },
            { "Dialog.ExportFolder", "エクスポート先を選択" },
            { "Dialog.Confirm", "確認" },
            { "Dialog.Error", "エラー" },
            { "Dialog.Warning", "警告" },
            { "Dialog.Info", "情報" },
            { "Dialog.DeleteConfirm", "選択した {0} ファイルを削除しますか？" },

            // Toast
            { "Toast.SaveSuccess", "保存成功" },
            { "Toast.FileSaved", "ファイルを PAK に保存しました" },
            // Tab/Browser
            { "Tab.LinClient", "Lin Client" },
            { "Tab.CloseOthers", "他のタブを閉じる" },
            { "Tab.CloseAll", "すべてのタブを閉じる" },

            // Grid Headers (additional)
            { "Grid.Offset", "オフセット" },
            { "Grid.Parts", "パーツ" },

            // Placeholder
            { "Placeholder.SearchInText", "テキストを検索..." },
            { "Placeholder.TypeToFilter", "フィルター..." },
            { "Placeholder.Search", "検索..." },
            { "Placeholder.ID", "ID..." },

            // Status (additional)
            { "Status.SearchingContent", "コンテンツを検索中..." },
            { "Status.ContentSearchCleared", "コンテンツ検索をクリアしました" },
            { "Status.UseViewerSearch", "ビューアの検索機能を使用してください" },
            { "Status.NotFound", "見つかりません" },
            { "Status.BinaryFile", "バイナリファイル" },
            { "Status.Encrypted", "暗号化" },
            { "Status.Saving", "保存中..." },

            // Errors
            { "Error.LoadImage", "画像を読み込めません" },
            { "Error.LoadSpr", "SPR ファイルを読み込めません" },
            { "Error.LoadL1Image", "L1Image を読み込めません" },
            { "Error.LoadTil", "TIL ファイルを読み込めません" },

            // TIL Viewer (additional)
            { "Til.Background", "背景:" },
            { "Til.TransparentBg", "□透明背景" },

            // Buttons (additional)
            { "Button.Pause", "⏸ 一時停止" },
            { "Button.Cancel", "キャンセル" },
            { "Button.OK", "OK" },
            { "Button.Find", "検索" },
            { "Button.Prev", "前へ" },
            { "Button.Next", "次へ" },

            // Right panel mode
            { "RightPanel.Preview", "プレビュー" },
            { "RightPanel.Gallery", "ギャラリー" },
            { "RightPanel.ThumbnailSize", "サムネイル:" },

            // Viewer Dialog
            { "ViewerDialog.Title", "詳細ビュー" },
        };
        #endregion

        #region English
        private static Dictionary<string, string> GetEnglish() => new()
        {
            // Window
            { "AppTitle", "PakViewer (Cross-Platform)" },

            // Menu - File
            { "Menu.File", "&File" },
            { "Menu.File.OpenFolder", "&Open Folder..." },
            { "Menu.File.OpenIdx", "Open &IDX File..." },
            { "Menu.File.OpenSprList", "Open SPR &List (list.spr)..." },
            { "Menu.File.OpenDat", "Open Lineage &M Icon/Image DAT..." },
            { "Menu.File.Quit", "&Quit" },

            // Menu - Tools
            { "Menu.Tools", "&Tools" },
            { "Menu.Tools.ExportSelected", "&Export Selected..." },
            { "Menu.Tools.ExportAll", "Export &All..." },
            { "Menu.Tools.DeleteSelected", "&Delete Selected" },

            // Menu - Edit
            { "Menu.Edit", "&Edit" },
            { "Menu.Edit.Language", "&Language" },

            // Menu - Language (top-level)
            { "Menu.Language", "&Language" },

            // Labels
            { "Label.Folder", "Folder:" },
            { "Label.IDX", "IDX:" },
            { "Label.Ext", "Ext:" },
            { "Label.Lang", "Lang:" },
            { "Label.Type", "Type:" },
            { "Label.Filter", "Filter:" },
            { "Label.Search", "Search:" },
            { "Label.Find", "Find:" },
            { "Label.Mode", "Mode:" },
            { "Label.Zoom", "Zoom:" },
            { "Label.Background", "BG:" },
            { "Color.Black", "Black" },
            { "Color.Red", "Red" },
            { "Color.Transparent", "Transparent" },
            { "Color.White", "White" },
            { "Color.Green", "Green" },
            { "Color.Blue", "Blue" },
            { "Color.Gray", "Gray" },
            { "Label.Scale", "Scale:" },

            // Mode
            { "Mode.Normal", "Normal" },
            { "Mode.SPR", "SPR" },
            { "Mode.SPRList", "SPR List" },

            // Buttons
            { "Button.Open", "Open..." },
            { "Button.Search", "Search" },
            { "Button.Clear", "Clear" },
            { "Button.Play", "▶ Play" },
            { "Button.Stop", "Stop" },
            { "Button.Save", "Save Changes" },
            { "Button.Export", "Export" },
            { "Button.LoadMore", "Load More (+500)" },

            // Grid Headers
            { "Grid.No", "No." },
            { "Grid.FileName", "File Name" },
            { "Grid.Size", "Size" },
            { "Grid.IDX", "IDX" },
            { "Grid.ID", "ID" },
            { "Grid.Name", "Name" },
            { "Grid.SpriteId", "Sprite" },
            { "Grid.ImageCount", "Images" },
            { "Grid.Type", "Type" },
            { "Grid.Actions", "Actions" },

            // Filters
            { "Filter.All", "All" },
            { "Filter.AllTypes", "All Types" },
            { "Filter.Unreferenced", "Unreferenced" },

            // SPR Types
            { "SprType.0", "Shadow/Magic" },
            { "SprType.1", "Accessory" },
            { "SprType.5", "Player/NPC" },
            { "SprType.6", "Talkable NPC" },
            { "SprType.7", "Chest/Switch" },
            { "SprType.8", "Door" },
            { "SprType.9", "Item" },
            { "SprType.10", "Monster" },
            { "SprType.11", "Castle Wall/Gate" },
            { "SprType.12", "New NPC" },

            // Checkboxes
            { "Check.SprListMode", "SPR List Mode" },
            { "Check.SprMode", "SPR Mode" },

            // SPR Mode
            { "SprMode.NotSet", "(Not set)" },
            { "SprMode.AllSprIdx", "[All SPR (sprite.idx sprite00-15.idx)]" },
            { "SprMode.AllSprite", "[All Sprites]" },

            // View modes
            { "View.List", "List" },
            { "View.Gallery", "Gallery" },

            // Context Menu
            { "Context.OpenInNewTab", "Open in New Tab" },
            { "Context.ExportSelected", "Export Selected" },
            { "Context.ExportSelectedTo", "Export Selected To..." },
            { "Context.DeleteSelected", "Delete Selected" },
            { "Context.CopyFilename", "Copy Filename" },
            { "Context.SelectAll", "Select All" },
            { "Context.UnselectAll", "Unselect All" },
            { "Context.CloseTab", "Close Tab" },
            { "Context.Delete", "Delete(&D)" },
            { "Context.SaveAs", "Save As(&S)..." },

            // Status
            { "Status.Ready", "Ready" },
            { "Status.Loading", "Loading: {0}..." },
            { "Status.Loaded", "Loaded: {0}" },
            { "Status.LoadedSprList", "Loaded SPR List: {0} ({1} entries)" },
            { "Status.Records", "Records: {0} / {1}" },
            { "Status.Exported", "Exported {0} files" },
            { "Status.ExportFailed", "Export failed: {0} files" },
            { "Status.Deleted", "Deleted {0} files" },
            { "Status.SaveSuccess", "Save Successful" },
            { "Status.SaveFailed", "Save Failed" },
            { "Status.Selected", "Selected" },
            { "Status.SelectionCleared", "Selection cleared" },
            { "Status.ItemsDeleted", "{0} entries deleted" },
            { "Status.Saved", "Saved" },

            // Confirm
            { "Confirm.DeleteItems", "Are you sure you want to delete {0} selected entries?" },
            { "Confirm.DeleteSprFiles", "The following SPR files will be deleted from sprite*.idx:\n\n{0}" },

            // Status
            { "Status.AndMore", "and {0} more" },

            // Error
            { "Error.NoItemSelected", "Please check the items to operate on" },
            { "Error.NoFileLoaded", "No file loaded" },
            { "Error.SaveFailed", "Save failed" },

            // Dialog
            { "Dialog.SaveAs", "Save As" },

            // Filter
            { "Filter.SprList", "SPR List Files" },

            // Viewer
            { "Viewer.SelectEntry", "Please select an entry on the left" },
            { "Viewer.NoActions", "This entry has no actions" },
            { "Viewer.NoFrames", "This action has no frames" },
            { "Viewer.Images", " images" },
            { "Viewer.Frames", " frames" },
            { "Viewer.TotalDurationValue", "Total" },
            { "Viewer.NoImageData", "(No image data)" },
            { "Viewer.AnimPreview", "Animation Preview" },
            { "Viewer.Stopped", "Stopped" },
            { "Viewer.Playing", "Playing: {0} [{1}/{2}]" },
            { "Viewer.Frame", "Frame: {0}/{1}" },
            { "Viewer.Directional", "Directional" },
            { "Viewer.NonDirectional", "Non-directional" },
            { "Viewer.Direction", "Direction" },
            { "Viewer.Speed", "Speed" },
            { "Speed.Standard", "1/24 (Standard)" },
            { "Speed.Slow", "10/24 (Slow)" },
            { "Speed.Custom", "Custom" },
            { "Viewer.TotalDuration", "Total Duration" },
            { "Viewer.File", "File" },
            { "Viewer.SprFrame", "SPR Frame" },

            // TIL Viewer
            { "Til.Block", "Block {0}" },
            { "Til.Flags", "Flags: {0}" },
            { "Til.Modified", "Modified" },
            { "Til.SaveHint", "Changes applied. Use Export to save the file." },

            // Dialogs
            { "Dialog.OpenFolder", "Select Folder" },
            { "Dialog.OpenFile", "Open File" },
            { "Dialog.SaveFile", "Save File" },
            { "Dialog.ExportFolder", "Select Export Folder" },
            { "Dialog.Confirm", "Confirm" },
            { "Dialog.Error", "Error" },
            { "Dialog.Warning", "Warning" },
            { "Dialog.Info", "Information" },
            { "Dialog.DeleteConfirm", "Are you sure you want to delete {0} selected files?" },

            // Toast
            { "Toast.SaveSuccess", "Save Successful" },
            { "Toast.FileSaved", "File saved to PAK" },
            // Tab/Browser
            { "Tab.LinClient", "Lin Client" },
            { "Tab.CloseOthers", "Close Other Tabs" },
            { "Tab.CloseAll", "Close All Tabs" },

            // Grid Headers (additional)
            { "Grid.Offset", "Offset" },
            { "Grid.Parts", "Parts" },

            // Placeholder
            { "Placeholder.SearchInText", "Search in text..." },
            { "Placeholder.TypeToFilter", "Type to filter..." },
            { "Placeholder.Search", "Search..." },
            { "Placeholder.ID", "ID..." },

            // Status (additional)
            { "Status.SearchingContent", "Searching content..." },
            { "Status.ContentSearchCleared", "Content search cleared" },
            { "Status.UseViewerSearch", "Use viewer's built-in search" },
            { "Status.NotFound", "Not found" },
            { "Status.BinaryFile", "Binary file" },
            { "Status.Encrypted", "Encrypted" },
            { "Status.Saving", "Saving..." },

            // Errors
            { "Error.LoadImage", "Failed to load image" },
            { "Error.LoadSpr", "Failed to load SPR file" },
            { "Error.LoadL1Image", "Failed to load L1Image" },
            { "Error.LoadTil", "Failed to load TIL file" },

            // TIL Viewer (additional)
            { "Til.Background", "Background:" },
            { "Til.TransparentBg", "□Transparent" },

            // Buttons (additional)
            { "Button.Pause", "⏸ Pause" },
            { "Button.Cancel", "Cancel" },
            { "Button.OK", "OK" },
            { "Button.Find", "Find" },
            { "Button.Prev", "Previous" },
            { "Button.Next", "Next" },

            // Right panel mode
            { "RightPanel.Preview", "Preview" },
            { "RightPanel.Gallery", "Gallery" },
            { "RightPanel.ThumbnailSize", "Thumbnail:" },

            // Viewer Dialog
            { "ViewerDialog.Title", "Detail View" },
        };
        #endregion

        #region 한국어
        private static Dictionary<string, string> GetKorean() => new()
        {
            // Window
            { "AppTitle", "PakViewer (크로스 플랫폼)" },

            // Menu - File
            { "Menu.File", "파일(&F)" },
            { "Menu.File.OpenFolder", "폴더 열기(&O)..." },
            { "Menu.File.OpenIdx", "IDX 파일 열기(&I)..." },
            { "Menu.File.OpenSprList", "SPR List 열기 (list.spr)(&L)..." },
            { "Menu.File.OpenDat", "Lineage M Icon/Image DAT 열기(&M)..." },
            { "Menu.File.Quit", "종료(&Q)" },

            // Menu - Tools
            { "Menu.Tools", "도구(&T)" },
            { "Menu.Tools.ExportSelected", "선택 항목 내보내기(&E)..." },
            { "Menu.Tools.ExportAll", "모두 내보내기(&A)..." },
            { "Menu.Tools.DeleteSelected", "선택 항목 삭제(&D)" },

            // Menu - Edit
            { "Menu.Edit", "편집(&E)" },
            { "Menu.Edit.Language", "언어(&L)" },

            // Menu - Language (top-level)
            { "Menu.Language", "언어(&L)" },

            // Labels
            { "Label.Folder", "폴더:" },
            { "Label.IDX", "IDX:" },
            { "Label.Ext", "확장자:" },
            { "Label.Lang", "언어:" },
            { "Label.Type", "유형:" },
            { "Label.Filter", "필터:" },
            { "Label.Search", "검색:" },
            { "Label.Find", "찾기:" },
            { "Label.Mode", "모드:" },
            { "Label.Zoom", "확대:" },
            { "Label.Background", "배경:" },
            { "Color.Black", "검정" },
            { "Color.Red", "빨강" },
            { "Color.Transparent", "투명" },
            { "Color.White", "흰색" },
            { "Color.Green", "초록" },
            { "Color.Blue", "파랑" },
            { "Color.Gray", "회색" },
            { "Label.Scale", "배율:" },

            // Mode
            { "Mode.Normal", "일반" },
            { "Mode.SPR", "SPR" },
            { "Mode.SPRList", "SPR List" },

            // Buttons
            { "Button.Open", "열기..." },
            { "Button.Search", "검색" },
            { "Button.Clear", "지우기" },
            { "Button.Play", "▶ 재생" },
            { "Button.Stop", "정지" },
            { "Button.Save", "변경사항 저장" },
            { "Button.Export", "내보내기" },
            { "Button.LoadMore", "더 보기 (+500)" },

            // Grid Headers
            { "Grid.No", "번호" },
            { "Grid.FileName", "파일명" },
            { "Grid.Size", "크기" },
            { "Grid.IDX", "IDX" },
            { "Grid.ID", "ID" },
            { "Grid.Name", "이름" },
            { "Grid.SpriteId", "스프라이트" },
            { "Grid.ImageCount", "이미지 수" },
            { "Grid.Type", "유형" },
            { "Grid.Actions", "동작" },

            // Filters
            { "Filter.All", "전체" },
            { "Filter.AllTypes", "모든 유형" },
            { "Filter.Unreferenced", "미참조" },

            // SPR Types
            { "SprType.0", "그림자/마법" },
            { "SprType.1", "장신구" },
            { "SprType.5", "플레이어/NPC" },
            { "SprType.6", "대화 NPC" },
            { "SprType.7", "상자/스위치" },
            { "SprType.8", "문" },
            { "SprType.9", "아이템" },
            { "SprType.10", "몬스터" },
            { "SprType.11", "성벽/성문" },
            { "SprType.12", "신규 NPC" },

            // Checkboxes
            { "Check.SprListMode", "SPR List 모드" },
            { "Check.SprMode", "SPR 모드" },

            // SPR Mode
            { "SprMode.NotSet", "(설정 안됨)" },
            { "SprMode.AllSprIdx", "[모든 SPR (sprite.idx sprite00-15.idx)]" },

            // View modes
            { "View.List", "목록" },
            { "View.Gallery", "갤러리" },

            // Context Menu
            { "Context.OpenInNewTab", "새 탭에서 열기" },
            { "Context.ExportSelected", "선택 항목 내보내기" },
            { "Context.ExportSelectedTo", "선택 항목 내보내기..." },
            { "Context.DeleteSelected", "선택 항목 삭제" },
            { "Context.CopyFilename", "파일명 복사" },
            { "Context.SelectAll", "모두 선택" },
            { "Context.UnselectAll", "선택 해제" },
            { "Context.CloseTab", "탭 닫기" },
            { "Context.Delete", "삭제(&D)" },
            { "Context.SaveAs", "다른 이름으로 저장(&S)..." },

            // Status
            { "Status.Ready", "준비" },
            { "Status.Loading", "로딩 중: {0}..." },
            { "Status.Loaded", "로드됨: {0}" },
            { "Status.LoadedSprList", "SPR List 로드됨: {0} ({1} 항목)" },
            { "Status.Records", "레코드: {0} / {1}" },
            { "Status.Exported", "{0}개 파일 내보내기 완료" },
            { "Status.ExportFailed", "내보내기 실패: {0}개 파일" },
            { "Status.Deleted", "{0}개 파일 삭제됨" },
            { "Status.SaveSuccess", "저장 성공" },
            { "Status.SaveFailed", "저장 실패" },
            { "Status.Selected", "선택됨" },
            { "Status.SelectionCleared", "선택 해제됨" },
            { "Status.ItemsDeleted", "{0}개 항목 삭제됨" },
            { "Status.Saved", "저장됨" },

            // Confirm
            { "Confirm.DeleteItems", "{0}개의 선택된 항목을 삭제하시겠습니까?" },
            { "Confirm.DeleteSprFiles", "다음 SPR 파일이 sprite*.idx에서 삭제됩니다:\n\n{0}" },

            // Status
            { "Status.AndMore", "외 {0}개" },

            // Error
            { "Error.NoItemSelected", "작업할 항목을 체크하세요" },
            { "Error.NoFileLoaded", "파일이 로드되지 않았습니다" },
            { "Error.SaveFailed", "저장 실패" },

            // Dialog
            { "Dialog.SaveAs", "다른 이름으로 저장" },

            // Filter
            { "Filter.SprList", "SPR List 파일" },

            // Viewer
            { "Viewer.SelectEntry", "왼쪽에서 항목을 선택하세요" },
            { "Viewer.NoActions", "이 항목에는 동작이 없습니다" },
            { "Viewer.NoFrames", "이 동작에는 프레임이 없습니다" },
            { "Viewer.Images", "개" },
            { "Viewer.Frames", "프레임" },
            { "Viewer.TotalDurationValue", "총 시간" },
            { "Viewer.NoImageData", "(이미지 데이터 없음)" },
            { "Viewer.AnimPreview", "애니메이션 미리보기" },
            { "Viewer.Stopped", "정지됨" },
            { "Viewer.Playing", "재생 중: {0} [{1}/{2}]" },
            { "Viewer.Frame", "프레임: {0}/{1}" },
            { "Viewer.Directional", "방향성" },
            { "Viewer.NonDirectional", "무방향" },
            { "Viewer.Direction", "방향" },
            { "Viewer.Speed", "재생 속도" },
            { "Speed.Standard", "1/24 (표준)" },
            { "Speed.Slow", "10/24 (슬로우)" },
            { "Speed.Custom", "사용자 정의" },
            { "Viewer.TotalDuration", "총 시간" },
            { "Viewer.File", "파일" },
            { "Viewer.SprFrame", "SPR 프레임" },

            // TIL Viewer
            { "Til.Block", "블록 {0}" },
            { "Til.Flags", "플래그: {0}" },
            { "Til.Modified", "수정됨" },
            { "Til.SaveHint", "변경사항이 적용되었습니다. Export 기능으로 파일을 저장하세요." },

            // Dialogs
            { "Dialog.OpenFolder", "폴더 선택" },
            { "Dialog.OpenFile", "파일 열기" },
            { "Dialog.SaveFile", "파일 저장" },
            { "Dialog.ExportFolder", "내보내기 폴더 선택" },
            { "Dialog.Confirm", "확인" },
            { "Dialog.Error", "오류" },
            { "Dialog.Warning", "경고" },
            { "Dialog.Info", "정보" },
            { "Dialog.DeleteConfirm", "선택한 {0}개 파일을 삭제하시겠습니까?" },

            // Toast
            { "Toast.SaveSuccess", "저장 성공" },
            { "Toast.FileSaved", "파일이 PAK에 저장되었습니다" },
            // Tab/Browser
            { "Tab.LinClient", "Lin Client" },
            { "Tab.CloseOthers", "다른 탭 닫기" },
            { "Tab.CloseAll", "모든 탭 닫기" },

            // Grid Headers (additional)
            { "Grid.Offset", "오프셋" },
            { "Grid.Parts", "파츠" },

            // Placeholder
            { "Placeholder.SearchInText", "텍스트 검색..." },
            { "Placeholder.TypeToFilter", "필터..." },
            { "Placeholder.Search", "검색..." },
            { "Placeholder.ID", "ID..." },

            // Status (additional)
            { "Status.SearchingContent", "콘텐츠 검색 중..." },
            { "Status.ContentSearchCleared", "콘텐츠 검색 초기화됨" },
            { "Status.UseViewerSearch", "뷰어의 내장 검색을 사용하세요" },
            { "Status.NotFound", "찾을 수 없음" },
            { "Status.BinaryFile", "바이너리 파일" },
            { "Status.Encrypted", "암호화됨" },
            { "Status.Saving", "저장 중..." },

            // Errors
            { "Error.LoadImage", "이미지를 로드할 수 없습니다" },
            { "Error.LoadSpr", "SPR 파일을 로드할 수 없습니다" },
            { "Error.LoadL1Image", "L1Image를 로드할 수 없습니다" },
            { "Error.LoadTil", "TIL 파일을 로드할 수 없습니다" },

            // TIL Viewer (additional)
            { "Til.Background", "배경:" },
            { "Til.TransparentBg", "□투명 배경" },

            // Buttons (additional)
            { "Button.Pause", "⏸ 일시정지" },
            { "Button.Cancel", "취소" },
            { "Button.OK", "확인" },
            { "Button.Find", "찾기" },
            { "Button.Prev", "이전" },
            { "Button.Next", "다음" },

            // Right panel mode
            { "RightPanel.Preview", "미리보기" },
            { "RightPanel.Gallery", "갤러리" },
            { "RightPanel.ThumbnailSize", "썸네일:" },

            // Viewer Dialog
            { "ViewerDialog.Title", "상세 보기" },
        };
        #endregion
    }
}
