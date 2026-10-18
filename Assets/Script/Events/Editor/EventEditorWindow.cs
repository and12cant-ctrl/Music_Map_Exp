using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Editor
{
    /// <summary>Edits the original assets. No proxy copies or implicit lifecycle conversion.</summary>
    public sealed class EventEditorWindow : EditorWindow
    {
        internal static readonly string[] Tabs = { "基本", "条件", "Slot", "時間", "HintLevel", "Outcome", "関係図", "検証" };
        [SerializeField] private EventCatalogSO catalog;
        [SerializeField] private EventDefinitionSO selected;
        [SerializeField] private int tabIndex;
        [SerializeField] private int outcomeIndex;
        private SerializedObject serialized;
        private VisualElement detail, list, footer, headingArea;
        private string search = "", categoryFilter = "";
        private EventDefinitionSO[] definitions = Array.Empty<EventDefinitionSO>();
        private bool rebuildQueued;
        private readonly List<Action> summaryUpdates = new List<Action>();

        [MenuItem("Tools/Events/Event Editor Ver.1")]
        public static void Open() => GetWindow<EventEditorWindow>("イベント編集 Ver.1").Show();
        private void OnEnable() { Undo.undoRedoPerformed += Refresh; EditorApplication.projectChanged += Refresh; }
        private void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.projectChanged -= Refresh; serialized?.Dispose(); }
        public void CreateGUI()
        {
            minSize = new Vector2(650, 420);
            rootVisualElement.Clear();
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Script/Events/Editor/EventEditorWindow.uss");
            if (stylesheet != null && !rootVisualElement.styleSheets.Contains(stylesheet)) rootVisualElement.styleSheets.Add(stylesheet);
            rootVisualElement.AddToClassList("event-editor");
            var toolbar = new Toolbar();
            toolbar.AddToClassList("event-toolbar");
            toolbar.Add(new Label("登録先カタログ") { name = "event-catalog-label" });
            // Separate the caption from BaseField's inspector label sizing. The object input
            // owns only the remaining space; long asset names cannot displace the actions.
            var cat = new ObjectField { name = "event-catalog", objectType = typeof(EventCatalogSO), allowSceneObjects = false, value = catalog };
            cat.RegisterValueChangedCallback(e => { catalog = e.newValue as EventCatalogSO; categoryFilter = ""; CreateGUI(); });
            toolbar.Add(cat);
            toolbar.Add(new ToolbarButton(Save) { text = "保存" });
            toolbar.Add(new ToolbarButton(() => { if (selected != null) Selection.activeObject = selected; }) { text = "Inspector" });
            rootVisualElement.Add(toolbar);
            var split = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("event-split");
            split.style.flexGrow = 1;
            var left = new VisualElement(); left.style.minWidth = 180;
            var query = new ToolbarSearchField { value = search };
            query.RegisterValueChangedCallback(e => { search = e.newValue; DrawList(); }); left.Add(query);
            var categories = new List<string> { "すべて", "未設定" };
            var ids = new List<string> { "", "<none>" };
            foreach (var c in catalog?.categoryCatalog?.Categories ?? Array.Empty<EventCategoryDefinition>())
                if (c != null) { categories.Add(c.DisplayName); ids.Add(c.Id); }
            var filter = new PopupField<string>("カテゴリ", categories, Math.Max(0, ids.IndexOf(categoryFilter)));
            filter.RegisterValueChangedCallback(_ => { categoryFilter = ids[filter.index]; DrawList(); }); left.Add(filter);
            var actions = Row();
            Button(actions, "新規", () => Create(false)); Button(actions, "複製", () => Create(true)); Button(actions, "削除", Delete);
            left.Add(actions);
            Button(left, "選択イベントをカタログへ登録", RegisterSelected);
            list = new ScrollView(); list.style.flexGrow = 1; left.Add(list); split.Add(left);
            var right = new VisualElement(); right.style.minWidth = 360; right.style.flexGrow = 1;
            right.AddToClassList("event-right");
            headingArea = new VisualElement(); right.Add(headingArea);
            var tabs = Row(); tabs.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < Tabs.Length; i++) { int index = i; Button(tabs, Tabs[i], () => SelectTab(index)); }
            right.Add(tabs);
            // Vertical mode constrains the content to this pane's viewport instead of
            // measuring text in an unbounded horizontal scrolling area.
            var detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.AddToClassList("event-detail");
            detailScroll.contentContainer.AddToClassList("event-detail-content");
            detail = detailScroll; right.Add(detail);
            split.Add(right); rootVisualElement.Add(split);
            footer = new VisualElement(); rootVisualElement.Add(footer);
            Refresh();
        }
        private void Refresh()
        {
            if (detail == null) return;
            definitions = AssetDatabase.FindAssets("t:EventDefinitionSO").Select(g => AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).OrderBy(d => d.sortOrder).ThenBy(d => d.eventCode).ToArray();
            DrawList(); DrawDetail();
        }
        private void Later()
        {
            if (rebuildQueued) return;
            rebuildQueued = true;
            rootVisualElement.schedule.Execute(() => { rebuildQueued = false; if (this != null) Refresh(); });
        }
        private void DrawList()
        {
            list?.Clear(); if (list == null) return;
            foreach (var d in definitions.Where(d => string.IsNullOrEmpty(search) || ((d.displayName ?? "") + " " + d.eventCode + " " + string.Join(" ", d.tags ?? new List<string>())).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(d => categoryFilter == "" || (categoryFilter == "<none>" ? string.IsNullOrEmpty(d.CategoryId) : d.CategoryId == categoryFilter)))
            {
                var button = new Button(() => Select(d)) { text = (d == selected ? "▶ " : "") + d.displayName + "\n" + d.eventCode + (catalog?.definitions?.Contains(d) == true ? "" : " [未登録]") };
                button.style.whiteSpace = WhiteSpace.Normal; button.style.minHeight = 42;
                if (d == selected) button.style.backgroundColor = new Color(.18f, .34f, .48f);
                list.Add(button);
            }
        }
        internal void Select(EventDefinitionSO definition) { selected = definition; Refresh(); }
        internal void SelectTab(int index) { tabIndex = Mathf.Clamp(index, 0, 7); DrawDetail(); }
        private void DrawDetail()
        {
            detail.Unbind(); detail.Clear(); headingArea.Clear(); summaryUpdates.Clear(); serialized?.Dispose(); serialized = null;
            if (selected == null) { Note(detail, "一覧からイベントを選択してください。新規作成には登録先カタログが必要です。"); DrawFooter(); return; }
            serialized = new SerializedObject(selected);
            var heading = new Label(selected.displayName + "  /  " + selected.eventCode + "  — " + Tabs[tabIndex]);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold; heading.style.whiteSpace = WhiteSpace.Normal; heading.style.marginTop = 8; heading.style.marginBottom = 8; headingArea.Add(heading);
            if (!selected.useExtendedLifecycle) Note(detail, "従来方式のイベントです。設定の自動変換は行いません。Odinの旧画面も引き続き利用できます。");
            if (tabIndex < 6 && (selected.appearance == null || selected.reservation == null || selected.execution?.judge == null || selected.useExtendedLifecycle && selected.extension == null))
            { Note(detail, "必須データ構造が欠落しています。検証タブとInspectorで確認してください。自動修復は行いません。", HelpBoxMessageType.Error); DrawFooter(); return; }
            switch (tabIndex)
            {
                case 0: Basic(); break;
                case 1: Conditions(); break;
                case 2: Slots(); break;
                case 3: Time(); break;
                case 4: Hints(); break;
                case 5: Outcomes(); break;
                case 6: Graph(); break;
                case 7: Validation(); break;
            }
            DrawFooter();
        }
        private void DrawFooter()
        {
            footer.Clear();
            var issues = EventValidator.Validate(definitions, catalog);
            var label = new Label($"検証: エラー {issues.Count(i => i.severity == EventIssueSeverity.Error)} / 警告 {issues.Count(i => i.severity == EventIssueSeverity.Warning)}　|　" + (selected != null && EditorUtility.IsDirty(selected) ? "変更あり（保存してください）" : "選択アセットに未保存変更なし"));
            label.style.whiteSpace = WhiteSpace.Normal; footer.Add(label);
        }
        private static VisualElement Row() { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private static void Button(VisualElement parent, string text, Action click) => parent.Add(new Button(click) { text = text });
        private static void Note(VisualElement parent, string text, HelpBoxMessageType type = HelpBoxMessageType.Info) => parent.Add(new HelpBox(text, type));
        private static Foldout Group(VisualElement parent, string text, bool open = true) { var f = new Foldout { text = text, value = open }; f.AddToClassList("event-group"); parent.Add(f); return f; }
        private SerializedProperty P(string path) => serialized.FindProperty(path);
        private void Changed() { serialized.ApplyModifiedProperties(); foreach (var update in summaryUpdates) update(); DrawFooter(); }
        private void Field(VisualElement parent, string path, string label, bool rebuild = false)
        {
            var p = P(path); if (p == null) { Note(parent, "設定項目がありません: " + label, HelpBoxMessageType.Warning); return; }
            string lastValue = Stamp(p);
            var field = new PropertyField(p, label); field.BindProperty(p);
            field.RegisterValueChangeCallback(_ =>
            {
                if (serialized == null || selected == null) return;
                string value = Stamp(P(path));
                // Initial binding also emits events. Rebuild only when the controlling value changed.
                if (value == lastValue) return;
                lastValue = value; foreach (var update in summaryUpdates) update(); DrawFooter(); if (rebuild) Later();
            }); parent.Add(field);
        }
        private static string Stamp(SerializedProperty p) => p == null ? "" : p.propertyType == SerializedPropertyType.Boolean ? p.boolValue.ToString() :
            p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer ? p.intValue.ToString() :
            p.propertyType == SerializedPropertyType.String ? p.stringValue : p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceInstanceIDValue.ToString() :
            p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString() : p.isArray ? p.arraySize.ToString() : p.type;
        private void Choice(VisualElement parent, string path, string label, string[] names, int[] values = null, bool rebuild = false)
        {
            var p = P(path); values = values ?? Enumerable.Range(0, names.Length).ToArray();
            var labels = names.ToList(); var numbers = values.ToList();
            int index = numbers.IndexOf(p.intValue);
            if (index < 0) { numbers.Add(p.intValue); labels.Add("不正な保存値: " + p.intValue); index = labels.Count - 1; }
            var field = new PopupField<string>(label, labels, index); parent.Add(field);
            field.RegisterValueChangedCallback(_ => { serialized.Update(); P(path).intValue = numbers[field.index]; Changed(); if (rebuild) Later(); });
        }
        private void AssetChoice<T>(VisualElement parent, string path, string label) where T : Object
        {
            var assets = (typeof(T) == typeof(EventDefinitionSO) ? (catalog?.definitions ?? new List<EventDefinitionSO>()).Cast<T>() : AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))))
                .Where(a => a != null).Distinct().ToList();
            var current = P(path).objectReferenceValue as T;
            if (current != null && !assets.Contains(current)) assets.Add(current);
            assets.Insert(0, null);
            var names = assets.Select(a => a == null ? "未設定" : a is EventDefinitionSO d ? d.displayName + " / " + d.eventCode + " [" + AssetDatabase.GetAssetPath(a) + "]" : a.name + " [" + AssetDatabase.GetAssetPath(a) + "]").ToList();
            var field = new PopupField<string>(label, names, Math.Max(0, assets.IndexOf(current))); parent.Add(field);
            field.RegisterValueChangedCallback(_ => { serialized.Update(); P(path).objectReferenceValue = assets[field.index]; Changed(); Later(); });
        }
        private void Basic()
        {
            Note(detail, "必須: イベント名・作者用ID・カテゴリ。内部EventIdは保存データの参照に使うため変更しません。");
            Field(detail, "displayName", "イベント名（必須）"); Field(detail, "eventCode", "作者用ID（必須・重複不可）");
            var id = new TextField("内部EventId") { value = selected.PersistentId, isReadOnly = true }; detail.Add(id);
            Field(detail, "enabled", "ゲームで有効");
            var cats = catalog?.categoryCatalog?.Categories.Where(c => c != null && !c.IsDeprecated).ToList() ?? new List<EventCategoryDefinition>();
            var current = catalog?.categoryCatalog?.Categories.FirstOrDefault(c => c != null && c.Id == selected.CategoryId);
            if (current != null && !cats.Contains(current)) cats.Add(current);
            var ids = new List<string> { "" }; ids.AddRange(cats.Select(c => c.Id));
            var labels = new List<string> { "未設定" }; labels.AddRange(cats.Select(c => c.DisplayName));
            if (!ids.Contains(selected.CategoryId ?? "")) { ids.Add(selected.CategoryId); labels.Add("未登録: " + selected.CategoryId); }
            var category = new PopupField<string>("カテゴリ（必須）", labels, Math.Max(0, ids.IndexOf(selected.CategoryId ?? ""))); detail.Add(category);
            category.RegisterValueChangedCallback(_ => { if (EventCategoryEditorUtility.TryAssign(selected, catalog?.categoryCatalog, ids[category.index], "カテゴリ変更", out var error)) Later(); else Note(detail, error, HelpBoxMessageType.Error); });
            Button(detail, "カテゴリ管理", () => { if (catalog?.categoryCatalog != null) Selection.activeObject = catalog.categoryCatalog; EditorApplication.ExecuteMenuItem("Tools/Events/Event Categories"); });
            Field(detail, "tags", "検索タグ"); Field(detail, "developerMemo", "制作者メモ");
            var presentation = Group(detail, "文章・画像");
            Presentation(presentation, "reservation.reservationPresentation", "開始前"); Presentation(presentation, "execution.beforeJudge", "判定前");
            Field(presentation, "appearance.mapIcon", "マップアイコン"); Field(presentation, "appearance.mapColorKey", "マップ色キー");
            var placement = Group(detail, "マップへの配置", false);
            Field(placement, "appearance.generateOnMap", "マップ生成時に配置"); Field(placement, "appearance.requiresUnlock", "解放が必要");
            Field(placement, "appearance.requiredPlacements", "最低配置数"); Field(placement, "appearance.weight", "抽選の重み"); Backgrounds(placement);
            Field(detail, "sortOrder", "一覧表示順"); Field(detail, "priority", "実行優先度");
            var advanced = Group(detail, "実行方式（既存アセットは慎重に変更）", false);
            Note(advanced, "方式切替はデータ移行ではありません。従来結果と拡張結果は別の設定です。");
            Field(advanced, "useExtendedLifecycle", "拡張ライフサイクルを使用", true);
            Field(advanced, "execution.executionType", "実行画面の種類"); Field(advanced, "execution.externalKey", "外部画面の接続キー");
        }
        private void Presentation(VisualElement parent, string path, string name)
        { var group = Group(parent, name); Field(group, path + ".text", "文章"); Field(group, path + ".image", "画像・背景"); }
        private void Backgrounds(VisualElement parent)
        {
            var candidates = AssetDatabase.FindAssets("t:BGCategoryListSO").Select(g => AssetDatabase.LoadAssetAtPath<BGCategoryListSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b != null).SelectMany(b => b.bGCategoryList ?? new List<BackgroundCategory>()).Where(b => b != null).Select(b => b.bgCategoryName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            List(parent, "appearance.backgroundCategories", "配置できる背景カテゴリ（空なら制限なし）", p => p.stringValue, (box, p) =>
            {
                var names = new List<string> { "" }; names.AddRange(candidates); if (!names.Contains(p.stringValue)) names.Add(p.stringValue);
                string path = p.propertyPath;
                var field = new PopupField<string>("背景カテゴリ", names, Math.Max(0, names.IndexOf(p.stringValue))); box.Add(field);
                field.RegisterValueChangedCallback(_ => { P(path).stringValue = field.value; Changed(); Later(); });
            });
        }
        private void Conditions()
        {
            Note(detail, "すべての条件を満たす必要があります（AND）。時間帯条件は発生可否の検査であり、昼夜切替時の自動生成トリガーではありません。");
            if (selected.useExtendedLifecycle) Queries("extension.conditions", false);
            Note(detail, "所持金は既存の配置条件で設定できます。背景は基本タブの配置カテゴリで制限します。拡張PointConditionへの所持金／背景比較は未実装です。");
            LegacyConditions(detail, "appearance.conditions", "配置条件（既存のWorld接続を使用）");
        }
        private void Queries(string path, bool points)
        {
            List(detail, path, points ? "ポイント条件" : "発生条件", p => QuerySummary(p.FindPropertyRelative("query")) + (points ? " → " + p.FindPropertyRelative("addPoint").intValue + "ポイント" : ""), (box, p) =>
            { Query(box, p.propertyPath + ".query"); if (points) Field(box, p.propertyPath + ".addPoint", "成立時の獲得ポイント"); });
        }
        private static readonly string[] Sources = { "キャラクター能力", "施設の関係値", "Flag", "ヒント所持", "時間帯", "イベント履歴の結果", "過去参加者の能力" };
        private static readonly string[] Comparisons = { "＝", "≠", "≧", "＞", "≦", "＜" };
        internal static void ResetQuery(SerializedProperty q, ConditionSource source)
        {
            foreach (string name in new[] { "character", "facility", "flag", "hint", "eventDefinition" }) q.FindPropertyRelative(name).objectReferenceValue = null;
            q.FindPropertyRelative("characterInstanceId").stringValue = "";
            foreach (string name in new[] { "historyMode", "comparison", "value", "phase", "outcome" }) q.FindPropertyRelative(name).intValue = 0;
            q.FindPropertyRelative("parameter").intValue = (int)EventParameter.Appeal;
            q.FindPropertyRelative("boolValue").boolValue = true; q.FindPropertyRelative("source").intValue = (int)source;
        }
        private void Query(VisualElement box, string path)
        {
            var q = P(path); int source = q.FindPropertyRelative("source").intValue;
            var names = Sources.ToList(); if (source < 0 || source >= names.Count) { names.Add("不正な判定対象"); source = names.Count - 1; }
            var field = new PopupField<string>("判定対象", names, source); box.Add(field);
            field.RegisterValueChangedCallback(_ => { if (field.index < Sources.Length) { ResetQuery(P(path), (ConditionSource)field.index); Changed(); Later(); } });
            bool numeric = source == 0 || source == 1 || source == 6;
            if (source == 0) { AssetChoice<CharaData>(box, path + ".character", "対象キャラクター"); Field(box, path + ".parameter", "能力値");
                var instance = Group(box, "特定の人物インスタンス（上級設定）", false); Field(instance, path + ".characterInstanceId", "保存済みruntime ID（空なら同種の誰か）"); }
            if (source == 1) AssetChoice<FacilityDefinitionSO>(box, path + ".facility", "対象施設");
            if (source == 2) AssetChoice<FlagDefinitionSO>(box, path + ".flag", "対象Flag");
            if (source == 3) AssetChoice<HintData>(box, path + ".hint", "対象ヒント");
            if (source == 5 || source == 6)
            {
                AssetChoice<EventDefinitionSO>(box, path + ".eventDefinition", "参照イベント");
                Choice(box, path + ".historyMode", "参照する履歴", new[] { "最新（Latest）", "いずれか（Any）", "すべて（All・0件は不成立）" });
                if (source == 6) Field(box, path + ".parameter", "参加者の能力値");
            }
            Choice(box, path + ".comparison", "演算子", numeric ? Comparisons : Comparisons.Take(2).ToArray());
            if (numeric) Field(box, path + ".value", "比較値");
            else if (source == 2 || source == 3) Field(box, path + ".boolValue", "比較値（True / False）");
            else if (source == 4) Choice(box, path + ".phase", "時間帯", new[] { "昼", "夜" });
            else if (source == 5) Choice(box, path + ".outcome", "結果", new[] { "SUCCESS", "FAILURE", "EXPIRED" });
        }
        private static string QuerySummary(SerializedProperty q)
        {
            if (q == null) return "未設定の条件";
            int s = q.FindPropertyRelative("source").intValue, c = q.FindPropertyRelative("comparison").intValue;
            string value = s == 0 || s == 1 || s == 6 ? q.FindPropertyRelative("value").intValue.ToString() : s == 4 ? (q.FindPropertyRelative("phase").intValue == 0 ? "昼" : "夜") : s == 5 ? ((OutcomeType)q.FindPropertyRelative("outcome").intValue).ToString() : q.FindPropertyRelative("boolValue").boolValue.ToString();
            string target = "";
            foreach (var name in new[] { "character", "facility", "flag", "hint", "eventDefinition" }) if (q.FindPropertyRelative(name).objectReferenceValue != null) target = q.FindPropertyRelative(name).objectReferenceValue.name;
            if (s == 5 || s == 6) target += " / " + (HistoryMode)q.FindPropertyRelative("historyMode").intValue;
            return (s >= 0 && s < Sources.Length ? Sources[s] : "不正な対象") + " " + target + " " + (c >= 0 && c < Comparisons.Length ? Comparisons[c] : "?") + " " + value;
        }
        private void Slots()
        {
            Note(detail, "Slot番号は上から1、2…です。保存済み履歴・他イベントが参照するSlotの並べ替えや削除は番号の意味を変えます。過去参加者は同一runtime IDを必須とし、代替しません。");
            List(detail, "reservation.cardSlots", "参加枠", p => p.FindPropertyRelative("label").stringValue + " / " + (SlotType)p.FindPropertyRelative("slotType").intValue, (box, p) =>
            {
                string path = p.propertyPath;
                Field(box, path + ".label", "枠の名前"); Field(box, path + ".acceptedType", "カードの種類"); Field(box, path + ".amount", "必要数");
                if (!selected.useExtendedLifecycle) { Field(box, path + ".required", "必須"); return; }
                Choice(box, path + ".slotType", "参加方式", new[] { "必須", "任意", "選択（既存仕様では任意枠）", "固定の関係者" }, rebuild: true);
                if (p.FindPropertyRelative("slotType").intValue == (int)SlotType.Involved) Field(box, path + ".involvedTarget", "固定の対象");
                string r = path + ".historyParticipant";
                Field(box, r + ".enabled", "過去イベントの同一参加者を要求", true);
                if (!P(r + ".enabled").boolValue) return;
                AssetChoice<EventDefinitionSO>(box, r + ".definition", "参照イベント");
                Field(box, r + ".slotNumber", "参照Slot番号（1から）");
                Choice(box, r + ".source", "履歴の選択方式", new[] { "FollowUpの発生元インスタンス", "条件に一致する最新の解決履歴" });
                Field(box, r + ".filterOutcome", "結果で履歴を絞り込む", true);
                if (P(r + ".filterOutcome").boolValue) Choice(box, r + ".outcome", "必要な結果", new[] { "SUCCESS", "FAILURE", "EXPIRED" });
                Note(box, "要求方式: 同じ人物を必須とします。履歴なし・Slot未記録・人物利用不可は未解決。開始画面に理由を表示します。");
            });
            List(detail, "reservation.requirements", "参加カードの合計能力条件", p => (EventParameter)p.FindPropertyRelative("parameter").intValue + " ≧ " + p.FindPropertyRelative("minimum").intValue, (box, p) => { Field(box, p.propertyPath + ".parameter", "能力値"); Field(box, p.propertyPath + ".minimum", "最低値"); });
        }
        private void Time()
        {
            if (selected.useExtendedLifecycle)
            {
                Field(detail, "extension.durationPhaseCount", "開始後、実行までの昼夜切替回数", true);
                Field(detail, "extension.deadlinePhaseCount", "未開始イベントの有効期限（昼夜切替回数）", true);
                Note(detail, $"Start後、昼夜切替を{selected.extension.durationPhaseCount}回経過すると実行できます。未開始なら出現から{selected.extension.deadlinePhaseCount}回でEXPIRED。期限0はStart前に即時期限切れ。開始後は期限の対象外です。");
            }
            else { Field(detail, "reservation.triggerMode", "実行のタイミング"); Field(detail, "reservation.requiredPhaseCount", "予約後の昼夜切替回数"); }
            Note(detail, "後続イベントの出現待ち時間・重複許可は各FollowUpに保存されます。Outcomeタブで設定してください。時間帯（昼／夜）の指定は条件タブです。");
            Button(detail, "OutcomeのFollowUp設定へ", () => SelectTab(5));
        }
        private void Hints()
        {
            if (!selected.useExtendedLifecycle) { Note(detail, "従来方式にPointConditionはありません。ヒント付与はOutcomeで設定します。"); return; }
            Note(detail, "条件は解決時に評価して加点します。発生条件とは独立しています。実際の付与にはOutcomeのHint Effectも必要です。");
            Queries("extension.pointConditions", true);
            Field(detail, "extension.level2Points", "レベル2に必要な合計点", true); Field(detail, "extension.level3Points", "レベル3に必要な合計点", true);
            Note(detail, $"合計点が{selected.extension.level3Points}以上 → Lv3、それ未満で{selected.extension.level2Points}以上 → Lv2、その他 → Lv1。");
            AssetChoice<HintPoolSO>(detail, "extension.randomHintPool", "ランダム取得の候補プール");
            Note(detail, "取得方式はOutcomeで固定／ランダムを選択。未取得候補を抽選し一巡で履歴リセット。所有レベルは高い値のみ反映します。HintDataにはMove / Density / Order / Rhythm / MaxEmo / MinEmoの分類・取得内容が未定義のため、その編集は保留です。", HelpBoxMessageType.Warning);
            Button(detail, "ヒントを付与するOutcomeへ", () => SelectTab(5));
        }
        private void Outcomes()
        {
            var judge = Group(detail, "成功・失敗の判定");
            Note(judge, "参加カードの指定能力値合計 ＋ 補正 ＋ ダイス合計 ≧ 目標値でSUCCESS。実行画面の続行で確定します。");
            Field(judge, "execution.judge.parameter", "使用する能力値"); Field(judge, "execution.judge.modifier", "合計への補正"); Field(judge, "execution.judge.target", "成功に必要な目標値");
            Field(judge, "execution.judge.useDice", "ダイスを使用", true);
            var dice = Group(judge, "ダイス設定（使用しない場合も有効範囲が必要）", selected.execution.judge.useDice);
            Field(dice, "execution.judge.diceCount", "ダイスの個数（1〜100）"); Field(dice, "execution.judge.diceSides", "ダイスの面数（2〜10000）");
            if (selected.useExtendedLifecycle)
            {
                var tabs = Row(); for (int i = 0; i < 3; i++) { int n = i; Button(tabs, ((OutcomeType)i).ToString(), () => { outcomeIndex = n; DrawDetail(); }); } detail.Add(tabs);
                string path = "extension." + new[] { "success", "failure", "expired" }[Mathf.Clamp(outcomeIndex, 0, 2)];
                Note(detail, "編集中: " + ((OutcomeType)outcomeIndex)); Outcome(detail, path);
            }
            else List(detail, "outcomes", "従来方式の結果", p => p.FindPropertyRelative("outcomeCode").stringValue, (box, p) =>
            {
                Field(box, p.propertyPath + ".outcomeCode", "結果コード"); Field(box, p.propertyPath + ".isDefault", "条件不成立時の既定結果");
                LegacyConditions(box, p.propertyPath + ".conditions", "結果の条件"); Outcome(box, p.propertyPath);
            });
        }
        private void Outcome(VisualElement box, string path)
        {
            Presentation(box, path + ".presentation", "結果の表示（EXPIREDは現行実行画面では非表示）");
            List(box, path + ".effects", "従来Effect（先に実行）", p => ((EventEffectKind)p.FindPropertyRelative("kind").intValue).ToString(), LegacyEffect);
            List(box, path + ".typedEffects", "Effect", EffectSummary, TypedEffect, false);
            var add = Row();
            foreach (var type in new[] { typeof(GoldEffect), typeof(ItemEffect), typeof(HintEffect), typeof(RelationEffect), typeof(FlagEffect) })
                Button(add, "+ " + type.Name.Replace("Effect", ""), () => { var p = P(path + ".typedEffects"); int index = p.arraySize++; p.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type); Changed(); Later(); });
            box.Add(add);
            List(box, path + ".followUps", "FollowUp（後続イベント）", p => (p.FindPropertyRelative("definition").objectReferenceValue?.name ?? "未設定") + " / " + p.FindPropertyRelative("delayPhaseCount").intValue + "回後", (parent, p) =>
            {
                AssetChoice<EventDefinitionSO>(parent, p.propertyPath + ".definition", "後続イベント");
                if (selected.useExtendedLifecycle) { Field(parent, p.propertyPath + ".delayPhaseCount", "親の解決後、出現までの昼夜切替回数"); Field(parent, p.propertyPath + ".allowConcurrent", "同じイベントの重複発生を許可"); Note(parent, "0回は親解決後に出現を試みます。重複禁止は発生中と予約中の両方が対象。対象条件・空セル等で出現を待つ場合があります。"); }
                else Field(parent, p.propertyPath + ".startMode", "後続の開始方式");
            });
        }
        private static string EffectSummary(SerializedProperty p)
        {
            var value = p.managedReferenceValue;
            if (value is GoldEffect gold) return "所持金 " + (gold.amount >= 0 ? "+" : "") + gold.amount;
            if (value is ItemEffect item) return "装備 " + (item.item?.name ?? "未設定") + " × " + item.amount;
            if (value is HintEffect hint) return "ヒント " + (hint.isFixedHint ? hint.fixedHint?.name ?? "未設定" : "プールから抽選");
            if (value is RelationEffect relation) return "施設関係 " + relation.facility?.name + " " + relation.amount;
            if (value is FlagEffect flag) return "Flag " + flag.flag?.name + " = " + flag.value;
            return "未設定Effect（削除して種類を選び直してください）";
        }
        private void TypedEffect(VisualElement box, SerializedProperty p)
        {
            string path = p.propertyPath;
            if (p.managedReferenceValue is GoldEffect) Field(box, path + ".amount", "所持金の増減値");
            else if (p.managedReferenceValue is ItemEffect) { AssetChoice<EquipData>(box, path + ".item", "装備アイテム"); Field(box, path + ".amount", "数量"); }
            else if (p.managedReferenceValue is FlagEffect) { AssetChoice<FlagDefinitionSO>(box, path + ".flag", "対象Flag"); Field(box, path + ".value", "設定値"); }
            else if (p.managedReferenceValue is RelationEffect) { AssetChoice<FacilityDefinitionSO>(box, path + ".facility", "対象施設"); Field(box, path + ".amount", "関係値の増減"); }
            else if (p.managedReferenceValue is HintEffect)
            {
                Field(box, path + ".isFixedHint", "固定ヒントを取得", true);
                if (P(path + ".isFixedHint").boolValue) AssetChoice<HintData>(box, path + ".fixedHint", "取得するヒント");
                else Note(box, "HintLevelタブのプールから抽選します。");
                Note(box, "付与レベルはPointConditionの合計で決まります。");
            }
        }
        private void LegacyEffect(VisualElement box, SerializedProperty p)
        {
            string path = p.propertyPath; Field(box, path + ".kind", "効果の種類", true);
            var kind = (EventEffectKind)p.FindPropertyRelative("kind").intValue;
            if (kind <= EventEffectKind.ChangeParameter) Field(box, path + ".amount", "数量・増減値");
            if (kind == EventEffectKind.ChangeParameter) Field(box, path + ".parameter", "能力値");
            if (kind == EventEffectKind.GrantAbility) AssetChoice<AbilityData>(box, path + ".ability", "アビリティ");
            if (kind == EventEffectKind.GrantEquip) AssetChoice<EquipData>(box, path + ".equip", "装備");
            if (kind == EventEffectKind.GrantHint) AssetChoice<HintData>(box, path + ".hint", "ヒント");
            if (kind == EventEffectKind.UnlockEvent || kind == EventEffectKind.ReserveEvent) AssetChoice<EventDefinitionSO>(box, path + ".eventDefinition", "対象イベント");
            if (kind == EventEffectKind.SetFlag || kind == EventEffectKind.StartBattle) Field(box, path + ".key", "既存接続先キー");
            if (kind == EventEffectKind.SetFlag) Field(box, path + ".flagValue", "Flagの値");
        }
        private void LegacyConditions(VisualElement box, string path, string label)
        {
            List(box, path, label, p => ((EventConditionKind)p.FindPropertyRelative("kind").intValue) + " " + p.FindPropertyRelative("key").stringValue + " " + p.FindPropertyRelative("value").intValue, (parent, p) =>
            {
                Field(parent, p.propertyPath + ".kind", "既存条件の種類", true);
                var kind = (EventConditionKind)p.FindPropertyRelative("kind").intValue;
                if (kind == EventConditionKind.ResourceAtLeast) { Note(parent, "PartyEventWorldStateで登録済みの所持金キーは Gold です。比較は既存仕様の≧のみ。"); Field(parent, p.propertyPath + ".key", "リソースキー（Gold）"); }
                if (kind == EventConditionKind.FlagEquals) { Field(parent, p.propertyPath + ".key", "既存WorldのFlagキー"); Field(parent, p.propertyPath + ".flagValue", "Flagの値"); }
                if (kind == EventConditionKind.ParameterAtLeast) Field(parent, p.propertyPath + ".parameter", "能力値");
                if (kind == EventConditionKind.TotalAtLeast || kind == EventConditionKind.TotalBelow || kind == EventConditionKind.ParameterAtLeast || kind == EventConditionKind.ResourceAtLeast) Field(parent, p.propertyPath + ".value", "比較値");
            });
        }
        private void List(VisualElement parent, string path, string label, Func<SerializedProperty, string> summary, Action<VisualElement, SerializedProperty> draw, bool allowAdd = true)
        {
            var group = Group(parent, label); var array = P(path);
            if (array == null) { Note(group, "データがありません", HelpBoxMessageType.Warning); return; }
            for (int i = 0; i < array.arraySize; i++)
            {
                int n = i; var p = array.GetArrayElementAtIndex(i); string key = path + ":" + i;
                var line = Group(group, (i + 1) + ". " + summary(p), SessionState.GetBool("EventEditor:" + selected.GetInstanceID() + key, false));
                summaryUpdates.Add(() => { var current = P(path); if (current != null && n < current.arraySize) line.text = (n + 1) + ". " + summary(current.GetArrayElementAtIndex(n)); });
                line.RegisterValueChangedCallback(e => { if (e.target == line) SessionState.SetBool("EventEditor:" + selected.GetInstanceID() + key, e.newValue); });
                var actions = Row();
                if (i > 0) Button(actions, "↑", () => { P(path).MoveArrayElement(n, n - 1); Changed(); Later(); });
                if (i + 1 < array.arraySize) Button(actions, "↓", () => { P(path).MoveArrayElement(n, n + 1); Changed(); Later(); });
                Button(actions, "削除", () => { var a = P(path); int size = a.arraySize; a.DeleteArrayElementAtIndex(n); if (a.arraySize == size) a.DeleteArrayElementAtIndex(n); Changed(); Later(); });
                line.Add(actions); draw(line, p.Copy());
            }
            if (allowAdd) Button(group, "+ 追加", () =>
            {
                var a = P(path); int index = a.arraySize++; var p = a.GetArrayElementAtIndex(index); Reset(p);
                if (path == "extension.conditions" || path == "extension.pointConditions") ResetQuery(p.FindPropertyRelative("query"), ConditionSource.World);
                if (path == "extension.pointConditions") p.FindPropertyRelative("addPoint").intValue = 1;
                if (path == "reservation.cardSlots") { p.FindPropertyRelative("amount").intValue = 1; p.FindPropertyRelative("required").boolValue = true; p.FindPropertyRelative("historyParticipant.slotNumber").intValue = 1; }
                if (path.EndsWith(".effects")) p.FindPropertyRelative("amount").intValue = 1;
                SessionState.SetBool("EventEditor:" + selected.GetInstanceID() + path + ":" + index, true); Changed(); Later();
            });
        }
        private static void Reset(SerializedProperty p)
        {
            if (p.isArray && p.propertyType != SerializedPropertyType.String) { p.ClearArray(); return; }
            switch (p.propertyType)
            {
                case SerializedPropertyType.String: p.stringValue = ""; break;
                case SerializedPropertyType.Boolean: p.boolValue = false; break;
                case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: p.intValue = 0; break;
                case SerializedPropertyType.Float: p.floatValue = 0; break;
                case SerializedPropertyType.ObjectReference: p.objectReferenceValue = null; break;
                case SerializedPropertyType.ManagedReference: p.managedReferenceValue = null; break;
                case SerializedPropertyType.Generic:
                    var child = p.Copy(); var end = p.GetEndProperty();
                    if (child.Next(true)) do { if (SerializedProperty.EqualContents(child, end)) break; Reset(child.Copy()); } while (child.Next(false)); break;
            }
        }
        private void Save()
        {
            rootVisualElement.Focus(); serialized?.ApplyModifiedProperties();
            if (selected != null) AssetDatabase.SaveAssetIfDirty(selected);
            if (catalog != null) AssetDatabase.SaveAssetIfDirty(catalog);
            Refresh();
        }
        private void Create(bool duplicate)
        {
            if (catalog == null || duplicate && selected == null) { EditorUtility.DisplayDialog("イベント作成", "登録先カタログと、複製する場合はイベントを選択してください。", "閉じる"); return; }
            var path = EditorUtility.SaveFilePanelInProject(duplicate ? "イベントを複製" : "イベントを作成", duplicate ? selected.name + "_Copy" : "NewEvent", "asset", "保存先を選択");
            if (string.IsNullOrEmpty(path)) return;
            selected = EventDatabaseWindow.CreateAsset(path, duplicate ? selected : null);
            if (!duplicate) { var so = new SerializedObject(selected); so.FindProperty("useExtendedLifecycle").boolValue = true; so.FindProperty("enabled").boolValue = false; so.ApplyModifiedProperties(); }
            RegisterSelected(); Refresh();
        }
        private void RegisterSelected()
        {
            if (catalog == null || selected == null) return;
            var so = new SerializedObject(catalog); var p = so.FindProperty("definitions");
            if (!catalog.definitions.Contains(selected)) { int i = p.arraySize++; p.GetArrayElementAtIndex(i).objectReferenceValue = selected; so.ApplyModifiedProperties(); }
            Later();
        }
        private void Delete()
        {
            if (selected == null) return;
            var path = AssetDatabase.GetAssetPath(selected);
            var refs = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != path && !AssetDatabase.IsValidFolder(p))
                .Where(p => AssetDatabase.GetDependencies(p, false).Contains(path)).ToArray();
            if (!EditorUtility.DisplayDialog("イベントアセットを削除", selected.displayName + " をごみ箱へ移動します。アセット削除はUndoできません。\n参照元 " + refs.Length + "件（参照切れの可能性）\n" + string.Join("\n", refs.Take(12)) + "\nカタログからは登録を解除します。", "削除", "キャンセル")) return;
            // Move succeeds before touching catalogs; no dangling catalog edits on filesystem failure.
            var catalogs = AssetDatabase.FindAssets("t:EventCatalogSO").Select(g => AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null && c.definitions.Contains(selected))
                .Select(c => new { catalog = c, indices = c.definitions.Select((d, i) => new { d, i }).Where(x => x.d == selected).Select(x => x.i).OrderByDescending(i => i).ToArray() }).ToArray();
            if (!AssetDatabase.MoveAssetToTrash(path)) { EditorUtility.DisplayDialog("削除失敗", "アセットを移動できませんでした。", "閉じる"); return; }
            foreach (var entry in catalogs) { Undo.RecordObject(entry.catalog, "イベント登録解除"); foreach (int i in entry.indices) entry.catalog.definitions.RemoveAt(i); EditorUtility.SetDirty(entry.catalog); AssetDatabase.SaveAssetIfDirty(entry.catalog); }
            selected = null; Refresh();
        }
        private void Validation()
        {
            Button(detail, "全イベントを再検証", Refresh);
            Note(detail, "既存EventValidatorを使用。エラーは設定不備、警告は要確認、情報は仕様上の注意です。保存を禁止する機能ではありません。");
            foreach (var issue in EventValidator.Validate(definitions, catalog))
            {
                var row = Group(detail, (issue.asset?.name ?? "カタログ") + " / " + issue.severity);
                Note(row, issue.message, issue.severity == EventIssueSeverity.Error ? HelpBoxMessageType.Error : issue.severity == EventIssueSeverity.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info);
                if (issue.asset is EventDefinitionSO d) Button(row, "該当設定へ", () => { selected = d; tabIndex = IssueTab(issue.message); Refresh(); });
            }
        }
        private static int IssueTab(string text)
        {
            var t = text.ToLowerInvariant();
            if (t.Contains("slot") || t.Contains("involved")) return 2;
            if (t.Contains("point") || t.Contains("threshold") || t.Contains("pool")) return 4;
            if (t.Contains("condition") || t.Contains("query")) return 1;
            if (t.Contains("phase") || t.Contains("deadline")) return 3;
            if (t.Contains("effect") || t.Contains("follow") || t.Contains("outcome") || t.Contains("judge")) return 5;
            return 0;
        }
        private void Graph()
        {
            Note(detail, "閲覧専用。青い実線: FollowUp（結果を表示）／橙の破線: 履歴参照。ノードをクリックして基本タブへ移動します。");
            detail.Add(new EventRelationGraph(definitions, d => { selected = d; tabIndex = 0; Refresh(); }));
        }
    }
}
