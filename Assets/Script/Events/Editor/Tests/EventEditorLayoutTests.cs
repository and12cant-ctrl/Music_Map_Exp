using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Editor.Tests
{
    public sealed class EventEditorLayoutTests
    {
        private readonly List<Object> objects = new List<Object>();
        private T Asset<T>() where T : ScriptableObject { var obj = ScriptableObject.CreateInstance<T>(); objects.Add(obj); return obj; }
        [TearDown] public void Cleanup() { foreach (var obj in objects) if (obj != null) Object.DestroyImmediate(obj); objects.Clear(); }

        [UnityTest] public IEnumerator LongContentFitsRightPane_AcrossTabsWindowSizesAndSplitWidths()
        {
            string longName = string.Concat(Enumerable.Repeat("長いイベント名と説明_", 12));
            var definition = Asset<EventDefinitionSO>(); definition.displayName = longName; definition.eventCode = "LAYOUT"; definition.useExtendedLifecycle = true;
            var pool = Asset<HintPoolSO>(); pool.name = longName; definition.extension.randomHintPool = pool;
            for (int i = 0; i < 3; i++)
            {
                definition.extension.pointConditions.Add(new PointCondition { query = new EventQuery { source = ConditionSource.EventHistory, eventDefinition = definition } });
                definition.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.EventHistory, eventDefinition = definition } });
            }
            definition.reservation.cardSlots.Add(new CardSlotDefinition { label = longName });
            definition.extension.success.followUps.Add(new EventFollowUp { definition = definition });
            definition.extension.success.typedEffects.Add(new HintEffect());
            var catalog = Asset<EventCatalogSO>(); catalog.name = longName; catalog.definitions.Add(definition);
            string before = EditorJsonUtility.ToJson(definition);
            var window = ScriptableObject.CreateInstance<EventEditorWindow>(); window.Show();
            var log = new List<string>();
            try
            {
                yield return null;
                // Change the same ObjectField used by the author; its existing callback rebuilds the UI.
                window.rootVisualElement.Q<Toolbar>().Q<ObjectField>().value = catalog;
                window.Select(definition);
                var sizes = new[] { new Vector2(1000, 760), new Vector2(650, 420), new Vector2(1600, 1000),
                    new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) };
                foreach (var size in sizes)
                {
                    window.position = new Rect(20, 20, size.x, size.y);
                    foreach (float leftWidth in new[] { 180f, 240f, size.x - 390f })
                    {
                        window.rootVisualElement.Q<TwoPaneSplitView>().fixedPaneInitialDimension = leftWidth;
                        for (int tab = 0; tab < 8; tab++)
                        {
                            window.SelectTab(tab);
                            yield return Settle(window);
                            CheckLayout(window, tab, log);
                        }
                    }
                }
                // A floating batch window may not support native maximize. Record the actual
                // flag, never report the preceding large-window measurements as native maximize.
                window.maximized = true;
                for (int tab = 0; tab < 8; tab++) { window.SelectTab(tab); yield return Settle(window); CheckLayout(window, tab, log); }
                log.Add("maximized requested; reported=" + window.maximized + "; rect=" + window.position);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(definition), "Layout changes must not modify the definition.");
            }
            finally
            {
                Directory.CreateDirectory("Logs/EventEditorLayout"); File.WriteAllLines("Logs/EventEditorLayout/geometry.txt", log);
                window.maximized = false; window.Close();
            }
        }
        private static IEnumerator Settle(EventEditorWindow window)
        {
            for (int i = 0; i < 3; i++) yield return null;
            foreach (var foldout in window.rootVisualElement.Q<TwoPaneSplitView>().flexedPane.Query<Foldout>().ToList()) foldout.value = true;
            for (int i = 0; i < 5; i++) yield return null;
        }
        private static bool Visible(VisualElement element)
        {
            for (var node = element; node != null; node = node.parent)
                if (node.resolvedStyle.display == DisplayStyle.None || node.resolvedStyle.visibility == Visibility.Hidden) return false;
            return element.worldBound.width > .1f && element.worldBound.height > .1f;
        }
        private static void CheckLayout(EventEditorWindow window, int tab, List<string> log)
        {
            var root = window.rootVisualElement;
            var toolbar = root.Q<Toolbar>(); var catalog = toolbar.Q<ObjectField>();
            var save = toolbar.Query<Button>().ToList().Single(b => b.text == "保存");
            var inspector = toolbar.Query<Button>().ToList().Single(b => b.text == "Inspector");
            Assert.LessOrEqual(catalog.worldBound.xMax, save.worldBound.xMin + 1, "Catalog overlaps Save");
            Assert.LessOrEqual(save.worldBound.xMax, inspector.worldBound.xMin + 1, "Save overlaps Inspector");
            Assert.LessOrEqual(inspector.worldBound.xMax, toolbar.worldBound.xMax + 1, "Toolbar overflows");
            foreach (var b in new[] { save, inspector })
                Assert.GreaterOrEqual(b.contentRect.width + 1, b.MeasureTextSize(b.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x, b.text + " text clipped");
            var split = root.Q<TwoPaneSplitView>(); var scroll = split.flexedPane.Q<ScrollView>(); var viewport = scroll.contentViewport.worldBound;
            string context = $"window={window.position.size} left={split.fixedPane.worldBound.width:F1} pane={split.flexedPane.worldBound.width:F1} tab={EventEditorWindow.Tabs[tab]} viewport={viewport.width:F1} content={scroll.contentContainer.worldBound.width:F1}";
            log.Add(context);
            Assert.Greater(viewport.width, 250, context);
            Assert.LessOrEqual(scroll.contentContainer.worldBound.xMax, viewport.xMax + 1.5f, context);
            foreach (var node in scroll.contentContainer.Query<VisualElement>().ToList().Where(n => Visible(n) && (n is Label || n is Button || n is HelpBox || n is Foldout || n is PropertyField || n.ClassListContains("unity-base-field"))))
            {
                Assert.LessOrEqual(node.worldBound.xMax, viewport.xMax + 1.5f, context + " overflowing " + node.GetType().Name + " " + node.name + " " + (node as TextElement)?.text);
                if (node is TextElement text && text.resolvedStyle.whiteSpace == WhiteSpace.Normal && !string.IsNullOrEmpty(text.text))
                {
                    float requiredHeight = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined).y;
                    Assert.GreaterOrEqual(text.contentRect.height + 1.5f, requiredHeight, context + " wrapping text clipped vertically: " + text.text);
                }
            }
            if (tab == 4)
            {
                Assert.AreEqual(ScrollViewMode.Vertical, scroll.mode);
                // A tall/wide window may fit everything; require scrolling only when needed.
                if (window.position.height <= 760) Assert.Greater(scroll.contentContainer.worldBound.height, viewport.height, "Expanded PointConditions must still scroll vertically");
                if (scroll.contentContainer.worldBound.height > viewport.height + 1.5f) Assert.Greater(scroll.verticalScroller.highValue, 0);
                Assert.LessOrEqual(scroll.horizontalScroller.highValue, 1.5f, "HintLevel must not need horizontal scrolling");
            }
        }

        [UnityTest] public IEnumerator ToolbarSelectionSaveAndInspectorRemainFunctional()
        {
            string folder = "Assets/EventLayoutTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var previousSelection = Selection.activeObject;
            var window = ScriptableObject.CreateInstance<EventEditorWindow>(); window.Show();
            try
            {
                var definition = Asset<EventDefinitionSO>(); definition.displayName = "変更前"; definition.eventCode = "LAYOUT"; definition.useExtendedLifecycle = true;
                var catalog = Asset<EventCatalogSO>(); catalog.definitions.Add(definition);
                AssetDatabase.CreateAsset(definition, folder + "/Event.asset"); AssetDatabase.CreateAsset(catalog, folder + "/Catalog.asset");
                yield return null;
                window.rootVisualElement.Q<Toolbar>().Q<ObjectField>().value = catalog; window.Select(definition);
                yield return Settle(window);
                Assert.AreSame(catalog, window.rootVisualElement.Q<Toolbar>().Q<ObjectField>().value);
                var name = window.rootVisualElement.Query<PropertyField>().ToList().Single(f => f.label == "イベント名（必須）").Q<TextField>();
                name.value = "編集・保存の確認";
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.AreEqual("編集・保存の確認", definition.displayName);
                Click(window.rootVisualElement.Q<Toolbar>().Query<Button>().ToList().Single(b => b.text == "保存"));
                yield return null;
                Assert.IsFalse(EditorUtility.IsDirty(definition));
                AssetDatabase.ImportAsset(folder + "/Event.asset", ImportAssetOptions.ForceUpdate);
                Assert.AreEqual("編集・保存の確認", AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(folder + "/Event.asset").displayName);
                Click(window.rootVisualElement.Q<Toolbar>().Query<Button>().ToList().Single(b => b.text == "Inspector"));
                Assert.AreSame(definition, Selection.activeObject);
            }
            finally { window.Close(); Selection.activeObject = previousSelection; AssetDatabase.DeleteAsset(folder); }
        }
        private static void Click(Button button) => typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Invoke(button.clickable, new object[] { null });
    }
}
