using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MusicMap.Events.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MusicMap.Events.Tests
{
    public sealed class EventCategoryTests
    {
        private readonly List<Object> objects = new List<Object>();
        private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); objects.Add(asset); return asset; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test]
        public void SharedCategoryRenameKeepsIdsReferencesAndFiltering()
        {
            var categories = CategoryCatalog("  Main  ", out var category);
            var first = Definition("FIRST");
            var second = Definition("SECOND");
            SetCategory(first, category.Id);
            SetCategory(second, category.Id);
            var catalog = Catalog(categories, first, second);

            Assert.AreEqual("Main", category.DisplayName);
            Assert.AreEqual(2, catalog.GetEnabledByCategoryId(category.Id).Count());
            Assert.IsTrue(categories.EditorTryUpdate(category.Id, "メイン", "renamed", 5, false, out var error), error);
            Assert.AreEqual(category.Id, first.CategoryId);
            Assert.AreEqual(category.Id, second.CategoryId);
            Assert.AreEqual(2, catalog.GetEnabledByCategoryId(category.Id).Count());
            Assert.AreEqual("メイン", categories.GetCategories().Single().DisplayName);
        }

        [Test]
        public void CatalogDetectsEmptyDuplicateAndNonGuidIdsAndNormalizedNames()
        {
            var catalog = Asset<EventCategoryCatalogSO>();
            string duplicateId = Guid.NewGuid().ToString("N");
            SetRawCategories(catalog,
                Raw(string.Empty, ""),
                Raw(duplicateId, "Main"),
                Raw(duplicateId, "ｍａｉｎ"),
                Raw("not-a-guid", "Other"));

            var errors = catalog.Initialize(false);
            Assert.That(errors.Any(e => e.Contains("empty id")));
            Assert.That(errors.Any(e => e.Contains("empty displayName")));
            Assert.That(errors.Any(e => e.Contains("Duplicate category id")));
            Assert.That(errors.Any(e => e.Contains("Duplicate category displayName")));
            Assert.That(errors.Any(e => e.Contains("non-GUID")));
            Assert.IsFalse(catalog.TryGetById(duplicateId, out _));
            Assert.IsFalse(catalog.TryGetByNormalizedDisplayName("MAIN", out _));
            Assert.IsFalse(catalog.TryGetByNormalizedDisplayName("Other", out _));
        }

        [Test]
        public void ValidatorDistinguishesUnsetUnknownAndDeprecatedCategories()
        {
            var categories = CategoryCatalog("Known", out var known);
            var definition = Definition("EVENT");
            var catalog = Catalog(categories, definition);
            SetCategory(definition, string.Empty);
            var unset = EventValidator.Validate(new[] { definition }, catalog);
            Assert.That(unset.Any(i => i.message.Contains("has no category") && i.severity == EventIssueSeverity.Error));

            SetCategory(definition, "missing");
            var unknown = EventValidator.Validate(new[] { definition }, catalog);
            Assert.That(unknown.Any(i => i.message.Contains("missing or ambiguous")));

            SetCategory(definition, known.Id);
            bool enabledBefore = definition.enabled;
            Assert.IsTrue(categories.EditorTryUpdate(known.Id, known.DisplayName, known.Description, known.SortOrder, true, out var error), error);
            Assert.IsTrue(categories.TryGetById(known.Id, out var resolved));
            Assert.IsTrue(resolved.IsDeprecated);
            Assert.AreEqual(enabledBefore, definition.enabled);
            Assert.AreEqual(1, catalog.GetEnabledByCategoryId(known.Id).Count());
            Assert.That(EventValidator.Validate(new[] { definition }, catalog).Any(i => i.message.Contains("deprecated") && i.severity == EventIssueSeverity.Warning));
        }

        [Test]
        public void CreateAssignsTrimmedCategoryAtomicallyAndRejectsDuplicates()
        {
            var categories = Asset<EventCategoryCatalogSO>();
            var definition = Definition("CREATE");
            Assert.IsTrue(EventCategoryEditorUtility.TryCreate(categories, "  日本語カテゴリ  ", "説明", 7, definition,
                out var created, out var error), error);
            Assert.AreEqual("日本語カテゴリ", created.DisplayName);
            Assert.AreEqual(created.Id, definition.CategoryId);
            Assert.AreEqual(EventCategoryMigrationState.Migrated, definition.CategoryMigrationState);

            var previousId = definition.CategoryId;
            Assert.IsFalse(EventCategoryEditorUtility.TryCreate(categories, "日本語カテゴリ", "duplicate", 0, definition,
                out _, out error));
            Assert.That(error.Contains("既に登録"));
            Assert.AreEqual(previousId, definition.CategoryId);
            Assert.AreEqual(1, categories.Categories.Count);
        }

        [Test]
        public void MigrationIsExplicitIdempotentAndEmptyLegacyCreatesNothing()
        {
            var categories = CategoryCatalog("Town", out var town);
            var exact = Definition("EXACT", EventCategoryMigrationState.Pending); exact.category = "Town";
            var assessment = EventCategoryMigration.Assess(exact, categories);
            Assert.AreEqual(EventCategoryMigrationMatch.StrictExact, assessment.match);
            Assert.AreSame(town, assessment.candidate);
            Assert.IsTrue(EventCategoryEditorUtility.TryApplyMigration(exact, categories, town.Id, out var error), error);
            int count = categories.Categories.Count;
            Assert.IsTrue(EventCategoryEditorUtility.TryApplyMigration(exact, categories, town.Id, out error), error);
            Assert.AreEqual(count, categories.Categories.Count);
            Assert.AreEqual(town.Id, exact.CategoryId);

            var blank = Definition("BLANK", EventCategoryMigrationState.Pending); blank.category = string.Empty;
            Assert.AreEqual(EventCategoryMigrationMatch.EmptyLegacyValue, EventCategoryMigration.Assess(blank, categories).match);
            Assert.IsTrue(EventCategoryEditorUtility.TryApplyMigration(blank, categories, string.Empty, out error), error);
            Assert.IsTrue(string.IsNullOrEmpty(blank.CategoryId));
            Assert.AreEqual(count, categories.Categories.Count);
        }

        [Test]
        public void NormalizedAndAmbiguousLegacyNamesAreNeverAutoSelected()
        {
            var categories = CategoryCatalog("Main", out _);
            var normalized = Definition("NORMALIZED", EventCategoryMigrationState.Pending); normalized.category = "ｍａｉｎ";
            var normalizedAssessment = EventCategoryMigration.Assess(normalized, categories);
            Assert.AreEqual(EventCategoryMigrationMatch.NormalizedOnly, normalizedAssessment.match);
            Assert.IsTrue(string.IsNullOrEmpty(normalized.CategoryId));

            SetRawCategories(categories, Raw(Guid.NewGuid().ToString("N"), "Same"), Raw(Guid.NewGuid().ToString("N"), "Same"));
            categories.Initialize(false);
            var ambiguous = Definition("AMBIGUOUS", EventCategoryMigrationState.Pending); ambiguous.category = "Same";
            Assert.AreEqual(EventCategoryMigrationMatch.Ambiguous, EventCategoryMigration.Assess(ambiguous, categories).match);
            Assert.IsTrue(string.IsNullOrEmpty(ambiguous.CategoryId));
        }

        [Test]
        public void CategorySelectionDoesNotChangeEventOrInstanceIdentityOrSaveShape()
        {
            var categories = CategoryCatalog("Stable", out var category);
            var definition = Definition("IDENTITY");
            string persistentId = definition.PersistentId;
            SetCategory(definition, category.Id);
            var manager = new EventManager(Catalog(categories, definition));
            var instance = manager.Spawn(definition, Vector2Int.zero);
            var save = manager.CreateSaveData();

            Assert.AreEqual(persistentId, definition.PersistentId);
            Assert.AreEqual(instance.instanceId, save.instances.Single().instanceId);
            Assert.AreEqual(persistentId, save.instances.Single().definitionId);
            Assert.IsNull(typeof(EventSaveData).GetField("categoryId"));
        }

        [Test]
        public void CreateAndAssignUndoRedoKeepsCategoryCacheAndReferenceInSync()
        {
            Undo.ClearAll();
            try
            {
                var categories = Asset<EventCategoryCatalogSO>();
                var definition = Definition("UNDO");
                Assert.IsTrue(EventCategoryEditorUtility.TryCreate(categories, "Undo Category", string.Empty, 0,
                    definition, out var created, out var error), error);
                string id = created.Id;
                Assert.IsTrue(categories.TryGetById(id, out _));
                Assert.AreEqual(id, definition.CategoryId);

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.IsFalse(categories.TryGetById(id, out _));
                Assert.IsTrue(string.IsNullOrEmpty(definition.CategoryId));

                Undo.PerformRedo();
                Assert.IsTrue(categories.TryGetById(id, out _));
                Assert.AreEqual(id, definition.CategoryId);
            }
            finally
            {
                Undo.ClearAll();
            }
        }

        private EventCategoryCatalogSO CategoryCatalog(string name, out EventCategoryDefinition category)
        {
            var catalog = Asset<EventCategoryCatalogSO>();
            Assert.IsTrue(catalog.EditorTryAdd(name, string.Empty, 0, out category, out var error), error);
            return catalog;
        }

        private EventDefinitionSO Definition(string code, EventCategoryMigrationState state = EventCategoryMigrationState.Migrated)
        {
            var definition = Asset<EventDefinitionSO>();
            SetPrivate(definition, "persistentId", Guid.NewGuid().ToString("N"));
            SetCategory(definition, string.Empty, state);
            definition.eventCode = code;
            definition.displayName = code;
            return definition;
        }

        private EventCatalogSO Catalog(EventCategoryCatalogSO categories, params EventDefinitionSO[] definitions)
        {
            var catalog = Asset<EventCatalogSO>();
            catalog.categoryCatalog = categories;
            catalog.definitions.AddRange(definitions);
            return catalog;
        }

        private static void SetCategory(EventDefinitionSO definition, string id,
            EventCategoryMigrationState state = EventCategoryMigrationState.Migrated)
        {
            SetPrivate(definition, "categoryId", id);
            SetPrivate(definition, "categoryMigrationState", state);
        }

        private static EventCategoryDefinition Raw(string id, string name)
        {
            var category = new EventCategoryDefinition();
            SetPrivate(category, "id", id);
            SetPrivate(category, "displayName", name);
            return category;
        }

        private static void SetRawCategories(EventCategoryCatalogSO catalog, params EventCategoryDefinition[] categories)
        {
            SetPrivate(catalog, "categories", categories.ToList());
            catalog.InvalidateCache();
        }

        private static void SetPrivate(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
