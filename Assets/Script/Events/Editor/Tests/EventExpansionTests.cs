using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

namespace MusicMap.Events.Tests
{
    public sealed class EventExpansionTests
    {
        private readonly List<Object> assets = new List<Object>();
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset);
            if (asset is EventIdentitySO) typeof(EventIdentitySO).GetField("persistentId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asset, Guid.NewGuid().ToString("N"));
            return asset;
        }
        private EventDefinitionSO Definition(string code = "EXT")
        {
            var d = Asset<EventDefinitionSO>();
            typeof(EventDefinitionSO).GetField("persistentId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, Guid.NewGuid().ToString("N"));
            d.eventCode = d.displayName = code; d.useExtendedLifecycle = true;
            d.execution.judge.useDice = false; d.execution.judge.target = 0;
            return d;
        }
        private EventCatalogSO Catalog(params EventDefinitionSO[] definitions)
        {
            var c = Asset<EventCatalogSO>(); c.definitions.AddRange(definitions);
            c.categoryCatalog = Asset<EventCategoryCatalogSO>();
            c.categoryCatalog.EditorTryAdd("Test", "", 0, out var category, out _);
            foreach (var d in definitions)
            {
                typeof(EventDefinitionSO).GetField("categoryId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, category.Id);
                typeof(EventDefinitionSO).GetField("categoryMigrationState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, EventCategoryMigrationState.Migrated);
            }
            return c;
        }
        private MapDataSO Map() { var m = Asset<MapDataSO>(); m.width = 4; m.height = 1; m.EnsureGrid(); return m; }
        private sealed class Cards : IEventCardSource, IEventParticipantSource
        {
            public int charm = 5;
            public bool TryGetCard(string id, out CardType type, out int amount) { type = CardType.Chara; amount = 1; return id == "actor/card"; }
            public bool TryGetParameter(IReadOnlyList<string> ids, EventParameter p, out int value) { value = ids.Contains("actor/card") ? charm : 0; return true; }
            public IEnumerable<string> Participants(IReadOnlyList<string> ids) => ids.Contains("actor/card") ? new[] { "actor" } : Array.Empty<string>();
            public bool TryCharacter(string id, EventParameter p, out int value) { value = charm; return id == "actor"; }
            public IEnumerable<string> Characters(CharaData d) => new[] { "actor" };
            public string FindCard(Object target) => target != null ? "actor/card" : null;
        }
        private static void Resolve(EventManager manager, EventInstance instance, IEventCardSource cards = null)
        {
            using (var runner = new EventRunner(manager, cards, null, new EventEffectExecutor(manager, null, null, null, null), null) { CanPresentNarrative = true })
            {
                for (int i = 0; i < 20 && instance.status != EventStatus.Resolved; i++) { runner.Tick(); runner.Continue(); }
                Assert.AreEqual(EventStatus.Resolved, instance.status, instance.lastError);
            }
        }
        [TearDown] public void Cleanup() { foreach (var a in assets) Object.DestroyImmediate(a); assets.Clear(); }

        [Test] public void Vertical_ConditionMapStartParticipantsPhaseEffectsHistoryFollowUp()
        {
            var d = Definition(); var next = Definition("NEXT"); var flag = Asset<FlagDefinitionSO>(); var hint = Asset<HintData>();
            d.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.Flag, flag = flag, boolValue = false } });
            d.reservation.cardSlots.Add(new CardSlotDefinition { slotType = SlotType.Required });
            d.extension.pointConditions.Add(new PointCondition { query = new EventQuery { source = ConditionSource.World, phase = WorldPhase.Night }, addPoint = 4 });
            d.extension.success.typedEffects.Add(new FlagEffect { flag = flag });
            d.extension.success.typedEffects.Add(new HintEffect { fixedHint = hint });
            d.extension.success.followUps.Add(new EventFollowUp { definition = next, delayPhaseCount = 2 });
            var cards = new Cards(); var map = Map(); var manager = new EventManager(Catalog(d, next), map, cards);
            Assert.IsTrue(manager.CanAppear(d)); var instance = manager.Spawn(d, Vector2Int.zero);
            Assert.AreEqual(instance.instanceId, map.mapGrid[0, 0].eventInstanceId);
            Assert.IsFalse(manager.Reserve(instance.instanceId, new[] { "" }, 0, out _));
            Assert.IsTrue(manager.Reserve(instance.instanceId, new[] { "actor/card" }, 0, out var error), error);
            CollectionAssert.AreEqual(new[] { "actor" }, instance.participantIds);
            manager.AdvancePhase(1); Resolve(manager, instance, cards);
            Assert.IsTrue(manager.Progress.Flag(flag.PersistentId)); Assert.IsFalse(manager.CanAppear(d));
            Assert.AreEqual(3, manager.Progress.HintLevel(hint.PersistentId));
            Assert.AreEqual(instance.instanceId, manager.Progress.history.Single().instanceId);
            Assert.AreEqual("actor", manager.Progress.history.Single().participantIds.Single());
            Assert.AreEqual(1, manager.Progress.followUps.Count);
            manager.AdvancePhase(2); Assert.AreEqual(1, manager.Instances.Count());
            manager.AdvancePhase(3); Assert.AreEqual(2, manager.Instances.Count()); Assert.IsEmpty(manager.Progress.followUps);
        }
        [TestCase(0)] [TestCase(2)] public void Deadline_ExpiresWithoutParticipants(int deadline)
        {
            var d = Definition(); d.extension.deadlinePhaseCount = deadline; d.reservation.cardSlots.Add(new CardSlotDefinition());
            var manager = new EventManager(Catalog(d)); var instance = manager.Spawn(d, Vector2Int.zero);
            if (deadline == 0) { Assert.AreEqual(EventStatus.Ready, instance.status); Assert.IsFalse(manager.Reserve(instance.instanceId, new[] { "" }, 0, out _)); }
            manager.AdvancePhase(deadline); Resolve(manager, instance);
            Assert.AreEqual("EXPIRED", instance.resolvedOutcomeCode); Assert.IsEmpty(manager.Progress.history.Single().participantIds);
        }
        [Test] public void DurationZero_StartAndFailure()
        {
            var d = Definition(); d.extension.durationPhaseCount = 0; d.execution.judge.target = 1;
            var manager = new EventManager(Catalog(d)); var instance = manager.Spawn(d, Vector2Int.zero);
            Assert.IsTrue(manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _)); Resolve(manager, instance);
            Assert.AreEqual(OutcomeType.FAILURE, manager.Progress.history.Single().outcome);
        }
        [Test] public void History_RepeatLatestAnyAllAndEmpty()
        {
            var d = Definition(); d.extension.durationPhaseCount = 0; var manager = new EventManager(Catalog(d));
            var q = new EventQuery { source = ConditionSource.EventHistory, eventDefinition = d, outcome = OutcomeType.SUCCESS };
            foreach (HistoryMode mode in Enum.GetValues(typeof(HistoryMode))) { q.historyMode = mode; Assert.IsFalse(manager.Evaluate(q)); }
            for (int i = 0; i < 2; i++) { d.execution.judge.target = i; var instance = manager.Spawn(d, Vector2Int.zero); manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _); Resolve(manager, instance); }
            Assert.AreEqual(2, manager.Progress.history.Count); Assert.AreNotEqual(manager.Progress.history[0].instanceId, manager.Progress.history[1].instanceId);
            q.historyMode = HistoryMode.Latest; Assert.IsFalse(manager.Evaluate(q)); q.historyMode = HistoryMode.Any; Assert.IsTrue(manager.Evaluate(q)); q.historyMode = HistoryMode.All; Assert.IsFalse(manager.Evaluate(q));
        }
        [Test] public void HistoryParticipant_UsesCurrentAbility()
        {
            var d = Definition(); var cards = new Cards(); var manager = new EventManager(Catalog(d), null, cards);
            manager.Progress.history.Add(new EventHistoryRecord { definitionId = d.PersistentId, instanceId = "old", resolved = true, participantIds = new List<string> { "actor" } });
            var q = new EventQuery { source = ConditionSource.EventParticipant, eventDefinition = d, comparison = Comparison.GreaterOrEqual, value = 5 };
            Assert.IsTrue(manager.Evaluate(q)); cards.charm = 1; Assert.IsFalse(manager.Evaluate(q));
        }
        [TestCase(false, 1)] [TestCase(true, 2)] public void FollowUp_PendingDuplicates(bool concurrent, int expected)
        {
            var d = Definition(); var target = Definition("TARGET"); d.extension.durationPhaseCount = 0;
            for (int i = 0; i < 2; i++) d.extension.success.followUps.Add(new EventFollowUp { definition = target, delayPhaseCount = 1, allowConcurrent = concurrent });
            var manager = new EventManager(Catalog(d, target), Map()); var instance = manager.Spawn(d, Vector2Int.zero);
            manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _); Resolve(manager, instance);
            Assert.AreEqual(expected, manager.Progress.followUps.Count); manager.AdvancePhase(1);
            Assert.AreEqual(expected, manager.Instances.Count(i => i.definitionId == target.PersistentId));
        }
        [TestCase(false, 1)] [TestCase(true, 2)] public void FollowUp_ActiveDuplicatesAndZeroDelay(bool concurrent, int expected)
        {
            var d = Definition(); var target = Definition("TARGET"); d.extension.durationPhaseCount = 0;
            d.extension.success.followUps.Add(new EventFollowUp { definition = target, allowConcurrent = concurrent });
            var manager = new EventManager(Catalog(d, target), Map()); manager.Spawn(target, Vector2Int.right);
            var instance = manager.Spawn(d, Vector2Int.zero); manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _); Resolve(manager, instance);
            Assert.AreEqual(expected, manager.Instances.Count(i => i.definitionId == target.PersistentId));
        }
        [Test] public void RandomHint_NoReplacementResetAndUpgradeOnly()
        {
            var a = Asset<HintData>(); var b = Asset<HintData>(); var pool = Asset<HintPoolSO>(); pool.hints.AddRange(new[] { a, b });
            var progress = new EventProgress(); var first = progress.Draw(pool, 1); var second = progress.Draw(pool, 1);
            Assert.AreNotEqual(first, second); progress.GrantHint(first.PersistentId, 2); progress.GrantHint(second.PersistentId, 1);
            progress.GrantHint(first.PersistentId, 1); Assert.AreEqual(2, progress.HintLevel(first.PersistentId));
            progress.GrantHint(first.PersistentId, 3); Assert.AreEqual(3, progress.HintLevel(first.PersistentId));
            Assert.IsNotNull(progress.Draw(pool, 1)); Assert.AreEqual(1, progress.draws.Single().drawn.Count); Assert.AreEqual(2, progress.hints.Count);
        }
        [Test] public void DelayedFollowUp_DoesNotCarryImmediateCycleAncestors()
        {
            var d = Definition(); var next = Definition("NEXT"); d.extension.durationPhaseCount = 0;
            d.extension.success.followUps.Add(new EventFollowUp { definition = next, delayPhaseCount = 1 });
            var manager = new EventManager(Catalog(d, next)); var instance = manager.Spawn(d, Vector2Int.zero); instance.immediateAncestors.Add("older");
            manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _); Resolve(manager, instance); manager.AdvancePhase(1);
            Assert.IsEmpty(manager.Instances.Single(i => i.definitionId == next.PersistentId).immediateAncestors);
        }
        [Test] public void Involved_IsAutomaticAndCannotBeReplaced()
        {
            var d = Definition(); d.reservation.cardSlots.Add(new CardSlotDefinition { slotType = SlotType.Involved, involvedTarget = Asset<CharaData>() });
            var manager = new EventManager(Catalog(d), null, new Cards()); var instance = manager.Spawn(d, Vector2Int.zero);
            Assert.IsFalse(manager.Reserve(instance.instanceId, new[] { "other" }, 0, out _));
            Assert.IsTrue(manager.Reserve(instance.instanceId, new[] { "" }, 0, out _)); Assert.AreEqual("actor", instance.participantIds.Single());
        }
        [Test] public void SaveRoundTrip_AllAddedStateAndLegacyVersion()
        {
            var d = Definition(); var catalog = Catalog(d); var manager = new EventManager(catalog);
            var flag = Asset<FlagDefinitionSO>(); var hint = Asset<HintData>(); var pool = Asset<HintPoolSO>(); pool.hints.Add(hint);
            manager.Progress.SetFlag(flag.PersistentId, true); manager.Progress.GrantHint(hint.PersistentId, 3); manager.Progress.Draw(pool, 0);
            manager.Progress.history.Add(new EventHistoryRecord { definitionId = d.PersistentId, instanceId = "history", resolved = true, participantIds = new List<string> { "actor" } });
            manager.Progress.followUps.Add(new ScheduledFollowUp { id = "pending", definitionId = d.PersistentId, duePhase = 3 });
            var instance = manager.Spawn(d, Vector2Int.zero);
            var save = JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(manager.CreateSaveData()));
            var restored = new EventManager(catalog); Assert.IsEmpty(restored.Restore(save));
            Assert.IsTrue(restored.Progress.Flag(flag.PersistentId)); Assert.AreEqual(3, restored.Progress.HintLevel(hint.PersistentId));
            Assert.AreEqual(1, restored.Progress.draws.Single().drawn.Count); Assert.AreEqual("actor", restored.Progress.history.Single().participantIds.Single());
            Assert.AreEqual(1, restored.Progress.followUps.Count); Assert.AreEqual(instance.deadlinePhase, restored.Instances.Single().deadlinePhase);
            manager.Progress.SetFlag(flag.PersistentId, false); Assert.IsTrue(restored.Progress.Flag(flag.PersistentId));
            Assert.IsEmpty(restored.Restore(new EventSaveData())); Assert.IsEmpty(restored.Progress.history);
        }
        [Test] public void Validator_AcceptsLegalZeroAndRejectsMissingTargets()
        {
            var d = Definition(); d.extension.deadlinePhaseCount = d.extension.durationPhaseCount = 0; var catalog = Catalog(d);
            Assert.IsFalse(EventValidator.Validate(new[] { d }, catalog).Any(i => i.severity == EventIssueSeverity.Error));
            d.extension.success.typedEffects.Add(new HintEffect()); d.extension.success.typedEffects.Add(new FlagEffect());
            d.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.Facility } });
            Assert.GreaterOrEqual(EventValidator.Validate(new[] { d }, catalog).Count(i => i.severity == EventIssueSeverity.Error), 3);
        }

        [Test] public void SaveDuringEffects_ResumesWithoutDoubleApplication()
        {
            var d = Definition(); d.extension.durationPhaseCount = 0; var facility = Asset<FacilityDefinitionSO>();
            d.extension.success.typedEffects.Add(new RelationEffect { facility = facility, amount = 2 });
            d.extension.success.typedEffects.Add(new RelationEffect { facility = facility, amount = 3 });
            var manager = new EventManager(Catalog(d)); var instance = manager.Spawn(d, Vector2Int.zero); manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _);
            using (var runner = new EventRunner(manager, null, null, new EventEffectExecutor(manager, null, null, null, null), null) { CanPresentNarrative = true })
            { runner.Pump(); runner.Continue(); runner.Continue(); Assert.AreEqual(1, instance.appliedEffectCount); }
            var save = JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(manager.CreateSaveData()));
            var restored = new EventManager(manager.Catalog); Assert.IsEmpty(restored.Restore(save)); var resumed = restored.Instances.Single(); Resolve(restored, resumed);
            Assert.AreEqual(5, restored.Progress.Relation(facility.PersistentId)); Assert.AreEqual(1, restored.Progress.history.Count);
        }
        [Test] public void InvalidProgressSave_PreservesCurrentState()
        {
            var d = Definition(); var manager = new EventManager(Catalog(d)); var instance = manager.Spawn(d, Vector2Int.zero);
            var save = manager.CreateSaveData(); save.progress.flags = null;
            Assert.IsNotEmpty(manager.Restore(save)); Assert.AreSame(instance, manager.Instances.Single());
        }
        [Test] public void SampleIdentities_ArePersistedAndSurviveReimport()
        {
            MusicMap.Events.Editor.EventExpansionSampleCreator.Create();
            foreach (var path in System.IO.Directory.GetFiles(MusicMap.Events.Editor.EventExpansionSampleCreator.Folder, "*.asset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path.Replace('\\', '/'));
                string id = asset is EventDefinitionSO d ? d.PersistentId : (asset as EventIdentitySO)?.PersistentId;
                if (id == null) continue;
                Assert.IsTrue(Guid.TryParse(id, out _), path);
                StringAssert.Contains("persistentId: " + id, System.IO.File.ReadAllText(path), path);
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                var property = new SerializedObject(asset).FindProperty("persistentId"); Assert.AreEqual(id, property.stringValue);
            }
            var fresh = ScriptableObject.CreateInstance<FlagDefinitionSO>(); assets.Add(fresh); Assert.IsTrue(Guid.TryParse(fresh.PersistentId, out _));
        }
        [Test] public void MixedEffects_GoldItemRandomHintRelationAndFlag()
        {
            var d = Definition(); d.extension.durationPhaseCount = 0;
            var hint = Asset<HintData>(); var pool = Asset<HintPoolSO>(); pool.hints.Add(hint); d.extension.randomHintPool = pool;
            var item = Asset<EquipData>(); var facility = Asset<FacilityDefinitionSO>(); var flag = Asset<FlagDefinitionSO>();
            d.extension.success.typedEffects.AddRange(new OutcomeEffect[] { new GoldEffect { amount = 5 }, new ItemEffect { item = item }, new HintEffect { isFixedHint = false }, new RelationEffect { facility = facility, amount = 3 }, new FlagEffect { flag = flag } });
            var go = new GameObject("Effect test party"); assets.Add(go); var party = go.AddComponent<PartyManager>(); typeof(PartyManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(party, null);
            var manager = new EventManager(Catalog(d)); var instance = manager.Spawn(d, Vector2Int.zero); manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _);
            using (var runner = new EventRunner(manager, null, null, new EventEffectExecutor(manager, party, null, null, null), null) { CanPresentNarrative = true })
                for (int i = 0; i < 20 && instance.status != EventStatus.Resolved; i++) { runner.Tick(); runner.Continue(); }
            Assert.AreEqual(EventStatus.Resolved, instance.status); Assert.AreEqual(5, party.Runtime.Gold); Assert.Contains(item, party.Runtime.Equips); Assert.Contains(hint, party.Runtime.Hints);
            Assert.IsTrue(manager.Evaluate(new EventQuery { source = ConditionSource.Facility, facility = facility, comparison = Comparison.GreaterOrEqual, value = 3 }));
            Assert.IsTrue(manager.Evaluate(new EventQuery { source = ConditionSource.Hint, hint = hint })); Assert.IsTrue(manager.Evaluate(new EventQuery { source = ConditionSource.Flag, flag = flag }));
        }
        [Test] public void OptionalChoiceAndFacilityCards_KeepIndependentTypes()
        {
            var d = Definition(); d.reservation.cardSlots.Add(new CardSlotDefinition { slotType = SlotType.Optional }); d.reservation.cardSlots.Add(new CardSlotDefinition { slotType = SlotType.Choice, acceptedType = CardType.Facility });
            var cardObject = new GameObject("Facility", typeof(RectTransform)); assets.Add(cardObject); var card = cardObject.AddComponent<CardDrag>(); card.Type = CardType.Facility; card.BindEventCard("facility/1");
            var slotObject = new GameObject("Slot", typeof(RectTransform)); assets.Add(slotObject); var slot = slotObject.AddComponent<CardSlot>(); slot.ConfigureForEvent(CardType.Facility, 1);
            Assert.IsTrue(slot.CanAccept(card));
            var manager = new EventManager(Catalog(d), null, new EventCardRegistry(null)); var instance = manager.Spawn(d, Vector2Int.zero);
            Assert.IsTrue(manager.Reserve(instance.instanceId, new[] { "", "facility/1" }, 0, out var error), error);
            Assert.AreEqual("facility/1", instance.assignedCardIds[1]); Assert.IsEmpty(instance.participantIds);
        }
        [UnityTest] public IEnumerator Sample_RealStartButtonAndDayNightIntegration()
        {
            MusicMap.Events.Editor.EventExpansionSampleCreator.Create();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            VerifySampleInPlayMode();
            yield return new ExitPlayMode();
        }
        private static void VerifySampleInPlayMode()
        {
            const string folder = MusicMap.Events.Editor.EventExpansionSampleCreator.Folder;
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(folder + "/SampleCatalog.asset");
            Assert.NotNull(catalog); var d = catalog.definitions.Single();
            Assert.IsFalse(EventValidator.Validate(new[] { d }, catalog).Any(i => i.severity == EventIssueSeverity.Error));
            Assert.AreEqual(2, d.extension.success.typedEffects.Count);
            var host = new GameObject("Event vertical test");
            var map = ScriptableObject.CreateInstance<MapDataSO>(); map.width = 2; map.height = 1; map.EnsureGrid();
            var character = ScriptableObject.CreateInstance<CharaData>();
            var cardHome = new GameObject("Card palette", typeof(RectTransform));
            try
            {
                var party = host.AddComponent<PartyManager>(); var member = new CharacterRuntimeData(character); party.AddMember(member);
                var root = host.AddComponent<EventSystemRoot>(); var game = host.GetComponent<GameManager>(); game.enabled = false;
                game.PartyManager = party; game.questMap = map; game.TimeManager.Setup(1, 10);
                root.catalog = catalog; root.uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prehab/EventSystemUI.prefab"); Assert.IsTrue(root.Initialize(map));
                EventMapPlacement.Generate(root.Manager, map, 2, new System.Random(17), root.World);
                var instance = root.Manager.Instances.Single(); Assert.IsTrue(root.MapController.Select(map.mapGrid[instance.cellPosition.x, instance.cellPosition.y]));
                var cardObject = new GameObject("Participant", typeof(RectTransform)); cardObject.transform.SetParent(cardHome.transform, false);
                var card = cardObject.AddComponent<CardDrag>(); card.BindEventCard(member.runtimeId + "/0"); card.parentSlotRect = (RectTransform)cardHome.transform;
                root.reservationView.Slots.Single().OnCardDropped(card);
                root.reservationView.confirmButton.onClick.Invoke();
                Assert.IsFalse(root.Reservation.IsOpen); Assert.AreEqual(EventStatus.Reserved, instance.status); Assert.AreEqual(member.runtimeId, instance.participantIds.Single());
                game.TimeManager.EndPeriod(); Assert.AreEqual(1, root.Manager.CurrentPhase);
                for (int i = 0; i < 10 && instance.status != EventStatus.Resolved; i++) { root.Runner.Tick(); root.executionView.continueButton.onClick.Invoke(); }
                Assert.AreEqual(EventStatus.Resolved, instance.status); Assert.AreEqual(OutcomeType.SUCCESS, root.Manager.Progress.history.Single().outcome);
                var hint = AssetDatabase.LoadAssetAtPath<HintData>(folder + "/SampleHint.asset"); Assert.AreEqual(3, root.Manager.Progress.HintLevel(hint.PersistentId)); Assert.Contains(hint, party.Runtime.Hints);
                var flag = AssetDatabase.LoadAssetAtPath<FlagDefinitionSO>(folder + "/SampleCompleted.asset"); Assert.IsTrue(root.Manager.Progress.Flag(flag.PersistentId));
                Assert.AreEqual(1, root.Manager.Progress.followUps.Count); game.TimeManager.EndPeriod();
                Assert.AreEqual(2, root.Manager.Instances.Count()); Assert.IsEmpty(root.Manager.Progress.followUps);
                Assert.AreEqual(2, root.Manager.CurrentPhase); Assert.AreNotEqual(instance.instanceId, root.Manager.Instances.Last().instanceId);
                var saved = root.CreateSaveData(); party.Runtime.Hints.Clear(); root.Restore(saved);
                Assert.Contains(hint, party.Runtime.Hints); Assert.AreEqual(1, root.Manager.Progress.history.Count);
                Assert.IsTrue(root.PrepareNewMap(map)); Assert.AreEqual(3, root.Manager.Progress.HintLevel(hint.PersistentId)); Assert.AreEqual(1, root.Manager.Progress.history.Count);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(cardHome); Object.DestroyImmediate(map); Object.DestroyImmediate(character); }
        }
    }
}
