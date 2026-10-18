using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public sealed partial class EventManager
    {
        private readonly Dictionary<string, EventInstance> instances = new Dictionary<string, EventInstance>(StringComparer.Ordinal);
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly MapDataSO map;
        private readonly IEventCardSource cards;
        public EventCatalogSO Catalog { get; }
        public int CurrentPhase { get; private set; }
        public IEnumerable<EventInstance> Instances => instances.Values;
        public event Action<EventInstance> Changed;
        public event Action Restored;

        public EventManager(EventCatalogSO catalog, MapDataSO map = null, IEventCardSource cards = null, int phase = 0)
        {
            Catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.map = map;
            this.cards = cards;
            CurrentPhase = Math.Max(0, phase);
            Catalog.Initialize();
        }

        public bool TryGet(string id, out EventInstance instance)
        {
            instance = null;
            return !string.IsNullOrEmpty(id) && instances.TryGetValue(id, out instance);
        }

        public EventInstance Spawn(EventDefinitionSO definition, Vector2Int position, string sourceEventInstanceId = null)
        {
            if (definition == null || !definition.enabled || !Catalog.TryGetById(definition.PersistentId, out var found) || found != definition)
                return SpawnError("Definition is disabled, missing, or ambiguous in the catalog.");
            if (definition.reservation == null || definition.execution == null || definition.EffectiveOutcomes == null)
                return SpawnError("Definition is missing reservation, execution, or outcomes.");
            if (map != null && (!TryCell(position, out var cell) || !string.IsNullOrEmpty(cell.eventInstanceId) || cell.EvData != null))
                return SpawnError($"Cell {position} is invalid or already contains an event.");
            if (!CanAppear(definition)) return null;
            if (definition.useExtendedLifecycle && (definition.extension.deadlinePhaseCount < 0 || (long)CurrentPhase + definition.extension.deadlinePhaseCount > int.MaxValue)) return SpawnError("Invalid deadline.");
            var instance = new EventInstance
            {
                instanceId = Guid.NewGuid().ToString("N"), definitionId = definition.PersistentId,
                sourceEventInstanceId = sourceEventInstanceId,
                cellPosition = position, status = EventStatus.Available,
                randomSeed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)
            };
            instance.appearedPhase = CurrentPhase;
            instance.deadlinePhase = definition.useExtendedLifecycle ? CurrentPhase + definition.extension.deadlinePhaseCount : -1;
            instances.Add(instance.instanceId, instance);
            if (map != null && TryCell(position, out var target)) target.eventInstanceId = instance.instanceId;
            if (instance.deadlinePhase == CurrentPhase) Expire(instance);
            else Changed?.Invoke(instance);
            return instance;
        }

        private static EventInstance SpawnError(string error) { Debug.LogError($"[EventManager.Spawn] {error}"); return null; }

        public bool Reserve(string id, IReadOnlyList<string> cardIds, int currentPhase, out string error)
        {
            error = null;
            if (!TryGetDefinition(id, out var instance, out var definition, out error)) return false;
            if (instance.status != EventStatus.Available) return Fail("Event is not Available.", out error);
            if (definition.useExtendedLifecycle) return StartExtended(instance, definition, cardIds, currentPhase, out error);
            if (!definition.enabled || definition.execution.executionType != EventExecutionType.Narrative || definition.reservation == null || definition.reservation.triggerMode != EventTriggerMode.Scheduled || definition.reservation.requiredPhaseCount <= 0)
                return Fail("Only Scheduled events with a positive phase count can be reserved.", out error);
            if (currentPhase != CurrentPhase || currentPhase > int.MaxValue - definition.reservation.requiredPhaseCount)
                return Fail("Current phase is stale or overflows executePhase.", out error);
            if (!ValidateCards(definition, cardIds, out error)) return false;
            instance.assignedCardIds = new List<string>(cardIds);
            instance.slotAssignments = CaptureSlots(definition, cardIds);
            instance.participantIds = (cards as IEventParticipantSource)?.Participants(cardIds).Distinct().ToList() ?? new List<string>();
            instance.reservedPhase = currentPhase;
            instance.executePhase = currentPhase + definition.reservation.requiredPhaseCount;
            instance.status = EventStatus.Reserved;
            Changed?.Invoke(instance);
            return true;
        }

        public bool ValidateCards(EventDefinitionSO definition, IReadOnlyList<string> ids, out string error)
        {
            if (definition?.reservation == null) return Fail("Reservation definition is missing.", out error);
            var slots = definition.reservation.cardSlots;
            if (slots == null || ids == null || ids.Count != slots.Count) return Fail("Card slot count does not match the definition.", out error);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot == null || slot.amount < 1) return Fail($"Slot {index + 1} has an invalid definition.", out error);
                var id = ids[index];
                if (string.IsNullOrEmpty(id))
                {
                    if (definition.useExtendedLifecycle ? slot.slotType == SlotType.Required || slot.slotType == SlotType.Involved : slot.required) return Fail($"Slot {index + 1} requires a card.", out error);
                    continue;
                }
                if (!unique.Add(id) || IsCardAssigned(id)) return Fail($"Card '{id}' is already assigned.", out error);
                if (cards == null || !cards.TryGetCard(id, out var type, out var amount) || type != slot.acceptedType || amount < slot.amount)
                    return Fail($"Card '{id}' is missing or does not meet slot {index + 1} requirements.", out error);
            }
            if (definition.reservation.requirements == null) return Fail("Requirements list is missing.", out error);
            foreach (var requirement in definition.reservation.requirements)
                if (requirement == null || cards == null || !cards.TryGetParameter(ids, requirement.parameter, out var value) || value < requirement.minimum)
                    return Fail("Assigned cards do not meet the required parameters.", out error);
            error = null;
            return true;
        }

        public bool IsCardAssigned(string id) => !string.IsNullOrEmpty(id) && instances.Values.Any(i =>
            (i.status == EventStatus.Reserved || i.status == EventStatus.Ready || i.status == EventStatus.Running) && i.assignedCardIds.Contains(id));

        public bool CancelReservation(string id, out string error)
        {
            if (!TryGet(id, out var instance) || (instance.status != EventStatus.Reserved && instance.status != EventStatus.Ready))
                return Fail("Only Reserved or Ready events can be cancelled.", out error);
            instance.status = EventStatus.Cancelled;
            instance.assignedCardIds.Clear();
            ClearCell(instance);
            Changed?.Invoke(instance);
            error = null;
            return true;
        }

        public void AdvancePhase(int phase)
        {
            if (phase < CurrentPhase) { Debug.LogError("[EventManager] Phase cannot move backwards; restore a matching save instead."); return; }
            CurrentPhase = phase;
            foreach (var available in instances.Values.Where(i => i.status == EventStatus.Available && i.deadlinePhase >= 0 && i.deadlinePhase <= phase).ToArray()) Expire(available);
            ProcessFollowUps();
            foreach (var instance in instances.Values.ToArray())
                if (instance.status == EventStatus.Reserved && instance.executePhase <= phase)
                {
                    instance.status = EventStatus.Ready;
                    Changed?.Invoke(instance);
                }
        }

        public bool RequestImmediate(string id, out string error)
        {
            if (!TryGetDefinition(id, out var instance, out var definition, out error)) return false;
            if (definition.useExtendedLifecycle) return Reserve(id, Array.Empty<string>(), CurrentPhase, out error);
            if (definition.reservation == null || definition.reservation.triggerMode != EventTriggerMode.Immediate || definition.reservation.requiredPhaseCount != 0)
                return Fail("Immediate events must have zero required phases.", out error);
            if (!definition.enabled || definition.reservation.cardSlots == null || definition.reservation.cardSlots.Count != 0 || definition.reservation.requirements == null || definition.reservation.requirements.Count != 0)
                return Fail("Immediate event is disabled or requires reservation cards/parameters.", out error);
            if (instance.status != EventStatus.Available && instance.status != EventStatus.Ready) return Fail("Event cannot execute in its current state.", out error);
            instance.status = EventStatus.Ready;
            Changed?.Invoke(instance);
            error = null;
            return true;
        }

        public bool TryGetDefinition(string id, out EventInstance instance, out EventDefinitionSO definition, out string error)
        {
            definition = null;
            if (!TryGet(id, out instance)) return Fail($"Unknown instanceId '{id ?? "<null>"}'.", out error);
            if (!Catalog.TryGetById(instance.definitionId, out definition)) return Fail($"Missing definitionId '{instance.definitionId}' for '{id}'.", out error);
            error = null;
            return true;
        }

        internal void Notify(EventInstance instance) => Changed?.Invoke(instance);
        internal void Resolve(EventInstance instance)
        {
            RecordHistory(instance);
            instance.status = EventStatus.Resolved;
            instance.lastError = null;
            ClearCell(instance);
            Changed?.Invoke(instance);
        }

        public bool IsUnlocked(string id) => unlocked.Contains(id);
        public bool Unlock(EventDefinitionSO definition, out string error)
        {
            if (definition == null || !Catalog.TryGetById(definition.PersistentId, out var found) || found != definition)
                return Fail("Follow-up definition is not registered.", out error);
            unlocked.Add(definition.PersistentId);
            error = null;
            return true;
        }

        public bool StartFollowUp(EventInstance parent, EventDefinitionSO definition, EventFollowUpMode mode, out string error)
        {
            if (mode == EventFollowUpMode.Unlock) return Unlock(definition, out error);
            if (parent == null || definition == null || !definition.enabled || definition.reservation == null || !Catalog.TryGetById(definition.PersistentId, out var registered) || registered != definition)
                return Fail("Invalid or disabled follow-up definition.", out error);
            if (definition.reservation.cardSlots == null) return Fail("Follow-up card slots are missing.", out error);
            bool autoImmediate = mode == EventFollowUpMode.Reserve && definition.reservation.triggerMode == EventTriggerMode.Immediate;
            if (autoImmediate && (parent.immediateAncestors.Count >= 32 || parent.definitionId == definition.PersistentId || parent.immediateAncestors.Contains(definition.PersistentId)))
                return Fail("Automatic immediate event cycle was blocked.", out error);
            if (mode == EventFollowUpMode.Reserve && !autoImmediate && !ValidateCards(definition, Enumerable.Repeat("", definition.reservation.cardSlots.Count).ToArray(), out error)) return false;
            if (autoImmediate && (definition.reservation.requiredPhaseCount != 0 || definition.reservation.cardSlots.Count != 0 || definition.reservation.requirements == null || definition.reservation.requirements.Count != 0))
                return Fail("Invalid Immediate follow-up configuration.", out error);
            if (mode == EventFollowUpMode.Reserve && !autoImmediate && (definition.reservation.requiredPhaseCount <= 0 || definition.execution?.executionType != EventExecutionType.Narrative || CurrentPhase > int.MaxValue - definition.reservation.requiredPhaseCount))
                return Fail("Invalid Scheduled follow-up configuration.", out error);
            var position = parent.cellPosition;
            bool replacesParent = parent.status == EventStatus.Running && TryCell(position, out var parentCell) && parentCell.eventInstanceId == parent.instanceId;
            if (!replacesParent && !FindFreeCell(position, out position)) return Fail("No free map cell for the follow-up event.", out error);
            if (replacesParent) ClearCell(parent);
            var child = Spawn(definition, position, parent.instanceId);
            if (child == null)
            {
                if (replacesParent && TryCell(position, out var originalCell)) { originalCell.eventInstanceId = parent.instanceId; parent.occupiesCell = true; }
                return Fail("Could not spawn the follow-up.", out error);
            }
            unlocked.Add(definition.PersistentId);
            error = null;
            if (autoImmediate)
            {
                child.immediateAncestors = new List<string>(parent.immediateAncestors) { parent.definitionId };
                return RequestImmediate(child.instanceId, out error);
            }
            if (mode == EventFollowUpMode.Reserve)
                return Reserve(child.instanceId, Enumerable.Repeat("", definition.reservation.cardSlots.Count).ToArray(), CurrentPhase, out error);
            return true;
        }

        private bool FindFreeCell(Vector2Int preferred, out Vector2Int position)
        {
            position = preferred;
            if (map == null) return true;
            if (TryCell(preferred, out var cell) && string.IsNullOrEmpty(cell.eventInstanceId) && cell.EvData == null) return true;
            if (map.mapGrid == null) return false;
            for (int y = 0; y < map.mapGrid.GetLength(1); y++)
                for (int x = 0; x < map.mapGrid.GetLength(0); x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (TryCell(candidate, out cell) && string.IsNullOrEmpty(cell.eventInstanceId) && cell.EvData == null) { position = candidate; return true; }
                }
            return false;
        }

        private bool TryCell(Vector2Int position, out MapCell cell)
        {
            cell = null;
            if (map?.mapGrid == null || position.x < 0 || position.y < 0 || position.x >= map.mapGrid.GetLength(0) || position.y >= map.mapGrid.GetLength(1)) return false;
            cell = map.mapGrid[position.x, position.y];
            return cell != null;
        }

        private void ClearCell(EventInstance instance)
        {
            if (TryCell(instance.cellPosition, out var cell) && cell.eventInstanceId == instance.instanceId) cell.eventInstanceId = null;
            instance.occupiesCell = false;
        }

        public EventSaveData CreateSaveData() => new EventSaveData
        {
            version = 3,
            progress = Progress.Copy(),
            currentPhase = CurrentPhase,
            instances = instances.Values.OrderBy(i => i.instanceId, StringComparer.Ordinal).Select(i => i.Copy()).ToList(),
            unlockedDefinitionIds = unlocked.OrderBy(id => id, StringComparer.Ordinal).ToList()
        };

        public List<string> Restore(EventSaveData data)
        {
            var errors = new List<string>();
            if (data == null || (data.version < 1 || data.version > 3) || data.currentPhase < 0 || data.instances == null)
            { errors.Add("Unsupported or invalid event save; current state was preserved."); return errors; }
            if (data.version >= 2 && (data.progress == null || !data.progress.IsValid()))
            { errors.Add("Invalid event progress; current state was preserved."); return errors; }
            var restored = new Dictionary<string, EventInstance>(StringComparer.Ordinal);
            var positions = new HashSet<Vector2Int>();
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.instances)
            {
                if (saved == null || string.IsNullOrEmpty(saved.instanceId) || restored.ContainsKey(saved.instanceId))
                { errors.Add($"Null or duplicate instanceId '{saved?.instanceId}'. Entry skipped."); continue; }
                var instance = saved.Copy();
                if (data.version == 1) instance.deadlinePhase = -1;
                restored.Add(instance.instanceId, instance);
                string problem = null;
                if (!EventSlotAssignment.IsValid(saved.slotAssignments)) problem = "Invalid saved Slot assignments.";
                if (!Catalog.TryGetById(instance.definitionId, out var definition)) problem = $"Missing definitionId '{instance.definitionId}'.";
                else if (!Enum.IsDefined(typeof(EventStatus), instance.status)) problem = "Invalid event status.";
                else if (definition.reservation?.cardSlots == null || definition.execution == null || definition.EffectiveOutcomes == null) problem = "Incomplete event definition.";
                else if (instance.appliedEffectCount < 0 || instance.appliedFollowUpCount < 0) problem = "Invalid result cursor.";
                bool active = instance.status != EventStatus.Resolved && instance.status != EventStatus.Cancelled;
                if (problem == null && active && instance.occupiesCell && map != null && (!TryCell(instance.cellPosition, out _) || positions.Contains(instance.cellPosition))) problem = "Invalid or duplicate map cell.";
                if (problem == null && active && instance.status != EventStatus.Available)
                {
                    var nonEmptyIds = instance.assignedCardIds.Where(id => !string.IsNullOrEmpty(id)).ToArray();
                    if (instance.resolvedOutcomeCode != "EXPIRED" && instance.assignedCardIds.Count != definition.reservation.cardSlots.Count) problem = "Saved card slot count differs from the definition.";
                    else if (nonEmptyIds.Distinct(StringComparer.Ordinal).Count() != nonEmptyIds.Length || nonEmptyIds.Any(id => assigned.Contains(id))) problem = "Card is assigned to multiple saved events.";
                    else if (instance.status == EventStatus.Reserved && ((!definition.useExtendedLifecycle && definition.reservation.triggerMode != EventTriggerMode.Scheduled) || instance.reservedPhase < 0 || instance.executePhase <= instance.reservedPhase)) problem = "Invalid saved reservation.";
                    else if (!instance.hasJudgement && (instance.appliedEffectCount > 0 || instance.appliedFollowUpCount > 0 || !string.IsNullOrEmpty(instance.resolvedOutcomeCode))) problem = "Result cursor exists without a completed judgement.";
                    else if (instance.hasJudgement)
                    {
                        var outcome = definition.EffectiveOutcomes.FirstOrDefault(o => o != null && o.outcomeCode == instance.resolvedOutcomeCode);
                        if (outcome?.effects == null || outcome.followUps == null || instance.appliedEffectCount > outcome.EffectCount || instance.appliedFollowUpCount > outcome.followUps.Count) problem = "Saved outcome no longer matches the definition.";
                    }
                }
                if (problem != null)
                {
                    instance.status = EventStatus.Cancelled;
                    instance.occupiesCell = false;
                    instance.lastError = problem;
                    errors.Add($"Instance '{instance.instanceId}' disabled: {problem}");
                }
                else
                {
                    if (active && instance.occupiesCell) positions.Add(instance.cellPosition);
                    if (active && instance.status != EventStatus.Available) foreach (var id in instance.assignedCardIds.Where(id => !string.IsNullOrEmpty(id))) assigned.Add(id);
                    if (instance.status == EventStatus.Running) instance.status = EventStatus.Ready;
                }
            }
            foreach (var previous in instances.Values) ClearCell(previous);
            if (map?.mapGrid != null) foreach (var cell in map.mapGrid) if (cell != null) cell.eventInstanceId = null;
            instances.Clear();
            unlocked.Clear();
            foreach (var id in data.unlockedDefinitionIds ?? new List<string>()) if (Catalog.TryGetById(id, out _)) unlocked.Add(id);
            Progress = data.version >= 2 && data.progress != null ? data.progress.Copy() : new EventProgress();
            CurrentPhase = data.currentPhase;
            foreach (var item in restored)
            {
                instances.Add(item.Key, item.Value);
                var instance = item.Value;
                if (instance.occupiesCell && instance.status != EventStatus.Cancelled && instance.status != EventStatus.Resolved && TryCell(instance.cellPosition, out var cell)) cell.eventInstanceId = instance.instanceId;
            }
            Restored?.Invoke();
            AdvancePhase(CurrentPhase);
            foreach (var instance in instances.Values.ToArray()) Changed?.Invoke(instance);
            foreach (var error in errors) Debug.LogError($"[EventManager.Restore] {error}");
            return errors;
        }

        internal static bool Fail(string message, out string error) { error = message; return false; }
    }
}
