using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public sealed partial class EventManager
    {
        public EventProgress Progress { get; private set; } = new EventProgress();
        public bool CanAppear(EventDefinitionSO definition) => definition != null && (!definition.useExtendedLifecycle || definition.extension != null && definition.extension.conditions != null && definition.extension.conditions.All(c => c != null && Evaluate(c.query)));
        public bool Evaluate(EventQuery q)
        {
            if (q == null || !q.HasTarget || !Enum.IsDefined(typeof(Comparison), q.comparison)) return false;
            var actors = cards as IEventParticipantSource;
            bool Compare(int a, int b)
            {
                switch (q.comparison) { case Comparison.Equal: return a == b; case Comparison.NotEqual: return a != b; case Comparison.GreaterOrEqual: return a >= b; case Comparison.Greater: return a > b; case Comparison.LessOrEqual: return a <= b; case Comparison.Less: return a < b; default: return false; }
            }
            bool Actor(string id) => actors != null && actors.TryCharacter(id, q.parameter, out var v) && Compare(v, q.value);
            switch (q.source)
            {
                case ConditionSource.Character: return actors != null && actors.Characters(q.character).Where(id => string.IsNullOrEmpty(q.characterInstanceId) || id == q.characterInstanceId).Any(Actor);
                case ConditionSource.Facility: return Compare(Progress.Relation(q.facility.PersistentId), q.value);
                case ConditionSource.Flag: return Compare(Progress.Flag(q.flag.PersistentId) ? 1 : 0, q.boolValue ? 1 : 0);
                case ConditionSource.Hint: return Compare(Progress.HintLevel(q.hint.PersistentId) > 0 ? 1 : 0, q.boolValue ? 1 : 0);
                case ConditionSource.World: return Compare(CurrentPhase % 2, (int)q.phase);
                case ConditionSource.EventHistory:
                case ConditionSource.EventParticipant:
                    var records = Progress.history.Where(h => h.resolved && h.definitionId == q.eventDefinition.PersistentId).ToArray();
                    if (records.Length == 0) return false;
                    bool Match(EventHistoryRecord h) => q.source == ConditionSource.EventHistory ? Compare((int)h.outcome, (int)q.outcome) : h.participantIds.Any(Actor);
                    switch (q.historyMode) { case HistoryMode.Latest: return Match(records[records.Length - 1]); case HistoryMode.Any: return records.Any(Match); case HistoryMode.All: return records.All(Match); default: return false; }
                default: return false;
            }
        }
        public bool TryBuildAssignments(EventDefinitionSO definition, IReadOnlyList<string> ids, out List<string> result, out string error)
            => TryBuildAssignments(null, definition, ids, out result, out error);
        public bool TryBuildAssignments(EventInstance instance, EventDefinitionSO definition, IReadOnlyList<string> ids, out List<string> result, out string error)
        {
            result = ids == null ? new List<string>() : new List<string>(ids);
            if (definition?.reservation?.cardSlots == null || result.Count != definition.reservation.cardSlots.Count) return Fail("Card slot count does not match.", out error);
            for (int i = 0; i < result.Count; i++)
            {
                var slot = definition.reservation.cardSlots[i];
                if (slot == null) return Fail("Missing slot.", out error);
                if (definition.useExtendedLifecycle && slot.historyParticipant?.enabled == true)
                {
                    if (!AssignHistorySlot(instance, slot, result, i, out error)) return false;
                    continue;
                }
                if (!definition.useExtendedLifecycle || slot.slotType != SlotType.Involved) continue;
                var automatic = (cards as IEventParticipantSource)?.FindCard(slot.involvedTarget);
                if (string.IsNullOrEmpty(automatic)) return Fail("Involved target has no available card.", out error);
                if (!string.IsNullOrEmpty(result[i]) && result[i] != automatic) return Fail("Involved participant cannot be replaced.", out error);
                result[i] = automatic;
            }
            error = null; return true;
        }
        private bool StartExtended(EventInstance instance, EventDefinitionSO definition, IReadOnlyList<string> ids, int phase, out string error)
        {
            var config = definition.extension;
            if (definition.execution?.executionType != EventExecutionType.Narrative || !definition.enabled || config == null || config.durationPhaseCount < 0 || phase != CurrentPhase || (long)phase + config.durationPhaseCount > int.MaxValue) return Fail("Invalid event duration/current phase.", out error);
            if (!TryBuildAssignments(instance, definition, ids, out var assignments, out error) || !ValidateCards(definition, assignments, out error)) return false;
            instance.assignedCardIds = assignments;
            instance.slotAssignments = CaptureSlots(definition, assignments);
            instance.participantIds = (cards as IEventParticipantSource)?.Participants(assignments).Distinct().ToList() ?? new List<string>();
            instance.reservedPhase = phase;
            instance.executePhase = phase + config.durationPhaseCount;
            instance.status = config.durationPhaseCount == 0 ? EventStatus.Ready : EventStatus.Reserved;
            Notify(instance); error = null; return true;
        }
        private void Expire(EventInstance instance)
        {
            instance.participantIds.Clear(); instance.assignedCardIds.Clear();
            instance.slotAssignments.Clear();
            instance.hasJudgement = true; instance.resolvedOutcomeCode = OutcomeType.EXPIRED.ToString();
            instance.status = EventStatus.Ready; Notify(instance);
        }
        internal void CalculateHintLevel(EventInstance instance, EventDefinitionSO definition)
        {
            if (!definition.useExtendedLifecycle || instance.hintLevelCalculated) return;
            long points = 0;
            foreach (var condition in definition.extension.pointConditions ?? new List<PointCondition>())
                if (condition != null && Evaluate(condition.query)) points += condition.addPoint;
            instance.hintPoints = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, points));
            instance.hintLevel = points >= definition.extension.level3Points ? 3 : points >= definition.extension.level2Points ? 2 : 1;
            instance.hintLevelCalculated = true;
        }
        internal void RecordHistory(EventInstance instance)
        {
            if (Progress.history.Any(h => h.instanceId == instance.instanceId)) return;
            if (!Enum.TryParse(instance.resolvedOutcomeCode, out OutcomeType outcome)) return;
            Progress.history.Add(new EventHistoryRecord { instanceId = instance.instanceId, definitionId = instance.definitionId, sourceEventInstanceId = instance.sourceEventInstanceId, slotAssignments = EventSlotAssignment.CopyList(instance.slotAssignments), resolved = true, outcome = outcome, participantIds = new List<string>(instance.participantIds) });
        }
        internal bool ScheduleFollowUp(EventInstance parent, EventFollowUp followUp, out string error)
        {
            error = null;
            var target = followUp?.definition;
            if (target == null || !target.enabled || !Catalog.TryGetById(target.PersistentId, out var registered) || registered != target || followUp.delayPhaseCount < 0 || (long)CurrentPhase + followUp.delayPhaseCount > int.MaxValue) return Fail("Invalid FollowUp reference or delay.", out error);
            if (followUp.delayPhaseCount == 0 && target.useExtendedLifecycle && target.extension?.deadlinePhaseCount == 0 && (parent.definitionId == target.PersistentId || parent.immediateAncestors.Contains(target.PersistentId) || parent.immediateAncestors.Count >= 32)) return Fail("Immediate expiry FollowUp cycle was blocked.", out error);
            string id = parent.instanceId + ":followup:" + parent.appliedFollowUpCount;
            if (Progress.followUps.Any(f => f.id == id)) return true;
            if (!followUp.allowConcurrent && (Progress.followUps.Any(f => f.definitionId == target.PersistentId) || instances.Values.Any(i => i.instanceId != parent.instanceId && i.definitionId == target.PersistentId && i.status != EventStatus.Resolved && i.status != EventStatus.Cancelled))) return true;
            Progress.followUps.Add(new ScheduledFollowUp { id = id, parentId = parent.instanceId, definitionId = target.PersistentId, scheduledPhase = CurrentPhase, duePhase = CurrentPhase + followUp.delayPhaseCount, allowConcurrent = followUp.allowConcurrent, preferredCell = parent.cellPosition });
            return true;
        }
        internal void ProcessFollowUps()
        {
            // Snapshot: newly generated zero-deadline events are drained by Runner on later frames.
            foreach (var pending in Progress.followUps.Where(f => f.duePhase <= CurrentPhase).ToArray())
            {
                if (!Catalog.TryGetById(pending.definitionId, out var target) || !target.enabled || !CanAppear(target)) continue;
                if (!pending.allowConcurrent && instances.Values.Any(i => i.definitionId == pending.definitionId && i.status != EventStatus.Resolved && i.status != EventStatus.Cancelled)) { Progress.followUps.Remove(pending); continue; }
                if (!FindFreeCell(pending.preferredCell, out var position)) continue;
                var child = Spawn(target, position, pending.parentId);
                if (child != null)
                {
                    if (pending.scheduledPhase == pending.duePhase && pending.duePhase == CurrentPhase && TryGet(pending.parentId, out var parent)) child.immediateAncestors = new List<string>(parent.immediateAncestors) { parent.definitionId };
                    Progress.followUps.Remove(pending);
                }
            }
        }
    }
}
