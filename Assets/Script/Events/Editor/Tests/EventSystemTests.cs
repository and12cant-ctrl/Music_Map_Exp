using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Tests
{
    public sealed class EventSystemTests
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();
        private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); objects.Add(asset); return asset; }
        private GameObject SceneObject(string name) { var go = new GameObject(name); objects.Add(go); return go; }
        private EventDefinitionSO Definition(string code = "TEST", bool immediate = false)
        {
            var definition = Asset<EventDefinitionSO>();
            typeof(EventDefinitionSO).GetField("persistentId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, Guid.NewGuid().ToString("N"));
            definition.eventCode = code;
            definition.displayName = code;
            definition.reservation.triggerMode = immediate ? EventTriggerMode.Immediate : EventTriggerMode.Scheduled;
            definition.reservation.requiredPhaseCount = immediate ? 0 : 2;
            return definition;
        }
        private EventCatalogSO Catalog(params EventDefinitionSO[] definitions)
        {
            var categoryCatalog = Asset<EventCategoryCatalogSO>();
            Assert.IsTrue(categoryCatalog.EditorTryAdd("Test Category", string.Empty, 0, out var category, out var categoryError), categoryError);
            foreach (var definition in definitions.Where(d => d != null)) SetCategory(definition, category.Id);
            var catalog = Asset<EventCatalogSO>(); catalog.categoryCatalog = categoryCatalog; catalog.definitions.AddRange(definitions); return catalog;
        }
        private static void SetCategory(EventDefinitionSO definition, string categoryId,
            EventCategoryMigrationState state = EventCategoryMigrationState.Migrated)
        {
            typeof(EventDefinitionSO).GetField("categoryId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, categoryId);
            typeof(EventDefinitionSO).GetField("categoryMigrationState", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, state);
        }
        private EventRunner Runner(EventManager manager, EventEffectExecutor effects = null, Func<EventExecutionType, IEventExecutionHandler> handlers = null)
        {
            var runner = new EventRunner(manager, null, null, effects, handlers) { CanPresentNarrative = true };
            disposables.Add(runner); return runner;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var disposable in disposables) disposable.Dispose(); disposables.Clear();
            foreach (var obj in objects) if (obj != null) Object.DestroyImmediate(obj); objects.Clear();
        }

        [Test] public void CatalogResolvesBothIdsAndFilters()
        {
            var definition = Definition(); definition.tags.Add("Town");
            var catalog = Catalog(definition);
            var category = catalog.categoryCatalog.GetCategories().Single();
            Assert.IsEmpty(catalog.Initialize(false));
            Assert.IsTrue(catalog.TryGetById(definition.PersistentId, out var found)); Assert.AreSame(definition, found);
            Assert.IsTrue(catalog.TryGetByCode("test", out found));
            Assert.AreEqual(1, catalog.GetEnabledByCategoryId(category.Id, "town").Count());
            definition.enabled = false; Assert.IsEmpty(catalog.GetEnabled());
        }
        [Test] public void DuplicatePersistentIdIsRejectedRatherThanOverwritten()
        {
            var first = Definition(); var second = Object.Instantiate(first); objects.Add(second); second.eventCode = "OTHER";
            var catalog = Catalog(first, second);
            Assert.That(catalog.Initialize(false).Any(e => e.Contains("duplicate persistentId")));
            Assert.IsFalse(catalog.TryGetById(first.PersistentId, out _));
        }
        [Test] public void DuplicateCodeAndNullAreDetected()
        {
            var catalog = Catalog(Definition(), Definition(), null);
            var errors = catalog.Initialize(false);
            Assert.That(errors.Any(e => e.Contains("duplicate eventCode")));
            Assert.That(errors.Any(e => e.Contains("null")));
            Assert.IsFalse(catalog.TryGetByCode("TEST", out _));
        }
        [Test] public void SpawnCreatesIndependentInstancesAndBindsMapCells()
        {
            var definition = Definition(); var map = Asset<MapDataSO>(); map.width = 2; map.height = 1; map.EnsureGrid();
            var manager = new EventManager(Catalog(definition), map);
            var first = manager.Spawn(definition, Vector2Int.zero); var second = manager.Spawn(definition, Vector2Int.right);
            Assert.AreNotEqual(first.instanceId, second.instanceId);
            Assert.AreEqual(first.definitionId, second.definitionId);
            Assert.AreEqual(first.instanceId, map.mapGrid[0, 0].eventInstanceId);
            Assert.AreEqual(second.instanceId, map.mapGrid[1, 0].eventInstanceId);
        }
        [Test] public void ReservationBecomesReadyOnlyAtDuePhase()
        {
            var definition = Definition(); var manager = new EventManager(Catalog(definition));
            var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsTrue(manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _));
            Assert.AreEqual(2, instance.executePhase);
            manager.AdvancePhase(1); Assert.AreEqual(EventStatus.Reserved, instance.status);
            manager.AdvancePhase(2); Assert.AreEqual(EventStatus.Ready, instance.status);
            manager.AdvancePhase(3); Assert.AreEqual(EventStatus.Ready, instance.status);
        }
        [Test] public void ImmediateNeverEntersReservationList()
        {
            var definition = Definition(immediate: true); var manager = new EventManager(Catalog(definition));
            var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsFalse(manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _));
            Assert.IsTrue(manager.RequestImmediate(instance.instanceId, out _));
            var runner = Runner(manager); runner.Pump();
            Assert.AreEqual(EventStatus.Running, instance.status); Assert.AreEqual(-1, instance.reservedPhase);
            runner.Continue(); runner.Continue(); Assert.AreEqual(EventStatus.Resolved, instance.status);
        }
        [Test] public void QueueRunsOneEventAtATimeInPriorityOrder()
        {
            var low = Definition("LOW", true); var high = Definition("HIGH", true); high.priority = 10;
            var manager = new EventManager(Catalog(low, high)); var runner = Runner(manager);
            var first = manager.Spawn(low, Vector2Int.zero); var second = manager.Spawn(high, Vector2Int.right);
            manager.RequestImmediate(first.instanceId, out _); manager.RequestImmediate(second.instanceId, out _);
            runner.Pump(); Assert.AreSame(second, runner.Current); Assert.AreEqual(EventStatus.Ready, first.status);
            runner.Pump(); Assert.AreSame(second, runner.Current);
            runner.Continue(); runner.Continue(); Assert.IsNull(runner.Current);
            runner.Pump(); Assert.AreSame(first, runner.Current);
        }
        [Test] public void ExecutionDoesNotMutateDefinition()
        {
            var definition = Definition(immediate: true); var catalog = Catalog(definition);
            string before = JsonUtility.ToJson(definition);
            var manager = new EventManager(catalog); var runner = Runner(manager);
            var instance = manager.Spawn(definition, Vector2Int.zero); manager.RequestImmediate(instance.instanceId, out _);
            runner.Pump(); runner.Continue(); runner.Continue();
            Assert.AreEqual(before, JsonUtility.ToJson(definition));
        }
        [Test] public void SaveRoundTripPreservesIdsSeedCardsAndJudgement()
        {
            var definition = Definition(immediate: true); var manager = new EventManager(Catalog(definition)); var runner = Runner(manager);
            var instance = manager.Spawn(definition, new Vector2Int(3, 4)); manager.RequestImmediate(instance.instanceId, out _);
            runner.Pump(); runner.Continue();
            var saved = JsonUtility.FromJson<EventSaveData>(JsonUtility.ToJson(manager.CreateSaveData()));
            Assert.That(!JsonUtility.ToJson(saved).Contains("m_FileID"));
            var restored = new EventManager(manager.Catalog); Assert.IsEmpty(restored.Restore(saved));
            Assert.IsTrue(restored.TryGet(instance.instanceId, out var result));
            Assert.AreEqual(instance.randomSeed, result.randomSeed); Assert.AreEqual(instance.cellPosition, result.cellPosition);
            Assert.AreEqual(instance.resolvedOutcomeCode, result.resolvedOutcomeCode); Assert.AreEqual(instance.judgeTotal, result.judgeTotal);
            CollectionAssert.AreEqual(instance.diceResults, result.diceResults);
            var resumed = Runner(restored); resumed.Pump(); resumed.Continue();
            Assert.AreEqual(EventStatus.Resolved, result.status); Assert.AreEqual(instance.judgeTotal, result.judgeTotal);
        }
        [Test] public void SaveSnapshotIsDetachedFromLiveInstance()
        {
            var definition = Definition(); var manager = new EventManager(Catalog(definition)); var instance = manager.Spawn(definition, Vector2Int.zero);
            var save = manager.CreateSaveData(); save.instances[0].assignedCardIds.Add("different"); save.instances[0].status = EventStatus.Cancelled;
            Assert.IsEmpty(instance.assignedCardIds); Assert.AreEqual(EventStatus.Available, instance.status);
        }
        [Test] public void MissingDefinitionDisablesOnlyAffectedInstance()
        {
            var definition = Definition(); var manager = new EventManager(Catalog(definition)); var instance = manager.Spawn(definition, Vector2Int.zero);
            var save = manager.CreateSaveData(); save.instances[0].definitionId = "missing";
            LogAssert.Expect(LogType.Error, new Regex("Missing definitionId"));
            Assert.AreEqual(1, manager.Restore(save).Count);
            Assert.IsTrue(manager.TryGet(instance.instanceId, out var restored)); Assert.AreEqual(EventStatus.Cancelled, restored.status);
            Assert.That(restored.lastError.Contains("missing"));
        }
        [Test] public void CancelReleasesCardAndCell()
        {
            var definition = Definition(); definition.reservation.cardSlots.Add(new CardSlotDefinition());
            var map = Asset<MapDataSO>(); map.width = 1; map.height = 1; map.EnsureGrid();
            var manager = new EventManager(Catalog(definition), map, new Cards()); var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsTrue(manager.Reserve(instance.instanceId, new[] { "CARD" }, 0, out _)); Assert.IsTrue(manager.IsCardAssigned("CARD"));
            Assert.IsTrue(manager.CancelReservation(instance.instanceId, out _)); Assert.IsFalse(manager.IsCardAssigned("CARD"));
            Assert.IsTrue(string.IsNullOrEmpty(map.mapGrid[0, 0].eventInstanceId));
        }
        [Test] public void MissingWrongAndDoubleBookedCardsAreRejected()
        {
            var definition = Definition(); definition.reservation.cardSlots.Add(new CardSlotDefinition());
            var manager = new EventManager(Catalog(definition), cards: new Cards()); var first = manager.Spawn(definition, Vector2Int.zero); var second = manager.Spawn(definition, Vector2Int.one);
            Assert.IsFalse(manager.Reserve(first.instanceId, new[] { "MISSING" }, 0, out _));
            Assert.IsTrue(manager.Reserve(first.instanceId, new[] { "CARD" }, 0, out _));
            Assert.IsFalse(manager.Reserve(second.instanceId, new[] { "CARD" }, 0, out _));
            Assert.AreEqual(EventStatus.Available, second.status);
        }
        [Test] public void ExistingTimeManagerDayNightNotifiesDueEvents()
        {
            var time = SceneObject("Time").AddComponent<TimeManager>(); typeof(TimeManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(time, null); time.Setup(1, 7);
            var definition = Definition(); var manager = new EventManager(Catalog(definition)); var instance = manager.Spawn(definition, Vector2Int.zero);
            manager.Reserve(instance.instanceId, Array.Empty<string>(), 0, out _);
            time.OnPeriodStarted += period => manager.AdvancePhase((time.Runtime.CurrentDate - 1) * 2 + (period == TimePeriod.Night ? 1 : 0));
            time.ConsumeTime(TimeManager.PeriodTime); Assert.AreEqual(EventStatus.Reserved, instance.status);
            time.ConsumeTime(TimeManager.PeriodTime); Assert.AreEqual(EventStatus.Ready, instance.status);
        }
        [Test] public void ExternalHandlerKeepsQueueBlockedUntilCompletion()
        {
            var definition = Definition(immediate: true); definition.execution.executionType = EventExecutionType.Shop;
            var manager = new EventManager(Catalog(definition)); var handler = new Handler(); var runner = Runner(manager, handlers: _ => handler);
            var first = manager.Spawn(definition, Vector2Int.zero); var second = manager.Spawn(definition, Vector2Int.one);
            manager.RequestImmediate(first.instanceId, out _); manager.RequestImmediate(second.instanceId, out _);
            runner.Pump(); runner.Pump(); Assert.AreEqual(1, handler.starts);
            handler.complete(true, "SUCCESS"); runner.Pump(); Assert.AreEqual(2, handler.starts);
        }
        [Test] public void MissingExternalHandlerIsRetryable()
        {
            var definition = Definition(immediate: true); definition.execution.executionType = EventExecutionType.Encounter;
            var manager = new EventManager(Catalog(definition)); var runner = Runner(manager); var instance = manager.Spawn(definition, Vector2Int.zero);
            manager.RequestImmediate(instance.instanceId, out _);
            LogAssert.Expect(LogType.Error, new Regex("Encounter handler is not connected")); runner.Pump();
            Assert.IsNull(runner.Current); Assert.AreEqual(EventStatus.Ready, instance.status); runner.Pump();
        }
        [Test] public void PartialResultJournalPreventsRepeatedGoldAfterRestore()
        {
            var definition = Definition(immediate: true);
            definition.outcomes = new List<EventOutcomeDefinition> { new EventOutcomeDefinition { outcomeCode = "DONE", isDefault = true, effects = new List<EventEffect>
            { new EventEffect { kind = EventEffectKind.GrantGold, amount = 10 }, new EventEffect { kind = EventEffectKind.ConsumeGold, amount = 20 } } } };
            var party = SceneObject("Party").AddComponent<PartyManager>(); typeof(PartyManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(party, null);
            var manager = new EventManager(Catalog(definition));
            var effects = new EventEffectExecutor(manager, party, new EventCardRegistry(party), null, null);
            var runner = Runner(manager, effects); var instance = manager.Spawn(definition, Vector2Int.zero);
            manager.RequestImmediate(instance.instanceId, out _); runner.Pump(); runner.Continue(); runner.Continue();
            Assert.AreEqual(10, party.Runtime.Gold); Assert.AreEqual(1, instance.appliedEffectCount);
            LogAssert.Expect(LogType.Error, new Regex("Insufficient gold")); runner.Tick();
            var save = manager.CreateSaveData(); Assert.IsEmpty(manager.Restore(save));
            party.AddGold(10); runner.Pump(); runner.Continue(); runner.Tick();
            Assert.AreEqual(0, party.Runtime.Gold); Assert.That(manager.Instances.Single().status == EventStatus.Resolved);
        }
        [Test] public void ImmediateCycleIsValidatedAndBlockedAtRuntime()
        {
            var definition = Definition(immediate: true);
            definition.outcomes[1].followUps.Add(new EventFollowUp { definition = definition, startMode = EventFollowUpMode.Reserve });
            var catalog = Catalog(definition);
            Assert.That(EventValidator.Validate(new[] { definition }, catalog).Any(i => i.message.Contains("Dangerous") && i.severity == EventIssueSeverity.Error));
            var manager = new EventManager(catalog); var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsFalse(manager.StartFollowUp(instance, definition, EventFollowUpMode.Reserve, out var error)); Assert.That(error.Contains("cycle"));
        }
        [Test] public void MapPlacementPreservesRectangularBackgroundLayoutAndUsesCatalog()
        {
            var definition = Definition(); var catalog = Catalog(definition);
            var map = Asset<MapDataSO>(); map.width = 6; map.height = 3; map.EnsureGrid();
            var background = Asset<BackgroundCategory>(); background.bgCategoryName = "Forest";
            var manager = new EventManager(catalog, map);
            foreach (var cell in map.mapGrid) cell.bgCategory = background;
            EventMapPlacement.Generate(manager, map, 3, new System.Random(1), null);
            Assert.AreEqual(2, manager.Instances.Count());
            foreach (var cell in map.mapGrid) Assert.AreSame(background, cell.bgCategory);
            foreach (var instance in manager.Instances) Assert.AreEqual(instance.instanceId, map.mapGrid[instance.cellPosition.x, instance.cellPosition.y].eventInstanceId);
        }

        [Test] public void FollowUpCanReplaceParentOnFullMapAndSurviveRestore()
        {
            var parentDefinition = Definition("PARENT", true); var childDefinition = Definition("CHILD");
            var map = Asset<MapDataSO>(); map.width = 1; map.height = 1; map.EnsureGrid();
            var manager = new EventManager(Catalog(parentDefinition, childDefinition), map);
            var parent = manager.Spawn(parentDefinition, Vector2Int.zero); manager.RequestImmediate(parent.instanceId, out _);
            var runner = Runner(manager); runner.Pump();
            Assert.IsTrue(manager.StartFollowUp(parent, childDefinition, EventFollowUpMode.SpawnAvailable, out var error), error);
            var child = manager.Instances.Single(i => i != parent);
            Assert.IsFalse(parent.occupiesCell); Assert.AreEqual(child.instanceId, map.mapGrid[0, 0].eventInstanceId);
            Assert.IsEmpty(manager.Restore(manager.CreateSaveData()));
            Assert.AreEqual(child.instanceId, map.mapGrid[0, 0].eventInstanceId);
        }
        [Test] public void InvalidSaveVersionPreservesCurrentState()
        {
            var definition = Definition(); var manager = new EventManager(Catalog(definition)); var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsNotEmpty(manager.Restore(new EventSaveData { version = 999 }));
            Assert.IsTrue(manager.TryGet(instance.instanceId, out var found)); Assert.AreSame(instance, found);
        }
        [Test] public void UnregisteredReferencesAndConflictingTimingAreValidated()
        {
            var definition = Definition(); definition.reservation.requiredPhaseCount = 0;
            definition.outcomes[0].followUps.Add(new EventFollowUp());
            var issues = EventValidator.Validate(new[] { definition }, Catalog());
            Assert.That(issues.Any(i => i.message.Contains("Scheduled requires")));
            Assert.That(issues.Any(i => i.message.Contains("missing or unregistered")));
            Assert.That(issues.Any(i => i.message.Contains("Not registered") && i.severity == EventIssueSeverity.Warning));
        }
        [Test] public void OptionalCardSlotsAndParameterRequirementsAreChecked()
        {
            var definition = Definition(); definition.reservation.cardSlots.Add(new CardSlotDefinition());
            definition.reservation.cardSlots.Add(new CardSlotDefinition { required = false });
            definition.reservation.requirements.Add(new EventRequirement { parameter = EventParameter.Appeal, minimum = 11 });
            var manager = new EventManager(Catalog(definition), cards: new Cards()); var instance = manager.Spawn(definition, Vector2Int.zero);
            Assert.IsFalse(manager.Reserve(instance.instanceId, new[] { "CARD", "" }, 0, out _));
            definition.reservation.requirements[0].minimum = 10;
            Assert.IsTrue(manager.Reserve(instance.instanceId, new[] { "CARD", "" }, 0, out _));
        }
        [Test] public void ExistingCardSlotAcceptsAndReturnsCharacterButRejectsLockedCard()
        {
            var slotObject = new GameObject("slot", typeof(RectTransform), typeof(CardSlot)); objects.Add(slotObject);
            var origin = new GameObject("origin", typeof(RectTransform)); objects.Add(origin);
            var cardObject = new GameObject("card", typeof(RectTransform), typeof(CardDrag)); objects.Add(cardObject);
            var card = cardObject.GetComponent<CardDrag>(); card.parentSlotRect = (RectTransform)origin.transform;
            var slot = slotObject.GetComponent<CardSlot>(); slot.ConfigureForEvent(CardType.Chara, 1);
            Assert.IsTrue(slot.CanAccept(card)); slot.OnCardDropped(card);
            Assert.AreSame(card, slot.SlotInCard); Assert.AreSame(slot.transform, card.transform.parent);
            slot.ReturnCard(card); Assert.IsNull(slot.SlotInCard); Assert.AreSame(origin.transform, card.transform.parent);
            card.EventLocked = true; Assert.IsFalse(slot.CanAccept(card));
        }
        [Test] public void MoneySlotChecksQuantityEvenWhenLegacyTrueTypeIsCharacter()
        {
            var slotObject = new GameObject("slot", typeof(RectTransform), typeof(CardSlot)); objects.Add(slotObject);
            var cardObject = new GameObject("card", typeof(RectTransform), typeof(CardDrag)); objects.Add(cardObject);
            var card = cardObject.GetComponent<CardDrag>(); card.Type = CardType.Money; card.TrueType = CardType.Chara;
            var slot = slotObject.GetComponent<CardSlot>(); slot.ConfigureForEvent(CardType.Money, 2);
            card._cardNum = 1; Assert.IsFalse(slot.CanAccept(card));
            card._cardNum = 2; Assert.IsTrue(slot.CanAccept(card));
        }

        private sealed class Cards : IEventCardSource
        {
            public bool TryGetCard(string id, out CardType type, out int amount) { type = CardType.Chara; amount = 1; return id == "CARD"; }
            public bool TryGetParameter(IReadOnlyList<string> ids, EventParameter parameter, out int value) { value = 10; return true; }
        }
        private sealed class Handler : IEventExecutionHandler
        {
            public EventExecutionType ExecutionType => EventExecutionType.Shop;
            public int starts;
            public Action<bool, string> complete;
            public bool TryBegin(EventDefinitionSO definition, EventInstance instance, string externalKey, string operationId, Action<bool, string> completion, out string error)
            { starts++; complete = completion; error = null; return true; }
        }
    }
}
