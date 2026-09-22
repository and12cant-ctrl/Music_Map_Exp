using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MusicMap.Events
{
    public enum EventCategoryMigrationState
    {
        Pending = 0,
        Migrated = 1
    }

    [Serializable]
    public sealed class EventCategoryDefinition
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private int sortOrder;
        [SerializeField] private bool isDeprecated;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int SortOrder => sortOrder;
        public bool IsDeprecated => isDeprecated;

        public EventCategoryDefinition() { }

#if UNITY_EDITOR
        internal EventCategoryDefinition(string id, string displayName, string description, int sortOrder)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.sortOrder = sortOrder;
        }

        internal void EditorUpdate(string newDisplayName, string newDescription, int newSortOrder, bool deprecated)
        {
            displayName = newDisplayName;
            description = newDescription;
            sortOrder = newSortOrder;
            isDeprecated = deprecated;
        }
#endif
    }

    [CreateAssetMenu(menuName = "Events/Event Category Catalog")]
    public sealed class EventCategoryCatalogSO : ScriptableObject
    {
        [SerializeField, HideInInspector] private List<EventCategoryDefinition> categories = new List<EventCategoryDefinition>();
        private readonly Dictionary<string, EventCategoryDefinition> byId = new Dictionary<string, EventCategoryDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, EventCategoryDefinition> byNormalizedName = new Dictionary<string, EventCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
        private bool initialized;

        public IReadOnlyList<EventCategoryDefinition> Categories
            => categories != null
                ? (IReadOnlyList<EventCategoryDefinition>)categories
                : Array.Empty<EventCategoryDefinition>();

        private void OnEnable() { initialized = false; }
        private void OnValidate() { initialized = false; }
        public void InvalidateCache() { initialized = false; }

        public static string NormalizeDisplayName(string value)
        {
            if (value == null) return string.Empty;
            return value.Trim().Normalize(NormalizationForm.FormKC);
        }

        public List<string> Initialize(bool logErrors = true)
        {
            byId.Clear();
            byNormalizedName.Clear();
            var errors = new List<string>();
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (categories == null) errors.Add("Category list is null.");

            int index = 0;
            foreach (var category in categories ?? new List<EventCategoryDefinition>())
            {
                if (category == null)
                {
                    errors.Add($"Category at index {index} is null.");
                    index++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(category.Id)) errors.Add($"Category at index {index} has an empty id.");
                else
                {
                    bool hasValidId = Guid.TryParse(category.Id, out _);
                    if (!hasValidId) errors.Add($"Category '{category.DisplayName}' has a non-GUID id '{category.Id}'.");
                    if (hasValidId && byId.ContainsKey(category.Id))
                    {
                        duplicateIds.Add(category.Id);
                        errors.Add($"Duplicate category id '{category.Id}'.");
                    }
                    else if (hasValidId) byId.Add(category.Id, category);
                }

                var normalizedName = NormalizeDisplayName(category.DisplayName);
                if (string.IsNullOrEmpty(normalizedName)) errors.Add($"Category '{category.Id}' has an empty displayName.");
                else if (byNormalizedName.ContainsKey(normalizedName))
                {
                    duplicateNames.Add(normalizedName);
                    errors.Add($"Duplicate category displayName '{category.DisplayName}' after normalization.");
                }
                else byNormalizedName.Add(normalizedName, category);
                index++;
            }

            foreach (var duplicate in duplicateIds) byId.Remove(duplicate);
            foreach (var duplicate in duplicateNames) byNormalizedName.Remove(duplicate);
            foreach (var unusableName in byNormalizedName
                .Where(pair => !byId.TryGetValue(pair.Value.Id, out var indexed) || indexed != pair.Value)
                .Select(pair => pair.Key).ToArray())
                byNormalizedName.Remove(unusableName);
            initialized = true;
            if (logErrors) foreach (var error in errors) Debug.LogError($"[EventCategoryCatalog:{name}] {error}", this);
            return errors;
        }

        public bool TryGetById(string id, out EventCategoryDefinition category)
        {
            if (!initialized) Initialize();
            category = null;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out category);
        }

        public bool TryGetByNormalizedDisplayName(string displayName, out EventCategoryDefinition category)
        {
            if (!initialized) Initialize();
            category = null;
            var key = NormalizeDisplayName(displayName);
            return !string.IsNullOrEmpty(key) && byNormalizedName.TryGetValue(key, out category);
        }

        public bool TryGetByStrictDisplayName(string displayName, out EventCategoryDefinition category)
        {
            category = null;
            var matches = (categories ?? new List<EventCategoryDefinition>()).Where(c => c != null && string.Equals(c.DisplayName, displayName, StringComparison.Ordinal)).Take(2).ToArray();
            if (matches.Length != 1 || !TryGetById(matches[0].Id, out var indexed) || indexed != matches[0]) return false;
            category = matches[0];
            return true;
        }

        public IEnumerable<EventCategoryDefinition> GetCategories(bool includeDeprecated = true)
        {
            if (!initialized) Initialize();
            return byId.Values.Where(c => includeDeprecated || !c.IsDeprecated)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Id, StringComparer.Ordinal);
        }

#if UNITY_EDITOR
        public bool EditorTryAdd(string displayName, string description, int sortOrder, out EventCategoryDefinition created, out string error)
        {
            created = null;
            if (!EditorValidateName(displayName, null, out var trimmed, out error)) return false;
            created = new EventCategoryDefinition(Guid.NewGuid().ToString("N"), trimmed, description ?? string.Empty, sortOrder);
            if (categories == null) categories = new List<EventCategoryDefinition>();
            categories.Add(created);
            initialized = false;
            return true;
        }

        public bool EditorTryUpdate(string id, string displayName, string description, int sortOrder, bool isDeprecated, out string error)
        {
            if (!TryGetById(id, out var category))
            {
                error = $"Category id '{id}' is missing or ambiguous.";
                return false;
            }
            if (!EditorValidateName(displayName, id, out var trimmed, out error)) return false;
            category.EditorUpdate(trimmed, description ?? string.Empty, sortOrder, isDeprecated);
            initialized = false;
            return true;
        }

        private bool EditorValidateName(string displayName, string ignoredId, out string trimmed, out string error)
        {
            trimmed = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "カテゴリ名は空欄にできません。";
                return false;
            }
            var key = NormalizeDisplayName(trimmed);
            var duplicate = (categories ?? new List<EventCategoryDefinition>()).FirstOrDefault(c => c != null && c.Id != ignoredId &&
                string.Equals(NormalizeDisplayName(c.DisplayName), key, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                error = $"同じカテゴリ名が既に登録されています: {duplicate.DisplayName}";
                return false;
            }
            error = null;
            return true;
        }
#endif
    }
}
