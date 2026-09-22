using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    [CreateAssetMenu(menuName = "Events/Event Catalog")]
    public sealed class EventCatalogSO : ScriptableObject
    {
        public EventCategoryCatalogSO categoryCatalog;
        public List<EventDefinitionSO> definitions = new List<EventDefinitionSO>();
        private readonly Dictionary<string, EventDefinitionSO> byId = new Dictionary<string, EventDefinitionSO>(StringComparer.Ordinal);
        private readonly Dictionary<string, EventDefinitionSO> byCode = new Dictionary<string, EventDefinitionSO>(StringComparer.OrdinalIgnoreCase);
        private bool initialized;

        private void OnEnable() { initialized = false; }
        private void OnValidate() { initialized = false; }

        public List<string> Initialize(bool logErrors = true)
        {
            byId.Clear();
            byCode.Clear();
            var errors = new List<string>();
            if (definitions == null) errors.Add("Catalog definition list is null.");
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions ?? new List<EventDefinitionSO>())
            {
                if (definition == null) { errors.Add("Catalog contains a null definition."); continue; }
                Add(byId, duplicateIds, definition.PersistentId, definition, "persistentId", errors);
                Add(byCode, duplicateCodes, definition.eventCode, definition, "eventCode", errors);
            }
            // Ambiguous entries must never resolve to an arbitrary asset.
            foreach (var id in duplicateIds) byId.Remove(id);
            foreach (var code in duplicateCodes) byCode.Remove(code);
            initialized = true;
            if (logErrors) foreach (var error in errors) Debug.LogError($"[EventCatalog:{name}] {error}", this);
            return errors;
        }

        private static void Add(Dictionary<string, EventDefinitionSO> index, HashSet<string> duplicates,
            string key, EventDefinitionSO definition, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(key)) { errors.Add($"{definition.name}: empty {label}."); return; }
            if (index.ContainsKey(key)) { duplicates.Add(key); errors.Add($"{definition.name}: duplicate {label} '{key}'."); }
            else index.Add(key, definition);
        }

        public bool TryGetById(string id, out EventDefinitionSO definition)
        {
            if (!initialized) Initialize();
            definition = null;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out definition);
        }

        public bool TryGetByCode(string code, out EventDefinitionSO definition)
        {
            if (!initialized) Initialize();
            definition = null;
            return !string.IsNullOrEmpty(code) && byCode.TryGetValue(code, out definition);
        }

        public IEnumerable<EventDefinitionSO> GetEnabled(string tag = null)
        {
            if (!initialized) Initialize();
            return byId.Values.Where(d => d.enabled && byCode.TryGetValue(d.eventCode ?? "", out var indexed) && indexed == d
                && (string.IsNullOrEmpty(tag) || (d.tags != null && d.tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))))
                .OrderBy(d => d.sortOrder).ThenBy(d => d.eventCode, StringComparer.Ordinal);
        }

        public IEnumerable<EventDefinitionSO> GetEnabledByCategoryId(string categoryId, string tag = null)
            => GetEnabled(tag).Where(d => !string.IsNullOrEmpty(categoryId) && string.Equals(d.CategoryId, categoryId, StringComparison.Ordinal));

        public IEnumerable<EventDefinitionSO> GetEnabledWithoutCategory(string tag = null)
            => GetEnabled(tag).Where(d => string.IsNullOrEmpty(d.CategoryId));
    }
}
