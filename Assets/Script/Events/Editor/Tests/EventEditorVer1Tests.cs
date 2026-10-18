using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MusicMap.Events.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Tests
{
    public sealed class EventEditorVer1Tests
    {
        private readonly List<Object> objects = new List<Object>();
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); objects.Add(a); return a; }
        private EventDefinitionSO Definition(string code)
        {
            var d = Asset<EventDefinitionSO>(); d.eventCode = d.displayName = code; d.useExtendedLifecycle = true;
            d.execution.judge.useDice = false; d.execution.judge.target = 0; d.extension.durationPhaseCount = 0; return d;
        }
        private EventCatalogSO Catalog(params EventDefinitionSO[] definitions)
        {
            var c = Asset<EventCatalogSO>(); c.definitions.AddRange(definitions); c.categoryCatalog = Asset<EventCategoryCatalogSO>();
            c.categoryCatalog.EditorTryAdd("Test", "", 0, out var category, out _);
            foreach (var d in definitions) { var so = new SerializedObject(d); so.FindProperty("categoryId").stringValue = category.Id; so.FindProperty("categoryMigrationState").intValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); }
            return c;
        }
        private sealed class Cards : IEventCardSource, IEventParticipantSource, IEventHistoryCardSource
        {
            public bool available = true;
            public bool TryGetCard(string id, out CardType type, out int amount) { type = CardType.Chara; amount = 1; return available && (id == "a/card" || id == "b/card"); }
            public bool TryGetParameter(IReadOnlyList<string> ids, EventParameter p, out int value) { value = 5; return true; }
            public IEnumerable<string> Participants(IReadOnlyList<string> ids) => ids.Where(id => id == "a/card" || id == "b/card").Select(id => id.Substring(0, 1));
            public bool TryCharacter(string id, EventParameter p, out int value) { value = 5; return available && (id == "a" || id == "b"); }
            public IEnumerable<string> Characters(CharaData d) => new[] { "a", "b" };
            public string FindCard(Object d) => "a/card";
            public string FindCharacterCard(string id) => available ? id + "/card" : null;
        }
        private static void Resolve(EventManager manager, EventInstance instance, Cards cards)
        {
            using (var runner = new EventRunner(manager, cards, null, new EventEffectExecutor(manager, null, null, null, null), null) { CanPresentNarrative = true })
                for (int n = 0; n < 20 && instance.status != EventStatus.Resolved; n++) { runner.Tick(); runner.Continue(); }
            Assert.AreEqual(EventStatus.Resolved, instance.status, instance.lastError);
        }
        private static CardSlotDefinition HistorySlot(EventDefinitionSO source, ParticipantHistorySource mode) => new CardSlotDefinition
        { historyParticipant = new ParticipantSlotReference { enabled = true, definition = source, source = mode, slotNumber = 1 } };
        [TearDown] public void Cleanup() { foreach (var obj in objects) if (obj != null) Object.DestroyImmediate(obj); objects.Clear(); }

        [Test] public void ParentSlotSnapshotSurvivesSave_AndDoesNotUseNewerOccurrence()
        {
            var a = Definition("A"); var b = Definition("B"); a.reservation.cardSlots.Add(new CardSlotDefinition()); b.reservation.cardSlots.Add(HistorySlot(a, ParticipantHistorySource.FollowUpParent));
            a.extension.success.followUps.Add(new EventFollowUp { definition = b, delayPhaseCount = 1, allowConcurrent = true });
            var cards = new Cards(); var catalog = Catalog(a, b); var manager = new EventManager(catalog, null, cards);
            var parent = manager.Spawn(a, Vector2Int.zero); Assert.IsTrue(manager.Reserve(parent.instanceId, new[] { "a/card" }, 0, out _)); Resolve(manager, parent, cards);
            var newer = manager.Spawn(a, Vector2Int.one); Assert.IsTrue(manager.Reserve(newer.instanceId, new[] { "b/card" }, 0, out _)); Resolve(manager, newer, cards);
            manager.AdvancePhase(1);
            var child = manager.Instances.Single(i => i.definitionId == b.PersistentId && i.sourceEventInstanceId == parent.instanceId);
            var saved = JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(manager.CreateSaveData())); Assert.AreEqual(3, saved.version);
            var restored = new EventManager(catalog, null, cards); Assert.IsEmpty(restored.Restore(saved));
            Assert.IsFalse(restored.Reserve(child.instanceId, new[] { "b/card" }, 1, out _));
            Assert.IsTrue(restored.Reserve(child.instanceId, new[] { "" }, 1, out var error), error);
            Assert.IsTrue(restored.TryGet(child.instanceId, out var assigned)); Assert.AreEqual("a/card", assigned.assignedCardIds[0]);
            Assert.AreEqual("a", assigned.slotAssignments[0].characterId);
        }
        [Test] public void LatestMatchingHistory_RespectsOutcomeAndMissingSlots()
        {
            var a = Definition("A"); var b = Definition("B"); var slot = HistorySlot(a, ParticipantHistorySource.LatestMatching); b.reservation.cardSlots.Add(slot);
            var manager = new EventManager(Catalog(a, b), null, new Cards()); var child = manager.Spawn(b, Vector2Int.zero);
            Assert.IsFalse(manager.Reserve(child.instanceId, new[] { "" }, 0, out _));
            manager.Progress.history.Add(new EventHistoryRecord { definitionId = a.PersistentId, instanceId = "first", resolved = true, outcome = OutcomeType.SUCCESS, participantIds = new List<string> { "a" }, slotAssignments = new List<EventSlotAssignment> { new EventSlotAssignment { slotNumber = 1, characterId = "a" } } });
            manager.Progress.history.Add(new EventHistoryRecord { definitionId = a.PersistentId, instanceId = "old-format", resolved = true, outcome = OutcomeType.FAILURE, participantIds = new List<string> { "b" } });
            Assert.IsFalse(manager.Reserve(child.instanceId, new[] { "b/card" }, 0, out var missing)); StringAssert.Contains("未記録", missing);
            slot.historyParticipant.filterOutcome = true; slot.historyParticipant.outcome = OutcomeType.SUCCESS;
            Assert.IsTrue(manager.Reserve(child.instanceId, new[] { "" }, 0, out var error), error);
        }
        [Test] public void UnavailableCharacterCannotBeSubstituted()
        {
            var a = Definition("A"); var b = Definition("B"); b.reservation.cardSlots.Add(HistorySlot(a, ParticipantHistorySource.LatestMatching));
            var cards = new Cards { available = false }; var manager = new EventManager(Catalog(a, b), null, cards);
            manager.Progress.history.Add(new EventHistoryRecord { definitionId = a.PersistentId, instanceId = "a", resolved = true, slotAssignments = new List<EventSlotAssignment> { new EventSlotAssignment { slotNumber = 1, characterId = "a" } } });
            var child = manager.Spawn(b, Vector2Int.zero); Assert.IsFalse(manager.Reserve(child.instanceId, new[] { "b/card" }, 0, out var error)); StringAssert.Contains("利用できません", error);
        }
        [Test] public void ExpiryKeepsBothParticipantCollectionsEmpty_AndAllOfZeroIsFalse()
        {
            var a = Definition("A"); a.extension.deadlinePhaseCount = 0; a.reservation.cardSlots.Add(new CardSlotDefinition());
            var cards = new Cards(); var manager = new EventManager(Catalog(a), null, cards);
            Assert.IsFalse(manager.Evaluate(new EventQuery { source = ConditionSource.EventHistory, eventDefinition = a, historyMode = HistoryMode.All }));
            var expired = manager.Spawn(a, Vector2Int.zero); Resolve(manager, expired, cards);
            Assert.IsEmpty(manager.Progress.history.Single().participantIds); Assert.IsEmpty(manager.Progress.history.Single().slotAssignments);
            Assert.AreEqual(OutcomeType.EXPIRED, manager.Progress.history.Single().outcome);
        }
        [TestCase(1)] [TestCase(2)] public void OldSaveLoadsWithoutInferringSlots(int version)
        {
            var a = Definition("A"); var catalog = Catalog(a); var manager = new EventManager(catalog);
            var save = new EventSaveData { version = version };
            save.progress.history.Add(new EventHistoryRecord { instanceId = "old", definitionId = a.PersistentId, participantIds = new List<string> { "a" }, resolved = true, slotAssignments = null });
            Assert.IsEmpty(manager.Restore(JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(save))));
            if (version == 2) Assert.IsTrue(manager.Progress.history[0].slotAssignments == null || manager.Progress.history[0].slotAssignments.Count == 0);
        }
        [Test] public void TwoFollowUps_SuppressPendingAndActiveUnlessConcurrent()
        {
            var a = Definition("A"); var b = Definition("B"); var c = Definition("C");
            var follow = new EventFollowUp { definition = b, delayPhaseCount = 1 };
            a.extension.success.followUps.AddRange(new[] { follow, follow, new EventFollowUp { definition = c, delayPhaseCount = 1 } });
            var cards = new Cards(); var manager = new EventManager(Catalog(a, b, c), null, cards); var parent = manager.Spawn(a, Vector2Int.zero);
            Assert.IsTrue(manager.Reserve(parent.instanceId, Array.Empty<string>(), 0, out _)); Resolve(manager, parent, cards);
            Assert.AreEqual(2, manager.Progress.followUps.Count);
            manager.AdvancePhase(1); Assert.AreEqual(3, manager.Instances.Count());
            var second = manager.Spawn(a, Vector2Int.zero); Assert.IsTrue(manager.Reserve(second.instanceId, Array.Empty<string>(), 1, out _)); Resolve(manager, second, cards); Assert.IsEmpty(manager.Progress.followUps);
            a.extension.success.followUps.RemoveAt(1); follow.allowConcurrent = true;
            var third = manager.Spawn(a, Vector2Int.zero); Assert.IsTrue(manager.Reserve(third.instanceId, Array.Empty<string>(), 1, out _)); Resolve(manager, third, cards); Assert.AreEqual(1, manager.Progress.followUps.Count);
        }
        [Test] public void QueryChangeClearsOldTargets_AndPointsStaySeparate()
        {
            var a = Definition("A"); var b = Definition("B");
            a.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.EventHistory, eventDefinition = b, historyMode = HistoryMode.All, comparison = Comparison.NotEqual } });
            a.extension.pointConditions.Add(new PointCondition { addPoint = 9 });
            using (var so = new SerializedObject(a))
            {
                EventEditorWindow.ResetQuery(so.FindProperty("extension.conditions").GetArrayElementAtIndex(0).FindPropertyRelative("query"), ConditionSource.World); so.ApplyModifiedProperties();
            }
            Assert.IsNull(a.extension.conditions[0].query.eventDefinition); Assert.AreEqual(Comparison.Equal, a.extension.conditions[0].query.comparison);
            Assert.AreEqual(9, a.extension.pointConditions[0].addPoint);
        }
        [UnityTest] public IEnumerator Window_AllTabsAndExistingAssetsAreReadOnlyUntilEdited()
        {
            var definitions = AssetDatabase.FindAssets("t:EventDefinitionSO").Select(g => AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var example = Definition("Editor test"); example.reservation.cardSlots.Add(new CardSlotDefinition());
            example.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.World } });
            example.extension.pointConditions.Add(new PointCondition()); example.extension.success.typedEffects.Add(new HintEffect()); definitions.Add(example);
            var window = ScriptableObject.CreateInstance<EventEditorWindow>(); window.Show();
            try
            {
                yield return null;
                foreach (var d in definitions)
                {
                    string before = EditorJsonUtility.ToJson(d); window.Select(d);
                    for (int tab = 0; tab < 8; tab++) { window.SelectTab(tab); yield return null; }
                    Assert.AreEqual(before, EditorJsonUtility.ToJson(d), "Opening tabs mutated " + d.name);
                }
                Assert.AreEqual(8, EventEditorWindow.Tabs.Length);
            }
            finally { window.Close(); }
        }
        [Test] public void SerializedEditsUndoRedo_AndPersistAcrossReimport()
        {
            string folder = "Assets/EventEditorTest_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            try
            {
                var d = EventDatabaseWindow.CreateAsset(folder + "/Event.asset", null); string id = d.PersistentId;
                Undo.IncrementCurrentGroup();
                using (var so = new SerializedObject(d))
                {
                    so.FindProperty("displayName").stringValue = "保存テスト"; so.FindProperty("useExtendedLifecycle").boolValue = true;
                    var points = so.FindProperty("extension.pointConditions"); points.arraySize = 2; points.GetArrayElementAtIndex(1).FindPropertyRelative("addPoint").intValue = 7;
                    so.FindProperty("extension.success.typedEffects").arraySize = 1;
                    so.FindProperty("extension.success.typedEffects").GetArrayElementAtIndex(0).managedReferenceValue = new GoldEffect { amount = 12 };
                    so.ApplyModifiedProperties();
                }
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreNotEqual("保存テスト", d.displayName); Undo.PerformRedo(); Assert.AreEqual("保存テスト", d.displayName);
                AssetDatabase.SaveAssetIfDirty(d); AssetDatabase.ImportAsset(folder + "/Event.asset", ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(folder + "/Event.asset");
                Assert.AreEqual(id, loaded.PersistentId); Assert.AreEqual(7, loaded.extension.pointConditions[1].addPoint); Assert.IsEmpty(loaded.extension.conditions);
                Assert.AreEqual(12, ((GoldEffect)loaded.extension.success.typedEffects[0]).amount);
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        // Unity batch mode has no keyboard focus. Invoke the Clickable handler, then use
        // real field value-change events and binding; this is not a physical mouse test.
        private static void Click(Button button) => typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Invoke(button.clickable, new object[] { null });
        [UnityTest] public IEnumerator Window_ControlCallbacksAddSwitchReorderAndRetainValues()
        {
            var d = Definition("Controls");
            var window = ScriptableObject.CreateInstance<EventEditorWindow>(); window.position = new Rect(20, 20, 900, 700); window.Show();
            try
            {
                yield return null; window.Select(d); window.SelectTab(1); yield return null;
                var group = window.rootVisualElement.Query<Foldout>().ToList().First(f => f.text == "発生条件");
                var add = group.Query<Button>().ToList().First(b => b.text == "+ 追加");
                Click(add);
                yield return null; yield return null;
                Assert.AreEqual(1, d.extension.conditions.Count);
                var source = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "判定対象");
                source.value = "キャラクター能力"; yield return null; yield return null;
                Assert.AreEqual(ConditionSource.Character, d.extension.conditions[0].query.source);
                var comparison = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "演算子");
                Assert.AreEqual(6, comparison.choices.Count); comparison.value = "≧";
                source = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "判定対象"); source.value = "時間帯";
                yield return null; yield return null;
                Assert.AreEqual(Comparison.Equal, d.extension.conditions[0].query.comparison);
                comparison = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "演算子"); Assert.AreEqual(2, comparison.choices.Count);
                var phase = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "時間帯"); phase.value = "夜";
                window.SelectTab(4); yield return null;
                group = window.rootVisualElement.Query<Foldout>().ToList().First(f => f.text == "ポイント条件");
                add = group.Query<Button>().ToList().First(b => b.text == "+ 追加"); Click(add);
                yield return null; yield return null;
                Assert.AreEqual(1, d.extension.pointConditions.Count); Assert.AreEqual(WorldPhase.Night, d.extension.conditions[0].query.phase);
                window.SelectTab(1); yield return null;
                phase = window.rootVisualElement.Query<PopupField<string>>().ToList().First(p => p.label == "時間帯"); Assert.AreEqual("夜", phase.value);
                // Binding must not continuously rebuild the window and lose the input focus.
                for (int frame = 0; frame < 8; frame++) yield return null;
                Assert.IsNotNull(phase.panel);
                group = window.rootVisualElement.Query<Foldout>().ToList().First(f => f.text == "発生条件");
                add = group.Query<Button>().ToList().First(b => b.text == "+ 追加"); Click(add);
                yield return null; yield return null;
                var up = window.rootVisualElement.Query<Button>().ToList().First(b => b.text == "↑"); Click(up);
                yield return null; yield return null;
                Assert.AreEqual(WorldPhase.Night, d.extension.conditions[1].query.phase);
                window.SelectTab(2); yield return null;
                group = window.rootVisualElement.Query<Foldout>().ToList().First(f => f.text == "参加枠"); add = group.Query<Button>().ToList().First(b => b.text == "+ 追加");
                Click(add);
                yield return null; yield return null;
                Assert.AreEqual(1, d.reservation.cardSlots[0].amount); Assert.AreEqual(1, d.reservation.cardSlots[0].historyParticipant.slotNumber);
                window.SelectTab(5); yield return null;
                add = window.rootVisualElement.Query<Button>().ToList().First(b => b.text == "+ Gold"); Click(add);
                yield return null; yield return null;
                Assert.IsInstanceOf<GoldEffect>(d.extension.success.typedEffects.Single());
                window.position = new Rect(20, 20, 650, 420); yield return null;
                Assert.Greater(window.rootVisualElement.Q<TwoPaneSplitView>().worldBound.width, 0);
            }
            finally { window.Close(); }
        }
    }
}
