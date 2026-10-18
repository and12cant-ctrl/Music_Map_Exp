using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public enum EventIssueSeverity { Warning, Error, Info }
    public sealed class EventValidationIssue
    {
        public EventIssueSeverity severity;
        public UnityEngine.Object asset;
        public string message;
        public override string ToString() => $"{severity}: {asset?.name}: {message}";
    }

    public static class EventValidator
    {
        public static List<EventValidationIssue> Validate(IEnumerable<EventDefinitionSO> all, EventCatalogSO catalog)
        {
            var issues = new List<EventValidationIssue>();
            var definitions = (all ?? Enumerable.Empty<EventDefinitionSO>()).Where(d => d != null).Distinct().ToArray();
            void Add(UnityEngine.Object asset, string message, EventIssueSeverity severity = EventIssueSeverity.Error)
                => issues.Add(new EventValidationIssue { asset = asset, message = message, severity = severity });
            foreach (var group in definitions.Where(d => !string.IsNullOrEmpty(d.PersistentId)).GroupBy(d => d.PersistentId).Where(g => g.Count() > 1))
                foreach (var definition in group) Add(definition, $"Duplicate persistentId '{group.Key}'. Use the database duplication command for new assets.");
            foreach (var group in definitions.Where(d => !string.IsNullOrWhiteSpace(d.eventCode)).GroupBy(d => d.eventCode, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                foreach (var definition in group) Add(definition, $"Duplicate eventCode '{group.Key}'.");
            EventCategoryCatalogSO categoryCatalog = null;
            if (catalog != null)
            {
                foreach (var error in catalog.Initialize(false)) Add(catalog, error);
                categoryCatalog = catalog.categoryCatalog;
                if (categoryCatalog == null) Add(catalog, "Category catalog is not assigned.");
                else foreach (var error in categoryCatalog.Initialize(false)) Add(categoryCatalog, error);
                foreach (var definition in definitions)
                    if (catalog.definitions == null || !catalog.definitions.Contains(definition)) Add(definition, "Not registered in the selected catalog.", EventIssueSeverity.Warning);
            }
            else Add(null, "Event catalog is required to resolve event categories.");
            foreach (var definition in definitions)
            {
                if (!Guid.TryParse(definition.PersistentId, out _)) Add(definition, "persistentId is empty or not a GUID.");
                if (string.IsNullOrWhiteSpace(definition.eventCode)) Add(definition, "eventCode is empty.");
                if (string.IsNullOrWhiteSpace(definition.displayName)) Add(definition, "displayName is empty.");
                CheckCategory(definition);
                if (definition.appearance == null || definition.reservation == null || definition.execution == null)
                { Add(definition, "Appearance/reservation/execution section is null."); continue; }
                if (definition.useExtendedLifecycle) { issues.AddRange(EventExpansionValidator.Validate(definition, catalog)); continue; }
                var reservation = definition.reservation;
                if (reservation.triggerMode == EventTriggerMode.Scheduled && reservation.requiredPhaseCount <= 0) Add(definition, "Scheduled requires requiredPhaseCount > 0.");
                if (reservation.triggerMode == EventTriggerMode.Immediate && reservation.requiredPhaseCount != 0) Add(definition, "Immediate requires requiredPhaseCount == 0.");
                if (definition.execution.executionType != EventExecutionType.Narrative && reservation.triggerMode != EventTriggerMode.Immediate) Add(definition, "Shop/Offering/Encounter must be Immediate.");
                if (reservation.cardSlots == null || reservation.cardSlots.Count > 32 || reservation.cardSlots.Any(s => s == null || s.amount < 1 || (s.acceptedType == CardType.Chara && s.amount != 1))) Add(definition, "Invalid card slots (maximum 32; positive amounts; character amount must be 1).");
                if (reservation.triggerMode == EventTriggerMode.Immediate && ((reservation.cardSlots?.Count ?? 0) > 0 || (reservation.requirements?.Count ?? 0) > 0)) Add(definition, "Immediate events bypass reservation and cannot require assigned cards/parameters.");
                if (reservation.requirements == null || reservation.requirements.Any(r => r == null || r.minimum < 0)) Add(definition, "Invalid parameter requirements.");
                if (definition.appearance.weight < 0 || float.IsNaN(definition.appearance.weight) || float.IsInfinity(definition.appearance.weight) || definition.appearance.requiredPlacements < 0) Add(definition, "Invalid appearance weight/count.");
                if (definition.appearance.conditions == null || definition.appearance.conditions.Any(c => c == null)) Add(definition, "Invalid appearance conditions.");
                if (definition.appearance.conditions != null && definition.appearance.conditions.Any(c => c != null && (c.kind == EventConditionKind.JudgeSucceeded || c.kind == EventConditionKind.JudgeFailed || c.kind == EventConditionKind.TotalAtLeast || c.kind == EventConditionKind.TotalBelow || c.kind == EventConditionKind.ParameterAtLeast))) Add(definition, "Appearance has a judgement/card condition that cannot be evaluated before placement.");
                if (definition.appearance.mapIcon == null) Add(definition, "Map icon is missing.", EventIssueSeverity.Warning);
                if (reservation.triggerMode == EventTriggerMode.Scheduled) CheckPresentation(reservation.reservationPresentation, "Reservation");
                if (definition.execution.executionType == EventExecutionType.Narrative)
                {
                    CheckPresentation(definition.execution.beforeJudge, "Before judgement");
                    var judge = definition.execution.judge;
                    if (judge == null || judge.diceCount < 1 || judge.diceCount > 100 || judge.diceSides < 2 || judge.diceSides > 10000) Add(definition, "Invalid dice settings.");
                }
                if (definition.outcomes == null || definition.outcomes.Count == 0) { Add(definition, "No outcomes."); continue; }
                if (definition.outcomes.Count(o => o != null && o.isDefault) != 1) Add(definition, "Exactly one default outcome is required.");
                var outcomeCodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var outcome in definition.outcomes)
                {
                    if (outcome == null) { Add(definition, "Null outcome."); continue; }
                    if (string.IsNullOrWhiteSpace(outcome.outcomeCode) || !outcomeCodes.Add(outcome.outcomeCode)) Add(definition, "Empty or duplicate outcomeCode.");
                    if (outcome.conditions == null || outcome.conditions.Any(c => c == null)) Add(definition, "Invalid outcome conditions.");
                    CheckPresentation(outcome.presentation, "Outcome " + outcome.outcomeCode);
                    if (outcome.effects == null || outcome.followUps == null) { Add(definition, "Null effect/follow-up list."); continue; }
                    foreach (var effect in outcome.effects)
                    {
                        if (effect == null) { Add(definition, "Null effect."); continue; }
                        if (effect.kind == EventEffectKind.GrantGold || effect.kind == EventEffectKind.ConsumeGold)
                            if (effect.amount < 0) Add(definition, "Gold amount cannot be negative.");
                        if (effect.kind == EventEffectKind.GrantAbility && effect.ability == null || effect.kind == EventEffectKind.GrantEquip && effect.equip == null || effect.kind == EventEffectKind.GrantHint && effect.hint == null) Add(definition, "Reward asset is missing.");
                        if ((effect.kind == EventEffectKind.GrantAbility || effect.kind == EventEffectKind.GrantEquip || effect.kind == EventEffectKind.GrantHint) && (effect.amount < 1 || effect.amount > 10000)) Add(definition, "Reward count must be between 1 and 10000.");
                        if (effect.kind == EventEffectKind.SetFlag && string.IsNullOrWhiteSpace(effect.key)) Add(definition, "Flag key is empty.");
                        if (effect.kind == EventEffectKind.UnlockEvent || effect.kind == EventEffectKind.ReserveEvent) CheckReference(effect.eventDefinition, effect.kind == EventEffectKind.ReserveEvent);
                    }
                    foreach (var followUp in outcome.followUps)
                    {
                        if (followUp == null) Add(definition, "Null follow-up.");
                        else CheckReference(followUp.definition, followUp.startMode == EventFollowUpMode.Reserve);
                    }
                }

                void CheckPresentation(EventPresentation presentation, string label)
                {
                    if (presentation == null || string.IsNullOrWhiteSpace(presentation.text)) Add(definition, label + ": required text is missing.");
                    if (presentation == null || presentation.image == null) Add(definition, label + ": required image is missing.");
                }
                void CheckReference(EventDefinitionSO target, bool autoStart)
                {
                    if (target == null || !definitions.Contains(target) || (catalog != null && (catalog.definitions == null || !catalog.definitions.Contains(target)))) Add(definition, "Follow-up definition is missing or unregistered.");
                    else if (autoStart && !target.enabled) Add(definition, "Automatic follow-up is disabled.");
                    else if (autoStart && ((target.reservation?.cardSlots?.Any(s => s != null && s.required) ?? false) || (target.reservation?.requirements?.Count ?? 0) > 0)) Add(definition, "Automatic follow-up needs card assignment. Use SpawnAvailable and reserve through the UI.");
                }
            }
            foreach (var definition in definitions)
            {
                if (HasCycle(definition, false)) Add(definition, "Reference loop detected.", EventIssueSeverity.Warning);
                if (HasCycle(definition, true)) Add(definition, "Dangerous automatic Immediate event cycle detected.");
            }
            return issues;

            void CheckCategory(EventDefinitionSO definition)
            {
                if (definition.CategoryMigrationState == EventCategoryMigrationState.Pending && !string.IsNullOrEmpty(definition.CategoryId))
                    Add(definition, "Category migration is pending even though a new category id is already assigned.");

                if (string.IsNullOrEmpty(definition.CategoryId))
                    Add(definition, definition.enabled ? "Enabled event has no category." : "Draft event has no category.",
                        definition.enabled ? EventIssueSeverity.Error : EventIssueSeverity.Warning);
                else if (categoryCatalog != null)
                {
                    if (!categoryCatalog.TryGetById(definition.CategoryId, out var category)) Add(definition, $"Category id '{definition.CategoryId}' is missing or ambiguous in the assigned category catalog.");
                    else if (category.IsDeprecated) Add(definition, $"Category '{category.DisplayName}' is deprecated.", EventIssueSeverity.Warning);
                }

                if (definition.CategoryMigrationState != EventCategoryMigrationState.Pending) return;
                if (string.IsNullOrEmpty(definition.LegacyCategory))
                {
                    Add(definition, "Legacy category migration is pending; the old value is empty and can be confirmed as unassigned.", EventIssueSeverity.Warning);
                    return;
                }
                if (categoryCatalog == null)
                {
                    Add(definition, $"Legacy category '{definition.LegacyCategory}' is pending and cannot be assessed without a category catalog.", EventIssueSeverity.Warning);
                    return;
                }
                if (categoryCatalog.TryGetByStrictDisplayName(definition.LegacyCategory, out var exact))
                    Add(definition, $"Legacy category '{definition.LegacyCategory}' is pending; exact candidate: '{exact.DisplayName}'.", EventIssueSeverity.Warning);
                else if (categoryCatalog.TryGetByNormalizedDisplayName(definition.LegacyCategory, out var normalized))
                    Add(definition, $"Legacy category '{definition.LegacyCategory}' only matches '{normalized.DisplayName}' after case/Unicode normalization; manual confirmation is required.", EventIssueSeverity.Warning);
                else Add(definition, $"Legacy category '{definition.LegacyCategory}' has no unambiguous exact candidate; manual confirmation is required.", EventIssueSeverity.Warning);
            }
        }

        private static IEnumerable<EventDefinitionSO> Edges(EventDefinitionSO definition, bool immediateOnly)
        {
            if (definition?.EffectiveOutcomes == null) yield break;
            if (immediateOnly && (definition.useExtendedLifecycle ? definition.extension.deadlinePhaseCount != 0 : definition.reservation?.triggerMode != EventTriggerMode.Immediate)) yield break;
            foreach (var outcome in definition.EffectiveOutcomes.Where(o => o != null))
            {
                foreach (var followUp in outcome.followUps ?? new List<EventFollowUp>())
                    if (followUp?.definition != null && (!immediateOnly || (definition.useExtendedLifecycle
                        ? followUp.delayPhaseCount == 0 && followUp.definition.useExtendedLifecycle && followUp.definition.extension?.deadlinePhaseCount == 0
                        : followUp.startMode == EventFollowUpMode.Reserve && followUp.definition.reservation?.triggerMode == EventTriggerMode.Immediate))) yield return followUp.definition;
                foreach (var effect in outcome.effects ?? new List<EventEffect>())
                    if (effect?.eventDefinition != null && (effect.kind == EventEffectKind.UnlockEvent || effect.kind == EventEffectKind.ReserveEvent)
                        && (!immediateOnly || effect.kind == EventEffectKind.ReserveEvent && effect.eventDefinition.reservation?.triggerMode == EventTriggerMode.Immediate)) yield return effect.eventDefinition;
            }
        }

        private static bool HasCycle(EventDefinitionSO root, bool immediateOnly)
        {
            var pending = new Stack<EventDefinitionSO>(Edges(root, immediateOnly));
            var visited = new HashSet<EventDefinitionSO>();
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node == root) return true;
                if (!visited.Add(node)) continue;
                foreach (var target in Edges(node, immediateOnly)) pending.Push(target);
            }
            return false;
        }
    }
}
