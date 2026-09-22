using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    internal enum EventCategoryMigrationMatch
    {
        AlreadyMigrated,
        PreserveCurrentSelection,
        EmptyLegacyValue,
        StrictExact,
        NormalizedOnly,
        Ambiguous,
        Missing
    }

    internal sealed class EventCategoryMigrationAssessment
    {
        internal EventCategoryMigrationMatch match;
        internal EventCategoryDefinition candidate;
        internal string reason;
    }

    internal static class EventCategoryMigration
    {
        internal static EventCategoryMigrationAssessment Assess(EventDefinitionSO definition, EventCategoryCatalogSO catalog)
        {
            if (definition.CategoryMigrationState == EventCategoryMigrationState.Migrated)
                return Result(EventCategoryMigrationMatch.AlreadyMigrated, null, "移行済みです。旧値は監査用に保持されています。");
            if (!string.IsNullOrEmpty(definition.CategoryId))
                return Result(EventCategoryMigrationMatch.PreserveCurrentSelection,
                    catalog != null && catalog.TryGetById(definition.CategoryId, out var selected) ? selected : null,
                    "新しいカテゴリIDが既に設定されています。旧値では上書きしません。");
            if (string.IsNullOrEmpty(definition.LegacyCategory))
                return Result(EventCategoryMigrationMatch.EmptyLegacyValue, null, "旧値は空欄です。新側も未設定として確定できます。");
            if (catalog == null) return Result(EventCategoryMigrationMatch.Missing, null, "カテゴリカタログがありません。");

            var usableCategories = catalog.GetCategories(true).ToArray();
            var strict = usableCategories.Where(c => string.Equals(c.DisplayName, definition.LegacyCategory, StringComparison.Ordinal)).ToArray();
            if (strict.Length == 1 && catalog.TryGetById(strict[0].Id, out var exact) && exact == strict[0])
                return Result(EventCategoryMigrationMatch.StrictExact, exact, "旧値と表示名が大文字小文字を含めて厳密に一致し、候補は1件です。");
            if (strict.Length > 1) return Result(EventCategoryMigrationMatch.Ambiguous, null, "厳密一致する表示名が複数あります。自動選択しません。");

            var normalizedKey = EventCategoryCatalogSO.NormalizeDisplayName(definition.LegacyCategory);
            var normalized = usableCategories.Where(c => string.Equals(
                EventCategoryCatalogSO.NormalizeDisplayName(c.DisplayName), normalizedKey, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (normalized.Length == 1)
                return Result(EventCategoryMigrationMatch.NormalizedOnly, normalized[0],
                    $"大文字小文字またはUnicode正規化後のみ '{normalized[0].DisplayName}' と一致します。手動確認が必要です。");
            if (normalized.Length > 1) return Result(EventCategoryMigrationMatch.Ambiguous, null, "正規化後に複数候補があります。自動選択しません。");
            return Result(EventCategoryMigrationMatch.Missing, null, "一致するカテゴリがありません。既存カテゴリを手動選択するか、新規追加してください。");
        }

        private static EventCategoryMigrationAssessment Result(EventCategoryMigrationMatch match,
            EventCategoryDefinition candidate, string reason)
            => new EventCategoryMigrationAssessment { match = match, candidate = candidate, reason = reason };
    }

    internal sealed class EventCategoryMigrationWindow : OdinEditorWindow
    {
        private EventCatalogSO eventCatalog;
        private EventDefinitionSO focus;
        private bool showMigrated;
        private Vector2 scroll;
        private readonly Dictionary<int, string> stagedSelections = new Dictionary<int, string>();
        private readonly Dictionary<int, string> rowErrors = new Dictionary<int, string>();

        [MenuItem("Tools/Events/Category Migration")]
        private static void OpenFromSelection()
        {
            var selectedCatalog = Selection.activeObject as EventCatalogSO;
            var selectedDefinition = Selection.activeObject as EventDefinitionSO;
            Open(selectedCatalog, selectedDefinition);
        }

        internal static void Open(EventCatalogSO explicitCatalog, EventDefinitionSO focus)
        {
            var window = GetWindow<EventCategoryMigrationWindow>("Category Migration");
            window.eventCatalog = explicitCatalog ?? ResolveSingleOwner(focus);
            window.focus = focus;
            window.minSize = new Vector2(760, 520);
            window.Show();
            window.Focus();
        }

        private static EventCatalogSO ResolveSingleOwner(EventDefinitionSO definition)
        {
            if (definition == null) return null;
            var owners = AssetDatabase.FindAssets("t:EventCatalogSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.definitions != null && c.definitions.Contains(definition)).ToArray();
            return owners.Length == 1 ? owners[0] : null;
        }

        protected override void OnEnable() { base.OnEnable(); Undo.undoRedoPerformed += OnUndoRedo; }
        protected override void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; base.OnDisable(); }
        private void OnUndoRedo()
        {
            eventCatalog?.categoryCatalog?.InvalidateCache();
            stagedSelections.Clear();
            rowErrors.Clear();
            Repaint();
        }

        [Sirenix.OdinInspector.OnInspectorGUI]
        private void DrawMigration()
        {
            var nextCatalog = (EventCatalogSO)EditorGUILayout.ObjectField("Event Catalog", eventCatalog, typeof(EventCatalogSO), false);
            if (nextCatalog != eventCatalog) { eventCatalog = nextCatalog; stagedSelections.Clear(); rowErrors.Clear(); }
            if (eventCatalog == null)
            {
                EditorGUILayout.HelpBox("移行先カテゴリカタログを決めるため、EventCatalogSOを明示的に選択してください。", MessageType.Warning);
                return;
            }
            if (eventCatalog.categoryCatalog == null)
            {
                EditorGUILayout.HelpBox("選択したEventCatalogSOにカテゴリカタログがありません。先に作成・接続してください。", MessageType.Error);
                if (GUILayout.Button("空のカテゴリカタログを作成・接続")) EventCategoryCatalogAssetMenu.CreateAndConnect(eventCatalog);
                return;
            }

            var categoryCatalog = eventCatalog.categoryCatalog;
            var definitions = (eventCatalog.definitions ?? new List<EventDefinitionSO>()).Where(d => d != null).Distinct()
                .OrderByDescending(d => d == focus).ThenBy(d => d.eventCode, StringComparer.Ordinal).ToArray();
            var pending = definitions.Where(d => d.CategoryMigrationState == EventCategoryMigrationState.Pending).ToArray();
            EditorGUILayout.LabelField($"検索範囲: 選択中の EventCatalogSO ({eventCatalog.name}) に登録されたイベント", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"全イベント {definitions.Length}件 / 移行待ち {pending.Length}件 / 移行済み {definitions.Length - pending.Length}件");
            var groups = pending.GroupBy(d => d.LegacyCategory ?? string.Empty).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"'{g.Key}'={g.Count()}件");
            EditorGUILayout.HelpBox("旧値集計: " + (groups.Any() ? string.Join("、", groups) : "移行待ちはありません"), MessageType.Info);
            EditorGUILayout.HelpBox("表示だけではアセットを変更しません。各行の「この対応を適用」を押した場合だけ、既存イベントのカテゴリIDと移行状態を更新します。", MessageType.Info);
            showMigrated = EditorGUILayout.ToggleLeft("移行済みも表示", showMigrated);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var definition in definitions.Where(d => showMigrated || d.CategoryMigrationState == EventCategoryMigrationState.Pending))
                DrawRow(definition, categoryCatalog);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(EventDefinitionSO definition, EventCategoryCatalogSO catalog)
        {
            int key = definition.GetInstanceID();
            var assessment = EventCategoryMigration.Assess(definition, catalog);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{definition.displayName}  ({definition.eventCode})", EditorStyles.boldLabel);
            if (GUILayout.Button("開く", GUILayout.Width(55))) EventDatabaseWindow.OpenAndSelect(definition);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("旧文字列", string.IsNullOrEmpty(definition.LegacyCategory) ? "<空欄>" : definition.LegacyCategory);
            EditorGUILayout.LabelField("現在の新ID", string.IsNullOrEmpty(definition.CategoryId) ? "<未設定>" : definition.CategoryId);
            EditorGUILayout.LabelField("判定", assessment.reason, EditorStyles.wordWrappedLabel);

            if (assessment.match != EventCategoryMigrationMatch.AlreadyMigrated)
            {
                string chosen = stagedSelections.TryGetValue(key, out var staged) ? staged :
                    assessment.match == EventCategoryMigrationMatch.StrictExact || assessment.match == EventCategoryMigrationMatch.PreserveCurrentSelection
                        ? assessment.candidate?.Id ?? definition.CategoryId : string.Empty;
                string chosenLabel = string.IsNullOrEmpty(chosen) ? "未設定" : catalog.TryGetById(chosen, out var selected) ? selected.DisplayName : "未登録ID";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("適用候補", chosenLabel);
                if (GUILayout.Button("手動選択", GUILayout.Width(90)))
                    EventCategoryPickerWindow.Open(catalog, chosen, true, null, id => { stagedSelections[key] = id; rowErrors.Remove(key); Repaint(); });
                if (GUILayout.Button("新規追加", GUILayout.Width(80)))
                    EventCategoryCreateWindow.Open(catalog, null, created => { stagedSelections[key] = created.Id; rowErrors.Remove(key); Repaint(); });
                EditorGUILayout.EndHorizontal();
                if (assessment.match == EventCategoryMigrationMatch.NormalizedOnly && !stagedSelections.ContainsKey(key))
                    EditorGUILayout.HelpBox("正規化後の候補は表示上の参考です。手動選択するまで適用対象にしません。", MessageType.Warning);
                if (rowErrors.TryGetValue(key, out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (GUILayout.Button("この対応を適用"))
                {
                    if (EventCategoryEditorUtility.TryApplyMigration(definition, catalog, chosen, out error))
                    { stagedSelections.Remove(key); rowErrors.Remove(key); }
                    else rowErrors[key] = error;
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
