using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    internal static class EventCategoryEditorLifecycle
    {
        internal static void SaveAfterCurrentGui(UnityEngine.Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            if (!AssetDatabase.Contains(target)) return;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            EditorApplication.delayCall += () =>
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var liveTarget = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (liveTarget != null && EditorUtility.IsDirty(liveTarget)) AssetDatabase.SaveAssetIfDirty(liveTarget);
            };
        }

        internal static void CloseAfterCurrentGui(EditorWindow window)
        {
            if (window == null) return;
            EditorApplication.delayCall += () =>
            {
                if (window != null) window.Close();
            };
        }

        internal static void OpenManagerAfterCurrentGui(EventCategoryCatalogSO catalog)
        {
            if (catalog == null) return;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog));
            EditorApplication.delayCall += () =>
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var liveCatalog = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<EventCategoryCatalogSO>(path);
                if (liveCatalog == null) return;
                Selection.activeObject = liveCatalog;
                EventCategoryManagerWindow.Open(liveCatalog, null);
            };
        }
    }

    [InitializeOnLoad]
    internal static class EventCategoryUndoCache
    {
        static EventCategoryUndoCache() => Undo.undoRedoPerformed += OnUndoRedo;

        private static void OnUndoRedo()
        {
            foreach (var catalog in Resources.FindObjectsOfTypeAll<EventCategoryCatalogSO>()) catalog.InvalidateCache();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }

    internal static class EventCategoryEditorUtility
    {
        private const string CategoryIdProperty = "categoryId";
        private const string MigrationStateProperty = "categoryMigrationState";

        internal static bool TryAssign(EventDefinitionSO definition, EventCategoryCatalogSO catalog, string categoryId,
            string undoName, out string error)
        {
            if (definition == null) { error = "イベントが選択されていません。"; return false; }
            if (!string.IsNullOrEmpty(categoryId))
            {
                if (catalog == null || !catalog.TryGetById(categoryId, out var category))
                { error = $"カテゴリID '{categoryId}' をカタログから解決できません。"; return false; }
                if (category.IsDeprecated)
                { error = $"使用停止カテゴリ '{category.DisplayName}' は新しく選択できません。"; return false; }
            }
            if (definition.CategoryId == categoryId && definition.CategoryMigrationState == EventCategoryMigrationState.Migrated)
            { error = null; return true; }

            Undo.RecordObject(definition, undoName);
            SetSerializedCategory(definition, categoryId, EventCategoryMigrationState.Migrated);
            Save(definition);
            error = null;
            RepaintAll();
            return true;
        }

        internal static bool TryCreate(EventCategoryCatalogSO catalog, string displayName, string description, int sortOrder,
            EventDefinitionSO assignTo, out EventCategoryDefinition created, out string error)
        {
            created = null;
            if (catalog == null) { error = "カテゴリカタログが設定されていません。"; return false; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(assignTo == null ? "Create Event Category" : "Create and Assign Event Category");
            Undo.RecordObject(catalog, "Create Event Category");
            if (assignTo != null) Undo.RecordObject(assignTo, "Assign Event Category");
            try
            {
                if (!catalog.EditorTryAdd(displayName, description, sortOrder, out created, out error)) return false;
                if (assignTo != null) SetSerializedCategory(assignTo, created.Id, EventCategoryMigrationState.Migrated);
                catalog.InvalidateCache();
                Save(catalog);
                if (assignTo != null) Save(assignTo);
                Undo.CollapseUndoOperations(group);
                RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                created = null;
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryUpdate(EventCategoryCatalogSO catalog, string id, string displayName, string description,
            int sortOrder, bool isDeprecated, out string error)
        {
            if (catalog == null) { error = "カテゴリカタログが設定されていません。"; return false; }
            Undo.RecordObject(catalog, "Edit Event Category");
            if (!catalog.EditorTryUpdate(id, displayName, description, sortOrder, isDeprecated, out error)) return false;
            catalog.InvalidateCache();
            Save(catalog);
            RepaintAll();
            return true;
        }

        internal static bool TryApplyMigration(EventDefinitionSO definition, EventCategoryCatalogSO catalog, string chosenId, out string error)
        {
            if (definition == null) { error = "イベントがありません。"; return false; }
            if (definition.CategoryMigrationState == EventCategoryMigrationState.Migrated)
            { error = null; return true; }
            if (!string.IsNullOrEmpty(definition.CategoryId) && definition.CategoryId != chosenId)
            { error = "新しいカテゴリ選択が既にあります。旧値で上書きしません。"; return false; }
            if (string.IsNullOrEmpty(definition.LegacyCategory) && !string.IsNullOrEmpty(chosenId))
            { error = "旧カテゴリが空欄のため、移行では未設定として確定してください。カテゴリの設定は通常の選択欄から行えます。"; return false; }
            if (!string.IsNullOrEmpty(chosenId) && (catalog == null || !catalog.TryGetById(chosenId, out _)))
            { error = $"カテゴリID '{chosenId}' を解決できません。"; return false; }

            Undo.RecordObject(definition, "Apply Event Category Migration");
            SetSerializedCategory(definition, string.IsNullOrEmpty(definition.CategoryId) ? chosenId : definition.CategoryId,
                EventCategoryMigrationState.Migrated);
            Save(definition);
            error = null;
            RepaintAll();
            return true;
        }

        internal static void InitializeNewAsset(EventDefinitionSO definition, string categoryId = null, string legacyCategory = null)
        {
            if (definition == null) return;
            definition.category = legacyCategory ?? string.Empty;
            SetSerializedCategory(definition, categoryId ?? string.Empty, EventCategoryMigrationState.Migrated);
        }

        private static void SetSerializedCategory(EventDefinitionSO definition, string categoryId, EventCategoryMigrationState state)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(CategoryIdProperty).stringValue = categoryId ?? string.Empty;
            serialized.FindProperty(MigrationStateProperty).enumValueIndex = (int)state;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(UnityEngine.Object target)
        {
            EventCategoryEditorLifecycle.SaveAfterCurrentGui(target);
        }

        internal static void RepaintAll()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        internal static EventDefinitionSO[] FindAllDefinitions() => AssetDatabase.FindAssets("t:EventDefinitionSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null).ToArray();
    }

    internal static class EventCategoryCatalogResolver
    {
        internal static bool TryResolve(EventDefinitionSO definition, EventCatalogSO explicitCatalog,
            out EventCategoryCatalogSO categoryCatalog, out string message)
        {
            categoryCatalog = null;
            if (explicitCatalog != null)
            {
                if (explicitCatalog.categoryCatalog == null)
                { message = "選択中のEventCatalogSOにカテゴリカタログが設定されていません。Event Databaseの「カテゴリカタログを作成・接続」またはEventCatalogSOのInspectorで設定してください。"; return false; }
                categoryCatalog = explicitCatalog.categoryCatalog;
                message = "Event Databaseで選択中のEventCatalogSOから解決しました。";
                return true;
            }

            var owners = AssetDatabase.FindAssets("t:EventCatalogSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.definitions != null && c.definitions.Contains(definition)).ToArray();
            if (owners.Length == 0)
            { message = "このイベントを登録しているEventCatalogSOがありません。先にEvent Databaseでイベントをカタログへ登録してください。"; return false; }
            if (owners.Any(c => c.categoryCatalog == null))
            { message = "このイベントを登録しているEventCatalogSOの一部にカテゴリカタログが設定されていません。すべての所属カタログへ明示的に設定してください。"; return false; }
            var distinct = owners.Select(c => c.categoryCatalog).Distinct().ToArray();
            if (distinct.Length != 1)
            { message = "このイベントは異なるカテゴリカタログを持つ複数のEventCatalogSOに登録されています。所属または参照先を統一してください。"; return false; }
            categoryCatalog = distinct[0];
            message = owners.Length == 1 ? $"登録先: {owners[0].name}" : $"同じカテゴリカタログを使う登録先 {owners.Length}件から解決しました。";
            return true;
        }
    }

    internal static class EventCategorySelectorGUI
    {
        private static readonly HashSet<int> ShowingIds = new HashSet<int>();

        internal static void Draw(EventDefinitionSO definition, EventCatalogSO explicitCatalog = null)
        {
            if (definition == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("共有イベントカテゴリ", EditorStyles.boldLabel);
            if (!EventCategoryCatalogResolver.TryResolve(definition, explicitCatalog, out var catalog, out var sourceMessage))
            {
                EditorGUILayout.HelpBox(sourceMessage, MessageType.Warning);
                DrawRawId(definition);
                DrawLegacy(definition, null);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(sourceMessage, EditorStyles.miniLabel);
            string label;
            MessageType messageType = MessageType.None;
            if (string.IsNullOrEmpty(definition.CategoryId)) label = "未設定";
            else if (!catalog.TryGetById(definition.CategoryId, out var category))
            { label = $"未登録ID: {definition.CategoryId}"; messageType = MessageType.Error; }
            else
            {
                label = category.DisplayName;
                if (category.IsDeprecated) messageType = MessageType.Warning;
            }
            EditorGUILayout.LabelField("選択中", label);
            if (messageType == MessageType.Warning) EditorGUILayout.HelpBox("このカテゴリは使用停止中です。既存参照は維持されますが、新規選択候補には表示されません。", messageType);
            if (messageType == MessageType.Error) EditorGUILayout.HelpBox("設定されたIDをカテゴリカタログから解決できません。", messageType);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("検索して選択"))
                EventCategoryPickerWindow.Open(catalog, definition.CategoryId, false, definition, id =>
                    EventCategoryEditorUtility.TryAssign(definition, catalog, id, "Select Event Category", out _));
            if (GUILayout.Button("未設定に戻す"))
                EventCategoryEditorUtility.TryAssign(definition, catalog, string.Empty, "Clear Event Category", out _);
            if (GUILayout.Button("新規追加")) EventCategoryCreateWindow.Open(catalog, definition, null);
            if (GUILayout.Button("カテゴリ管理")) EventCategoryManagerWindow.Open(catalog, definition.CategoryId);
            EditorGUILayout.EndHorizontal();
            DrawRawId(definition);
            DrawLegacy(definition, explicitCatalog);
            EditorGUILayout.EndVertical();
        }

        private static void DrawRawId(EventDefinitionSO definition)
        {
            int key = definition.GetInstanceID();
            bool show = ShowingIds.Contains(key);
            bool next = EditorGUILayout.Foldout(show, "内部IDを確認", true);
            if (next != show) { if (next) ShowingIds.Add(key); else ShowingIds.Remove(key); }
            if (next)
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Category ID", definition.CategoryId ?? string.Empty);
            }
        }

        private static void DrawLegacy(EventDefinitionSO definition, EventCatalogSO explicitCatalog)
        {
            if (definition.CategoryMigrationState != EventCategoryMigrationState.Pending) return;
            EditorGUILayout.HelpBox($"旧カテゴリ移行待ち: '{definition.LegacyCategory ?? string.Empty}'。Inspector表示では自動変換しません。", MessageType.Warning);
            if (GUILayout.Button("カテゴリ移行支援を開く")) EventCategoryMigrationWindow.Open(explicitCatalog, definition);
        }
    }

    internal sealed class EventCategoryPickerWindow : OdinEditorWindow
    {
        private EventCategoryCatalogSO catalog;
        private EventDefinitionSO assignmentTarget;
        private Action<string> selected;
        private bool includeDeprecated;
        private string search = string.Empty;
        private Vector2 scroll;

        internal static void Open(EventCategoryCatalogSO catalog, string currentId, bool includeDeprecated,
            EventDefinitionSO assignmentTarget, Action<string> selected)
        {
            var window = GetWindow<EventCategoryPickerWindow>(true, "イベントカテゴリを選択", true);
            window.catalog = catalog;
            window.assignmentTarget = assignmentTarget;
            window.selected = selected;
            window.includeDeprecated = includeDeprecated;
            window.minSize = new Vector2(420, 360);
            window.Show();
        }

        protected override void OnEnable() { base.OnEnable(); Undo.undoRedoPerformed += OnUndoRedo; }
        protected override void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; base.OnDisable(); }
        private void OnUndoRedo() { catalog?.InvalidateCache(); Repaint(); }

        [Sirenix.OdinInspector.OnInspectorGUI]
        private void DrawPicker()
        {
            if (catalog == null) { EditorGUILayout.HelpBox("カテゴリカタログがありません。", MessageType.Error); return; }
            search = EditorGUILayout.TextField("検索", search);
            EditorGUILayout.LabelField(includeDeprecated ? "使用停止を含む候補" : "使用中の候補のみ", EditorStyles.miniLabel);
            var query = EventCategoryCatalogSO.NormalizeDisplayName(search);
            var categories = catalog.GetCategories(includeDeprecated).Where(c => string.IsNullOrEmpty(query) ||
                EventCategoryCatalogSO.NormalizeDisplayName(c.DisplayName).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                EventCategoryCatalogSO.NormalizeDisplayName(c.Description).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var category in categories)
            {
                using (new EditorGUI.DisabledScope(category.IsDeprecated && !includeDeprecated))
                    if (GUILayout.Button($"{category.DisplayName}  (順序 {category.SortOrder}){(category.IsDeprecated ? "  [使用停止]" : string.Empty)}"))
                    { selected?.Invoke(category.Id); EventCategoryEditorLifecycle.CloseAfterCurrentGui(this); }
            }
            if (categories.Length == 0) EditorGUILayout.HelpBox("一致するカテゴリがありません。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("未設定")) { selected?.Invoke(string.Empty); EventCategoryEditorLifecycle.CloseAfterCurrentGui(this); }
            if (GUILayout.Button("新規追加"))
                EventCategoryCreateWindow.Open(catalog, assignmentTarget, category =>
                { selected?.Invoke(category.Id); EventCategoryEditorLifecycle.CloseAfterCurrentGui(this); });
            if (GUILayout.Button("キャンセル")) EventCategoryEditorLifecycle.CloseAfterCurrentGui(this);
            EditorGUILayout.EndHorizontal();
        }
    }

    internal sealed class EventCategoryCreateWindow : OdinEditorWindow
    {
        private EventCategoryCatalogSO catalog;
        private EventDefinitionSO assignmentTarget;
        private Action<EventCategoryDefinition> createdCallback;
        private string displayName = string.Empty;
        private string description = string.Empty;
        private int sortOrder;
        private string error;

        internal static void Open(EventCategoryCatalogSO catalog, EventDefinitionSO assignmentTarget,
            Action<EventCategoryDefinition> callback)
        {
            var window = GetWindow<EventCategoryCreateWindow>(true, "イベントカテゴリを新規追加", true);
            window.catalog = catalog;
            window.assignmentTarget = assignmentTarget;
            window.createdCallback = callback;
            window.minSize = new Vector2(440, 270);
            window.Show();
        }

        [Sirenix.OdinInspector.OnInspectorGUI]
        private void DrawCreator()
        {
            EditorGUILayout.HelpBox("カテゴリは複数イベントで共有されます。名前は前後空白を除去し、Unicode FormKC・大文字小文字無視で重複検査します。", MessageType.Info);
            displayName = EditorGUILayout.TextField("名前", displayName);
            EditorGUILayout.LabelField("説明");
            description = EditorGUILayout.TextArea(description, GUILayout.MinHeight(60));
            sortOrder = EditorGUILayout.IntField("表示順", sortOrder);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("作成"))
            {
                if (EventCategoryEditorUtility.TryCreate(catalog, displayName, description, sortOrder, assignmentTarget, out var created, out error))
                { createdCallback?.Invoke(created); EventCategoryEditorLifecycle.CloseAfterCurrentGui(this); }
            }
            if (GUILayout.Button("キャンセル")) EventCategoryEditorLifecycle.CloseAfterCurrentGui(this);
            EditorGUILayout.EndHorizontal();
        }
    }

    internal sealed class EventCategoryManagerWindow : OdinEditorWindow
    {
        private EventCategoryCatalogSO catalog;
        private string selectedId;
        private string search = string.Empty;
        private bool includeDeprecated = true;
        private string editName;
        private string editDescription;
        private int editSortOrder;
        private bool editDeprecated;
        private string editError;
        private Vector2 categoryScroll;
        private Vector2 usageScroll;

        [MenuItem("Tools/Events/Event Categories")]
        private static void OpenFromSelection()
        {
            var selectedCatalog = Selection.activeObject as EventCategoryCatalogSO ?? (Selection.activeObject as EventCatalogSO)?.categoryCatalog;
            Open(selectedCatalog, null);
        }

        internal static void Open(EventCategoryCatalogSO catalog, string categoryId)
        {
            var window = GetWindow<EventCategoryManagerWindow>("Event Categories");
            window.catalog = catalog;
            window.Select(categoryId);
            window.minSize = new Vector2(620, 520);
            window.Show();
            window.Focus();
        }

        protected override void OnEnable() { base.OnEnable(); Undo.undoRedoPerformed += OnUndoRedo; }
        protected override void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; base.OnDisable(); }
        private void OnUndoRedo() { catalog?.InvalidateCache(); Select(selectedId); Repaint(); }

        [Sirenix.OdinInspector.OnInspectorGUI]
        private void DrawManager()
        {
            var nextCatalog = (EventCategoryCatalogSO)EditorGUILayout.ObjectField("カテゴリカタログ", catalog, typeof(EventCategoryCatalogSO), false);
            if (nextCatalog != catalog) { catalog = nextCatalog; Select(null); }
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("編集するカテゴリカタログを明示的に選択してください。検索で最初に見つかったアセットは自動採用しません。", MessageType.Info);
                return;
            }

            foreach (var problem in catalog.Initialize(false)) EditorGUILayout.HelpBox(problem, MessageType.Error);
            EditorGUILayout.BeginHorizontal();
            search = EditorGUILayout.TextField("検索", search);
            includeDeprecated = EditorGUILayout.ToggleLeft("使用停止を表示", includeDeprecated, GUILayout.Width(120));
            if (GUILayout.Button("新規追加", GUILayout.Width(90))) EventCategoryCreateWindow.Open(catalog, null, c => Select(c.Id));
            EditorGUILayout.EndHorizontal();

            var query = EventCategoryCatalogSO.NormalizeDisplayName(search);
            categoryScroll = EditorGUILayout.BeginScrollView(categoryScroll, GUILayout.Height(150));
            foreach (var category in catalog.GetCategories(includeDeprecated).Where(c => string.IsNullOrEmpty(query) ||
                EventCategoryCatalogSO.NormalizeDisplayName(c.DisplayName).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                if (GUILayout.Button($"{category.DisplayName}  [{category.Id}]{(category.IsDeprecated ? "  使用停止" : string.Empty)}",
                    selectedId == category.Id ? EditorStyles.toolbarButton : EditorStyles.miniButton)) Select(category.Id);
            EditorGUILayout.EndScrollView();

            if (!catalog.TryGetById(selectedId, out var selectedCategory)) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("カテゴリ定義", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ここでの変更は、このカテゴリを参照するすべてのイベントへ表示上反映されます。内部IDは変更しません。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("内部ID", selectedCategory.Id);
            editName = EditorGUILayout.TextField("名前", editName ?? selectedCategory.DisplayName);
            EditorGUILayout.LabelField("説明");
            editDescription = EditorGUILayout.TextArea(editDescription ?? selectedCategory.Description, GUILayout.MinHeight(55));
            editSortOrder = EditorGUILayout.IntField("表示順", editSortOrder);
            editDeprecated = EditorGUILayout.Toggle("使用停止", editDeprecated);
            if (!string.IsNullOrEmpty(editError)) EditorGUILayout.HelpBox(editError, MessageType.Error);
            if (GUILayout.Button("変更を保存"))
            {
                if (EventCategoryEditorUtility.TryUpdate(catalog, selectedCategory.Id, editName, editDescription, editSortOrder, editDeprecated, out editError))
                    Select(selectedCategory.Id);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("使用箇所", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("検索範囲: プロジェクト内の全 EventDefinitionSO アセット", EditorStyles.miniLabel);
            var usages = EventCategoryEditorUtility.FindAllDefinitions().Where(d => d.CategoryId == selectedCategory.Id)
                .OrderBy(d => d.eventCode, StringComparer.Ordinal).ToArray();
            usageScroll = EditorGUILayout.BeginScrollView(usageScroll, GUILayout.MaxHeight(130));
            foreach (var definition in usages)
                if (GUILayout.Button($"{definition.displayName}  ({definition.eventCode})")) EventDatabaseWindow.OpenAndSelect(definition);
            if (usages.Length == 0) EditorGUILayout.LabelField("使用イベントはありません。", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void Select(string id)
        {
            selectedId = id;
            editError = null;
            if (catalog != null && catalog.TryGetById(id, out var category))
            {
                editName = category.DisplayName;
                editDescription = category.Description;
                editSortOrder = category.SortOrder;
                editDeprecated = category.IsDeprecated;
            }
            else { editName = editDescription = null; editSortOrder = 0; editDeprecated = false; }
            Repaint();
        }
    }

    public static class EventCategoryCatalogAssetMenu
    {
        [MenuItem("Tools/Events/Create Category Catalog and Connect")]
        private static void CreateFromSelection() => CreateAndConnect(Selection.activeObject as EventCatalogSO);

        [MenuItem("Tools/Events/Create Category Catalog and Connect", true)]
        private static bool ValidateCreateFromSelection() => Selection.activeObject is EventCatalogSO;

        internal static EventCategoryCatalogSO CreateAndConnect(EventCatalogSO eventCatalog)
        {
            if (eventCatalog == null) { EditorUtility.DisplayDialog("Event Category Catalog", "接続先のEventCatalogSOを選択してください。", "OK"); return null; }
            if (eventCatalog.categoryCatalog != null) { EditorUtility.DisplayDialog("Event Category Catalog", "このEventCatalogSOには既にカテゴリカタログが設定されています。", "OK"); return eventCatalog.categoryCatalog; }
            var path = EditorUtility.SaveFilePanelInProject("Create Event Category Catalog", "EventCategoryCatalog", "asset", "空のカテゴリカタログを作成します。");
            if (string.IsNullOrEmpty(path)) return null;
            return CreateAndConnectAtPath(eventCatalog, path, true);
        }

        // Safe batch entry point: it refuses to choose when either side is ambiguous.
        public static void CreateAndConnectOnlyProjectCatalog()
        {
            var eventCatalogs = AssetDatabase.FindAssets("t:EventCatalogSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).ToArray();
            if (eventCatalogs.Length != 1)
                throw new InvalidOperationException($"Expected exactly one EventCatalogSO, found {eventCatalogs.Length}. Use the selection-based Editor menu instead.");
            var eventCatalog = eventCatalogs[0];
            if (eventCatalog.categoryCatalog != null)
            {
                Debug.Log($"Event category catalog is already connected: {AssetDatabase.GetAssetPath(eventCatalog.categoryCatalog)}");
                return;
            }

            var categoryCatalogs = AssetDatabase.FindAssets("t:EventCategoryCatalogSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<EventCategoryCatalogSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).ToArray();
            if (categoryCatalogs.Length > 1)
                throw new InvalidOperationException($"Found {categoryCatalogs.Length} EventCategoryCatalogSO assets. Assign the intended one explicitly.");
            if (categoryCatalogs.Length == 1)
            {
                Connect(eventCatalog, categoryCatalogs[0]);
                AssetDatabase.SaveAssets();
                Debug.Log($"Connected existing event category catalog: {AssetDatabase.GetAssetPath(categoryCatalogs[0])}");
                return;
            }

            var eventCatalogPath = AssetDatabase.GetAssetPath(eventCatalog);
            var directory = System.IO.Path.GetDirectoryName(eventCatalogPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Could not resolve the EventCatalogSO directory.");
            var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/EventCategoryCatalog.asset");
            var created = CreateAndConnectAtPath(eventCatalog, path, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created and connected empty event category catalog: {AssetDatabase.GetAssetPath(created)}", created);
        }

        private static EventCategoryCatalogSO CreateAndConnectAtPath(EventCatalogSO eventCatalog, string path, bool openManager)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create and Connect Event Category Catalog");
            var categoryCatalog = ScriptableObject.CreateInstance<EventCategoryCatalogSO>();
            AssetDatabase.CreateAsset(categoryCatalog, path);
            Undo.RegisterCreatedObjectUndo(categoryCatalog, "Create Event Category Catalog");
            Connect(eventCatalog, categoryCatalog);
            Undo.CollapseUndoOperations(group);
            if (openManager) EventCategoryEditorLifecycle.OpenManagerAfterCurrentGui(categoryCatalog);
            return categoryCatalog;
        }

        private static void Connect(EventCatalogSO eventCatalog, EventCategoryCatalogSO categoryCatalog)
        {
            Undo.RecordObject(eventCatalog, "Connect Event Category Catalog");
            eventCatalog.categoryCatalog = categoryCatalog;
            EventCategoryEditorLifecycle.SaveAfterCurrentGui(eventCatalog);
        }
    }

    internal class EventDefinitionInlineEditor : OdinEditor
    {
        internal EventCatalogSO CatalogOverride { get; set; }
        public override void OnInspectorGUI()
        {
            EventCategorySelectorGUI.Draw((EventDefinitionSO)target, CatalogOverride);
            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }

    [CustomEditor(typeof(EventDefinitionSO))]
    internal sealed class EventDefinitionSOEditor : EventDefinitionInlineEditor { }

    [CustomEditor(typeof(EventCategoryCatalogSO))]
    internal sealed class EventCategoryCatalogSOEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (EventCategoryCatalogSO)target;
            EditorGUILayout.LabelField($"カテゴリ数: {catalog.Categories.Count}");
            foreach (var problem in catalog.Initialize(false)) EditorGUILayout.HelpBox(problem, MessageType.Error);
            if (GUILayout.Button("カテゴリ管理を開く")) EventCategoryManagerWindow.Open(catalog, null);
        }
    }
}
