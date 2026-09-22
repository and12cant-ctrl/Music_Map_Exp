using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Tests
{
    public sealed class EventPlacementTests
    {
        private readonly List<Object> objects = new List<Object>();
        private T Asset<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); objects.Add(value); return value; }
        private EventDefinitionSO Definition(string code, int required = 0, float weight = 1)
        {
            var value = Asset<EventDefinitionSO>();
            typeof(EventDefinitionSO).GetField("persistentId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(value, Guid.NewGuid().ToString("N"));
            value.eventCode = code;
            value.appearance.requiredPlacements = required;
            value.appearance.weight = weight;
            return value;
        }
        private EventCatalogSO Catalog(params EventDefinitionSO[] definitions)
        { var value = Asset<EventCatalogSO>(); value.definitions.AddRange(definitions); return value; }
        private MapDataSO Map(int width, int height)
        { var map = Asset<MapDataSO>(); map.width = width; map.height = height; map.EnsureGrid(); return map; }
        private BackgroundCategory Background(string name)
        { var bg = Asset<BackgroundCategory>(); bg.bgCategoryName = name; return bg; }
        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(15, 15, 9)]
        [TestCase(20, 20, 16)]
        [TestCase(25, 25, 25)]
        [TestCase(17, 18, 16)]
        [TestCase(18, 7, 8)]
        public void RequiredEventsAreSeparateFromOneRandomEventPerBlock(int width, int height, int blocks)
        {
            var required = Definition("REQUIRED", 3, 10000);
            var random = Definition("RANDOM");
            var map = Map(width, height);
            var manager = new EventManager(Catalog(required, random), map);
            var notified = new List<EventInstance>(); manager.Changed += notified.Add;
            EventMapPlacement.Generate(manager, map, 5, new System.Random(13), null, new[] { new Vector2Int(5, 5) });
            var instances = manager.Instances.ToArray();
            Assert.AreEqual(3, instances.Count(i => i.definitionId == required.PersistentId));
            var randoms = instances.Where(i => i.definitionId == random.PersistentId).ToArray();
            Assert.AreEqual(blocks, randoms.Length);
            Assert.AreEqual(blocks, randoms.Select(i => new Vector2Int(i.cellPosition.x / 5, i.cellPosition.y / 5)).Distinct().Count());
            Assert.AreEqual(instances.Length, instances.Select(i => i.cellPosition).Distinct().Count());
            Assert.AreEqual(instances.Length, notified.Count);
            Assert.That(notified.Take(3).All(i => i.definitionId == required.PersistentId));
            foreach (var instance in instances)
            {
                Assert.That(instance.cellPosition.x, Is.InRange(0, width - 1));
                Assert.That(instance.cellPosition.y, Is.InRange(0, height - 1));
                Assert.AreNotEqual(new Vector2Int(5, 5), instance.cellPosition);
                Assert.AreEqual(instance.instanceId, map.mapGrid[instance.cellPosition.x, instance.cellPosition.y].eventInstanceId);
                Assert.IsTrue(manager.TryGet(instance.instanceId, out var found)); Assert.AreSame(instance, found);
            }
        }

        [Test] public void RequiredMatchingPreservesScarceBackgroundCells()
        {
            var broad = Definition("A_BROAD", 1);
            var restricted = Definition("B_RESTRICTED", 1);
            restricted.appearance.backgroundCategories.Add("Forest");
            var map = Map(2, 1); map.mapGrid[0, 0].bgCategory = Background("Forest");
            map.mapGrid[1, 0].bgCategory = Background("Desert");
            var manager = new EventManager(Catalog(broad, restricted), map);
            LogAssert.Expect(LogType.Warning, new Regex("Skipped random block"));
            EventMapPlacement.Generate(manager, map, 5, new System.Random(1), null);
            Assert.AreEqual(2, manager.Instances.Count());
            Assert.AreEqual(Vector2Int.zero, manager.Instances.Single(i => i.definitionId == restricted.PersistentId).cellPosition);
        }

        [Test] public void FullMapPreservesExistingNewAndLegacyEventsAndReportsRequiredFailure()
        {
            var random = Definition("RANDOM"); var required = Definition("REQUIRED", 1);
            var map = Map(2, 1); var manager = new EventManager(Catalog(random, required), map);
            var previous = manager.Spawn(random, Vector2Int.zero);
            var legacy = map.mapGrid[1, 0].EvData = new EventData { id = "legacy" };
            LogAssert.Expect(LogType.Error, new Regex("Required event 'REQUIRED'.*0/1"));
            LogAssert.Expect(LogType.Warning, new Regex("Skipped random block"));
            EventMapPlacement.Generate(manager, map, 5, new System.Random(2), null);
            Assert.AreEqual(1, manager.Instances.Count());
            Assert.AreEqual(previous.instanceId, map.mapGrid[0, 0].eventInstanceId);
            Assert.AreSame(legacy, map.mapGrid[1, 0].EvData);
        }

        [Test] public void RandomDefinitionIsSelectedOnlyWhenItHasEligibleCells()
        {
            var good = Definition("GOOD"); good.appearance.backgroundCategories.Add("Forest");
            var impossible = Definition("IMPOSSIBLE", 0, 10000); impossible.appearance.backgroundCategories.Add("Water");
            var off = Definition("OFF"); off.appearance.generateOnMap = false;
            var locked = Definition("LOCKED"); locked.appearance.requiresUnlock = true;
            var condition = Definition("CONDITION"); condition.appearance.conditions.Add(new EventCondition { kind = EventConditionKind.FlagEquals, key = "missing" });
            var zero = Definition("ZERO", 0, 0);
            var map = Map(5, 5); map.mapGrid[4, 3].bgCategory = Background("Forest");
            var manager = new EventManager(Catalog(good, impossible, off, locked, condition, zero), map);
            EventMapPlacement.Generate(manager, map, 5, new System.Random(5), null);
            var instance = manager.Instances.Single();
            Assert.AreEqual(good.PersistentId, instance.definitionId);
            Assert.AreEqual(new Vector2Int(4, 3), instance.cellPosition);
        }

        [Test] public void WeightsAndSeedAffectDefinitionAndCellWithoutOriginBias()
        {
            var light = Definition("LIGHT", 0, 1); var heavy = Definition("HEAVY", 0, 9);
            var catalog = Catalog(light, heavy); int heavyCount = 0;
            var positions = new HashSet<Vector2Int>();
            for (int seed = 0; seed < 100; seed++)
            {
                var map = Map(5, 5); var manager = new EventManager(catalog, map);
                EventMapPlacement.Generate(manager, map, 5, new System.Random(seed), null);
                var instance = manager.Instances.Single(); positions.Add(instance.cellPosition);
                if (instance.definitionId == heavy.PersistentId) heavyCount++;
            }
            Assert.That(heavyCount, Is.InRange(75, 99));
            Assert.Greater(positions.Count, 15);
        }

        [Test] public void NewMapResetClearsRegistryLegacyCellsQueueAndReservationsTogether()
        {
            var definition = Definition("RESET"); var map = Map(5, 5);
            var go = new GameObject("ResetTest"); objects.Add(go);
            var root = go.AddComponent<EventSystemRoot>(); root.catalog = Catalog(definition);
            go.GetComponent<GameManager>().questMap = map;
            Assert.IsTrue(root.PrepareNewMap(map));
            var previous = root.Manager.Spawn(definition, Vector2Int.zero);
            Assert.IsTrue(root.Manager.Reserve(previous.instanceId, new string[0], 0, out var error), error);
            map.mapGrid[1, 0].EvData = new EventData { id = "stale" };
            map.mapGrid[1, 0].eventId = "stale";
            Assert.IsTrue(root.PrepareNewMap(map));
            Assert.IsEmpty(root.Manager.Instances); Assert.IsFalse(root.Runner.HasQueuedEvents);
            Assert.IsFalse(root.Reservation.IsOpen);
            foreach (var cell in map.mapGrid)
            { Assert.IsNull(cell.EvData); Assert.IsTrue(string.IsNullOrEmpty(cell.eventInstanceId)); Assert.IsNull(cell.eventId); }
            EventMapPlacement.Generate(root.Manager, map, 5, new System.Random(2), root.World);
            Assert.AreEqual(1, root.Manager.Instances.Count());
            Assert.IsFalse(root.Manager.TryGet(previous.instanceId, out _));
        }

        [Test] public void GeneratorPreservesRectangularBackgroundsAndNeverMutatesLegacyCategories()
        {
            var map = Map(17, 18); var definition = Definition("NEW");
            var requiredBg = Asset<BGCategoryListSO>(); requiredBg.bGCategoryList.Add(Background("Required"));
            var randomBg = Asset<BGCategoryListSO>(); randomBg.bGCategoryList.Add(Background("Random"));
            var legacyCategory = Asset<EventCategory>(); var legacy = new EventData { id = "keep" }; legacyCategory.evDatas.Add(legacy);
            var legacyList = Asset<EventListSO>(); legacyList.evCategoryList.Add(legacyCategory);
            var go = new GameObject("GeneratorTest"); objects.Add(go);
            var root = go.AddComponent<EventSystemRoot>(); root.catalog = Catalog(definition);
            go.GetComponent<GameManager>().questMap = map;
            var generator = go.AddComponent<MapGenerator>();
            generator.MapGenerate(map, requiredBg, randomBg, legacyList, legacyList, 5);
            Assert.AreEqual(16, root.Manager.Instances.Count());
            Assert.AreSame(legacy, legacyCategory.evDatas.Single());
            foreach (var cell in map.mapGrid) { Assert.NotNull(cell.bgCategory); Assert.AreEqual(cell.bgCategory.bgCategoryName, cell.backgroundId); Assert.IsNull(cell.EvData); }
            Assert.That(map.mapGrid.Cast<MapCell>().Any(c => c.bgCategory == requiredBg.bGCategoryList[0]));
            var ids = root.Manager.Instances.Select(i => i.instanceId).ToArray();
            generator.MapGenerate(map, requiredBg, randomBg, null, null, 5);
            Assert.AreEqual(16, root.Manager.Instances.Count());
            Assert.That(ids.All(id => !root.Manager.TryGet(id, out _)));
        }

        [Test] public void MainSceneConnectsOneRootToExistingCatalogAndCompleteUiPrefab()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/メインバトル.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<EventSystemRoot>(true)).ToArray();
                Assert.AreEqual(1, roots.Length); var root = roots[0];
                Assert.AreEqual("Main", root.name);
                Assert.AreEqual("Assets/イベント/イベントSO/イベントカタログ.asset", AssetDatabase.GetAssetPath(root.catalog));
                Assert.NotNull(root.GetComponent<MapGenerator>());
                Assert.NotNull(root.GetComponent<MiniMapBuilder>());
                Assert.AreEqual("Assets/チュートリアル.asset", AssetDatabase.GetAssetPath(root.GetComponent<GameManager>().questMap));
                Assert.IsTrue(root.reserveBaseCell);
                var reservation = root.uiPrefab.GetComponentInChildren<ReservationView>(true);
                var execution = root.uiPrefab.GetComponentInChildren<ExecutionView>(true);
                Assert.NotNull(reservation.panel); Assert.NotNull(reservation.confirmButton); Assert.NotNull(reservation.slotPrefab);
                Assert.NotNull(execution.panel); Assert.NotNull(execution.continueButton);
                Assert.NotNull(root.uiPrefab.GetComponentInChildren<EventScheduleView>(true));
                Assert.IsNotEmpty(root.GetComponentsInChildren<HakenTab>(true));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
