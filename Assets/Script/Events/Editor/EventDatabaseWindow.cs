using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    public sealed class EventDatabaseWindow : OdinEditorWindow
    {
        private enum CategoryFilterMode { All, Unassigned, Specific }

        [MenuItem("Tools/Events/Event Database")]
        private static void Open() => GetWindow<EventDatabaseWindow>("Event Database").Show();

        internal static void OpenAndSelect(EventDefinitionSO definition)
        {
            var window = GetWindow<EventDatabaseWindow>("Event Database");
            window.SetSelected(definition);
            window.Refresh();
            window.Show();
            window.Focus();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        [BoxGroup("カタログ"), OnValueChanged(nameof(OnCatalogChanged))] public EventCatalogSO catalog;
        [BoxGroup("検索")] public string search;
        [BoxGroup("検索")] public string tag;
        [BoxGroup("旧データ移行"), OnValueChanged(nameof(OnLegacyListChanged))] public EventListSO legacyList;
        private EventDefinitionSO selected;
        private EventDefinitionInlineEditor selectedEditor;
        [NonSerialized] private CategoryFilterMode categoryFilterMode;
        [NonSerialized] private string categoryFilterId;
        [NonSerialized] private readonly Dictionary<int, string> legacyCategoryMappingIds = new Dictionary<int, string>();
        private EventDefinitionSO[] definitions = new EventDefinitionSO[0];
        private List<EventValidationIssue> issues = new List<EventValidationIssue>();
        private Vector2 listScroll;
        private Vector2 issueScroll;
        private Vector2 legacyMappingScroll;

        protected override void OnEnable() { base.OnEnable(); Undo.undoRedoPerformed += OnUndoRedo; Refresh(); }
        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (selectedEditor != null) DestroyImmediate(selectedEditor);
            base.OnDisable();
        }
        private void OnUndoRedo() { catalog?.categoryCatalog?.InvalidateCache(); Refresh(); }
        private void OnCatalogChanged() { legacyCategoryMappingIds.Clear(); Refresh(); }
        [Button("一覧を更新"), BoxGroup("検索")]
        public void Refresh()
        {
            definitions = AssetDatabase.FindAssets("t:EventDefinitionSO").Select(g => AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).OrderBy(d => d.sortOrder).ThenBy(d => d.eventCode, StringComparer.Ordinal).ToArray();
        }

        [OnInspectorGUI, PropertyOrder(-1)]
        private void DrawList()
        {
            EditorGUILayout.LabelField("イベント一覧", EditorStyles.boldLabel);
            DrawCategoryFilter();
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(180));
            foreach (var definition in definitions.Where(d => Contains(d.eventCode, search) || Contains(d.displayName, search))
                .Where(MatchesCategoryFilter)
                .Where(d => string.IsNullOrEmpty(tag) || (d.tags?.Any(t => string.Equals(tag, t, StringComparison.OrdinalIgnoreCase)) ?? false)))
            {
                if (GUILayout.Button($"{definition.eventCode}   {definition.displayName}   [{CategoryLabel(definition)}]", selected == definition ? EditorStyles.toolbarButton : EditorStyles.miniButton))
                { SetSelected(definition); EditorGUIUtility.PingObject(definition); }
            }
            EditorGUILayout.EndScrollView();
        }
        private static bool Contains(string value, string query) => string.IsNullOrEmpty(query) || (value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

        private void DrawCategoryFilter()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて", categoryFilterMode == CategoryFilterMode.All ? EditorStyles.toolbarButton : EditorStyles.miniButtonLeft)) categoryFilterMode = CategoryFilterMode.All;
            if (GUILayout.Button("未設定", categoryFilterMode == CategoryFilterMode.Unassigned ? EditorStyles.toolbarButton : EditorStyles.miniButtonMid)) categoryFilterMode = CategoryFilterMode.Unassigned;
            string specific = categoryFilterMode == CategoryFilterMode.Specific && catalog?.categoryCatalog != null && catalog.categoryCatalog.TryGetById(categoryFilterId, out var category)
                ? category.DisplayName + (category.IsDeprecated ? " [使用停止]" : string.Empty) : "特定カテゴリ…";
            if (GUILayout.Button(specific, categoryFilterMode == CategoryFilterMode.Specific ? EditorStyles.toolbarButton : EditorStyles.miniButtonRight))
            {
                categoryFilterMode = CategoryFilterMode.Specific;
                if (catalog?.categoryCatalog != null)
                    EventCategoryPickerWindow.Open(catalog.categoryCatalog, categoryFilterId, true, null, id =>
                    { categoryFilterId = id; categoryFilterMode = string.IsNullOrEmpty(id) ? CategoryFilterMode.Unassigned : CategoryFilterMode.Specific; Repaint(); });
            }
            EditorGUILayout.EndHorizontal();
            if (catalog == null) EditorGUILayout.HelpBox("カテゴリフィルターを使うにはEventCatalogSOを選択してください。", MessageType.Info);
            else if (catalog.categoryCatalog == null) EditorGUILayout.HelpBox("選択中のEventCatalogSOにカテゴリカタログがありません。", MessageType.Warning);
        }

        private bool MatchesCategoryFilter(EventDefinitionSO definition)
        {
            switch (categoryFilterMode)
            {
                case CategoryFilterMode.Unassigned: return string.IsNullOrEmpty(definition.CategoryId);
                case CategoryFilterMode.Specific: return !string.IsNullOrEmpty(categoryFilterId) && definition.CategoryId == categoryFilterId;
                default: return true;
            }
        }

        private string CategoryLabel(EventDefinitionSO definition)
        {
            if (string.IsNullOrEmpty(definition.CategoryId)) return "未設定";
            return catalog?.categoryCatalog != null && catalog.categoryCatalog.TryGetById(definition.CategoryId, out var category)
                ? category.DisplayName + (category.IsDeprecated ? " / 使用停止" : string.Empty)
                : "未登録カテゴリ";
        }

        [Button("新規イベント"), BoxGroup("編集")]
        private void Create()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Event", "NewEvent", "asset", "Choose the new asset location.");
            if (string.IsNullOrEmpty(path)) return;
            selected = CreateAsset(path, null);
            RegisterSelected();
            Refresh();
        }

        [Button("新しいGUIDで複製"), BoxGroup("編集")]
        private void Duplicate()
        {
            if (selected == null) return;
            var path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(selected));
            selected = CreateAsset(path, selected);
            RegisterSelected();
            Refresh();
        }

        internal static EventDefinitionSO CreateAsset(string path, EventDefinitionSO source, string categoryId = null, string legacyCategory = null)
        {
            var asset = source == null ? CreateInstance<EventDefinitionSO>() : Instantiate(source);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("persistentId").stringValue = Guid.NewGuid().ToString("N");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (source == null) EventCategoryEditorUtility.InitializeNewAsset(asset, categoryId, legacyCategory);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.eventCode = UniqueCode(source == null ? "EVENT_001" : source.eventCode + "_COPY");
            if (source == null) asset.displayName = asset.name;
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Event");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static string UniqueCode(string basis)
        {
            var codes = new HashSet<string>(AssetDatabase.FindAssets("t:EventDefinitionSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)).eventCode), StringComparer.OrdinalIgnoreCase);
            var code = basis;
            int number = 2;
            while (codes.Contains(code)) code = basis + "_" + number++;
            return code;
        }

        [Button("選択イベントをカタログへ登録"), BoxGroup("編集")]
        private void RegisterSelected()
        {
            if (catalog == null || selected == null || catalog.definitions.Contains(selected)) return;
            Undo.RecordObject(catalog, "Register Event");
            catalog.definitions.Add(selected);
            EditorUtility.SetDirty(catalog);
            catalog.Initialize();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        [Button("新規カタログ"), BoxGroup("カタログ")]
        private void CreateCatalog()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Catalog", "EventCatalog", "asset", "Choose a catalog location.");
            if (string.IsNullOrEmpty(path)) return;
            catalog = CreateInstance<EventCatalogSO>();
            AssetDatabase.CreateAsset(catalog, path);
            Undo.RegisterCreatedObjectUndo(catalog, "Create Event Catalog");
        }

        [Button("カテゴリカタログを作成・接続"), BoxGroup("カタログ")]
        private void CreateCategoryCatalog()
        {
            if (catalog == null) { EditorUtility.DisplayDialog("Event Category Catalog", "先にEventCatalogSOを選択または作成してください。", "OK"); return; }
            EventCategoryCatalogAssetMenu.CreateAndConnect(catalog);
            Refresh();
        }

        [Button("カテゴリ管理を開く"), BoxGroup("カタログ")]
        private void OpenCategoryManager()
        {
            if (catalog?.categoryCatalog == null) { EditorUtility.DisplayDialog("Event Categories", "カテゴリカタログを設定してください。", "OK"); return; }
            EventCategoryManagerWindow.Open(catalog.categoryCatalog, selected?.CategoryId);
        }

        [Button("全イベント検査"), BoxGroup("検査")]
        private void ValidateAll()
        {
            Refresh();
            issues = EventValidator.Validate(definitions, catalog);
            if (catalog == null) issues.Add(new EventValidationIssue { message = "Select a catalog to check registration.", severity = EventIssueSeverity.Warning });
        }

        [OnInspectorGUI, BoxGroup("検査")]
        private void DrawIssues()
        {
            EditorGUILayout.LabelField($"Errors: {issues.Count(i => i.severity == EventIssueSeverity.Error)} / Warnings: {issues.Count(i => i.severity == EventIssueSeverity.Warning)}");
            issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.MaxHeight(220));
            foreach (var issue in issues)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(issue.ToString(), issue.severity == EventIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                if (issue.asset != null && GUILayout.Button("開く", GUILayout.Width(45)))
                { if (issue.asset is EventDefinitionSO definition) SetSelected(definition); Selection.activeObject = issue.asset; EditorGUIUtility.PingObject(issue.asset); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        [OnInspectorGUI, BoxGroup("参照")]
        private void DrawReferences()
        {
            if (selected == null) return;
            foreach (var source in definitions.Where(d => d.outcomes != null && d.outcomes.Where(o => o != null).Any(o =>
                (o.followUps?.Any(f => f?.definition == selected) ?? false) || (o.effects?.Any(e => e?.eventDefinition == selected) ?? false))))
                if (GUILayout.Button("参照元: " + source.eventCode)) { SetSelected(source); break; }
        }

        [OnInspectorGUI, BoxGroup("選択イベント")]
        private void DrawSelectedInspector()
        {
            if (selected == null) { EditorGUILayout.LabelField("イベントを一覧から選択してください。", EditorStyles.miniLabel); return; }
            if (selectedEditor == null || selectedEditor.target != selected)
            {
                if (selectedEditor != null) DestroyImmediate(selectedEditor);
                selectedEditor = UnityEditor.Editor.CreateEditor(selected, typeof(EventDefinitionInlineEditor)) as EventDefinitionInlineEditor;
            }
            if (selectedEditor == null) return;
            selectedEditor.CatalogOverride = catalog;
            selectedEditor.OnInspectorGUI();
        }

        [Button("新イベントの旧カテゴリ文字列を移行"), BoxGroup("旧データ移行")]
        private void OpenCategoryMigration() => EventCategoryMigrationWindow.Open(catalog, selected);

        private void OnLegacyListChanged()
        {
            legacyCategoryMappingIds.Clear();
            Repaint();
        }

        [OnInspectorGUI, BoxGroup("旧データ移行")]
        private void DrawLegacyCategoryMappings()
        {
            if (legacyList == null) return;
            if (catalog?.categoryCatalog == null)
            {
                EditorGUILayout.HelpBox("旧カテゴリの対応先を選ぶには、移行先EventCatalogSOへカテゴリカタログを設定してください。", MessageType.Warning);
                return;
            }

            var legacyCategories = (legacyList.evCategoryList ?? new List<EventCategory>()).Where(c => c != null).Distinct().ToArray();
            EditorGUILayout.LabelField("旧カテゴリ → 共有カテゴリ対応", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("厳密一致は候補として使えます。それ以外は検索から明示的に選択してください。", EditorStyles.miniLabel);
            legacyMappingScroll = EditorGUILayout.BeginScrollView(legacyMappingScroll, GUILayout.MaxHeight(180));
            foreach (var legacyCategory in legacyCategories)
            {
                TryResolveLegacyImportCategory(legacyCategory, out var mapped, out var reason);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.IsNullOrEmpty(legacyCategory.evCategoryName) ? "<空欄>" : legacyCategory.evCategoryName,
                    GUILayout.MinWidth(120));
                EditorGUILayout.LabelField(mapped == null ? "未解決" : mapped.DisplayName, GUILayout.MinWidth(120));
                if (GUILayout.Button("検索して選択", GUILayout.Width(105)))
                {
                    int key = legacyCategory.GetInstanceID();
                    EventCategoryPickerWindow.Open(catalog.categoryCatalog, mapped?.Id, false, null, id =>
                    {
                        if (string.IsNullOrEmpty(id)) legacyCategoryMappingIds.Remove(key);
                        else legacyCategoryMappingIds[key] = id;
                        Repaint();
                    });
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        [Button("旧リストから新アセットを作成"), BoxGroup("旧データ移行")]
        private void ImportLegacy()
        {
            if (legacyList == null || catalog == null) { Debug.LogWarning("Select a legacy list and a destination catalog."); return; }
            if (catalog.categoryCatalog == null) { Debug.LogWarning("Assign a category catalog before importing legacy events."); return; }
            var legacyCategories = (legacyList.evCategoryList ?? new List<EventCategory>()).Where(c => c != null).ToArray();
            var mappings = new Dictionary<EventCategory, EventCategoryDefinition>();
            foreach (var legacyCategory in legacyCategories)
            {
                if (!TryResolveLegacyImportCategory(legacyCategory, out var mapped, out var reason))
                {
                    EditorUtility.DisplayDialog("Legacy Event Import",
                        $"旧カテゴリ '{legacyCategory.evCategoryName}' の対応先が未解決です。{reason}\n対応表で既存カテゴリを選ぶか新規追加してから再実行してください。何も取り込みませんでした。", "OK");
                    return;
                }
                mappings[legacyCategory] = mapped;
            }
            var folder = EditorUtility.OpenFolderPanel("Destination inside Assets", Application.dataPath, "");
            if (string.IsNullOrEmpty(folder)) return;
            var relative = FileUtil.GetProjectRelativePath(folder);
            if (relative != "Assets" && !relative.StartsWith("Assets/", StringComparison.Ordinal)) { Debug.LogError("Choose a folder inside Assets."); return; }
            foreach (var legacyCategory in legacyCategories)
            {
                var oldEvents = legacyCategory.evDatas?.Where(e => e != null).ToList() ?? new List<EventData>();
                if (oldEvents.Count == 0) oldEvents.Add(new EventData { id = legacyCategory.evCategoryName, icon = legacyCategory.catIcon });
                foreach (var old in oldEvents)
                {
                    SetSelected(CreateAsset(AssetDatabase.GenerateUniqueAssetPath(relative + "/LegacyEvent.asset"), null,
                        mappings[legacyCategory].Id, legacyCategory.evCategoryName));
                    selected.displayName = old.id;
                    selected.appearance.mapIcon = old.icon != null ? old.icon : legacyCategory.catIcon;
                    selected.enabled = false;
                    selected.developerMemo = $"Imported from {AssetDatabase.GetAssetPath(legacyList)} / {old.id}.\nLegacy likeLevel={old.likeLevel}, mysteryLevel={old.mysteryLevel}.\n旧データに時間・文章・判定・報酬の定義がないため、内容を設定し検査後に有効化してください。";
                    EditorUtility.SetDirty(selected);
                    AssetDatabase.SaveAssetIfDirty(selected);
                    RegisterSelected();
                }
            }
            Refresh();
            ValidateAll();
        }

        private bool TryResolveLegacyImportCategory(EventCategory legacyCategory, out EventCategoryDefinition mapped, out string reason)
        {
            mapped = null;
            if (legacyCategory == null || catalog?.categoryCatalog == null)
            {
                reason = "カテゴリカタログがありません。";
                return false;
            }

            int key = legacyCategory.GetInstanceID();
            if (legacyCategoryMappingIds.TryGetValue(key, out var selectedId))
            {
                if (!catalog.categoryCatalog.TryGetById(selectedId, out mapped))
                {
                    reason = "手動選択したIDが現在のカタログに存在しません。選び直してください。";
                    return false;
                }
                if (mapped.IsDeprecated)
                {
                    reason = $"手動選択した '{mapped.DisplayName}' は使用停止中です。使用中カテゴリを選んでください。";
                    mapped = null;
                    return false;
                }
                reason = "手動で確認した対応先です。";
                return true;
            }

            if (catalog.categoryCatalog.TryGetByStrictDisplayName(legacyCategory.evCategoryName, out mapped) && !mapped.IsDeprecated)
            {
                reason = "大文字小文字を含めて表示名が厳密一致する一意候補です。取り込み実行時にこの対応を使用します。";
                return true;
            }
            mapped = null;
            reason = "厳密一致する使用中カテゴリが1件ではありません。自動対応付けしません。";
            return false;
        }

        private void SetSelected(EventDefinitionSO definition)
        {
            if (selected == definition) return;
            selected = definition;
            if (selectedEditor != null) DestroyImmediate(selectedEditor);
            selectedEditor = null;
            Repaint();
        }
    }
}
