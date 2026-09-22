using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MusicMap.Events.Editor
{
    public static class EventUiPrefabBuilder
    {
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts 1/NanoSans/NotoSansJP-Bold SDF.asset");
        public static void BuildDefault()
        {
            const string path = "Assets/Prehab/EventSystemUI.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) Build(path);
            Debug.Log("[EventUiPrefabBuilder] UI prefab is ready at " + path);
        }
        [MenuItem("Tools/Events/Create UI Prefab")]
        public static void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Event UI", "EventSystemUI", "prefab", "Create a separate UI prefab; existing scenes remain unchanged.");
            if (!string.IsNullOrEmpty(path)) Selection.activeObject = Build(path);
        }

        public static GameObject Build(string path)
        {
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prehab/カードスロット.prefab")?.GetComponent<CardSlot>();
            if (slot == null) throw new System.InvalidOperationException("Existing CardSlot prefab was not found.");
            var root = new GameObject("EventSystemUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                var reservation = root.AddComponent<ReservationView>();
                var reservePanel = Rect("Reservation", root.transform, new Vector2(.02f, .25f), new Vector2(.61f, .98f));
                Background(reservePanel);
                reservation.panel = reservePanel.gameObject;
                reservation.title = Text("Title", reservePanel, "イベント予約", .04f, .88f, .96f, .97f, 30);
                reservation.background = Rect("Background", reservePanel, new Vector2(.04f, .51f), new Vector2(.4f, .86f)).gameObject.AddComponent<Image>();
                reservation.background.preserveAspect = true;
                reservation.background.raycastTarget = false;
                reservation.description = Text("Description", reservePanel, "", .44f, .5f, .96f, .85f, 23);
                reservation.requiredTime = Text("RequiredTime", reservePanel, "", .04f, .43f, .96f, .5f, 23);
                reservation.requirements = ScrollText("Requirements", reservePanel, .66f, .19f, .96f, .41f, 20);
                var slotArea = ScrollArea("Slots", reservePanel, .04f, .19f, .63f, .41f, out var slotContent);
                var grid = slotContent.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(70, 100);
                grid.spacing = new Vector2(12, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 6;
                slotContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                reservation.slotContainer = slotContent;
                reservation.slotPrefab = slot;
                reservation.message = Text("Message", reservePanel, "", .04f, .11f, .96f, .19f, 18);
                reservation.confirmButton = Button("予約", reservePanel, .54f, .02f, .96f, .1f);
                reservation.cancelButton = Button("閉じる", reservePanel, .04f, .02f, .46f, .1f);

                var execution = root.AddComponent<ExecutionView>();
                var schedule = root.AddComponent<EventScheduleView>();
                var schedulePanel = Rect("Schedule", root.transform, new Vector2(.02f, .02f), new Vector2(.61f, .23f));
                Background(schedulePanel);
                Text("Title", schedulePanel, "予約一覧（クリックで取消）", .04f, .76f, .96f, .98f, 20);
                ScrollArea("Rows", schedulePanel, .04f, .05f, .96f, .74f, out var scheduleContent);
                var layout = scheduleContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlHeight = true; layout.childControlWidth = true;
                layout.childForceExpandHeight = false; layout.spacing = 4;
                scheduleContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                schedule.container = scheduleContent;
                var row = Button("Template", schedulePanel, 0, 0, 1, 1);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;
                row.gameObject.SetActive(false);
                schedule.rowPrefab = row;
                var executionPanel = Rect("Execution", root.transform, new Vector2(.04f, .06f), new Vector2(.96f, .94f));
                Background(executionPanel);
                execution.panel = executionPanel.gameObject;
                execution.log = ScrollText("Log", executionPanel, .03f, .12f, .54f, .96f, 25);
                execution.eventImage = Rect("EventImage", executionPanel, new Vector2(.58f, .5f), new Vector2(.97f, .96f)).gameObject.AddComponent<Image>();
                execution.eventImage.preserveAspect = true;
                execution.eventImage.raycastTarget = false;
                execution.dice = Text("Dice", executionPanel, "", .58f, .36f, .97f, .49f, 24);
                execution.rewards = ScrollText("Rewards", executionPanel, .58f, .17f, .97f, .35f, 21);
                execution.message = Text("Message", executionPanel, "", .03f, .02f, .69f, .11f, 18);
                execution.continueButton = Button("続行", executionPanel, .73f, .03f, .97f, .11f);
                reservation.dragLayer = Rect("DragLayer", root.transform, Vector2.zero, Vector2.one);
                reservePanel.gameObject.SetActive(false);
                executionPanel.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static void Background(RectTransform rect)
        { rect.gameObject.AddComponent<Image>().color = new Color(.06f, .08f, .12f, 1); }
        private static TMP_Text Text(string name, Transform parent, string value, float x1, float y1, float x2, float y2, int size)
        {
            var text = Rect(name, parent, new Vector2(x1, y1), new Vector2(x2, y2)).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) text.font = Font;
            text.text = value; text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
            return text;
        }
        private static Button Button(string label, Transform parent, float x1, float y1, float x2, float y2)
        {
            var rect = Rect(label, parent, new Vector2(x1, y1), new Vector2(x2, y2));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.15f, .3f, .45f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text("Label", rect, label, 0, 0, 1, 1, 24).alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static ScrollRect ScrollArea(string name, Transform parent, float x1, float y1, float x2, float y2, out RectTransform content)
        {
            var viewport = Rect(name, parent, new Vector2(x1, y1), new Vector2(x2, y2));
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false;
            content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }
        private static TMP_Text ScrollText(string name, Transform parent, float x1, float y1, float x2, float y2, int size)
        {
            ScrollArea(name, parent, x1, y1, x2, y2, out var content);
            var text = content.gameObject.AddComponent<TextMeshProUGUI>(); text.fontSize = size; text.raycastTarget = false;
            if (Font != null) text.font = Font;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }
    }
}
