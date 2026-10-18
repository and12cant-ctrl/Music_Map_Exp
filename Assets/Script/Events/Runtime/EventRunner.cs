using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicMap.Events
{
    public sealed class EventRunner : IDisposable
    {
        private readonly EventManager manager;
        private readonly IEventCardSource cards;
        private readonly IEventWorldState world;
        private readonly EventEffectExecutor effects;
        private readonly Func<EventExecutionType, IEventExecutionHandler> handlers;
        private readonly HashSet<string> queue = new HashSet<string>(StringComparer.Ordinal);
        private EventDefinitionSO currentDefinition;
        private bool operationPending;
        private int generation;
        public EventInstance Current { get; private set; }
        public bool HasQueuedEvents => queue.Count > 0;
        public bool IsOperationPending => operationPending;
        public bool CanPresentNarrative { get; set; }
        public event Action<EventInstance, EventDefinitionSO, EventPresentation> PresentationRequested;
        public event Action<string> Failed;
        public event Action Closed;

        public EventRunner(EventManager manager, IEventCardSource cards, IEventWorldState world,
            EventEffectExecutor effects, Func<EventExecutionType, IEventExecutionHandler> handlers)
        {
            this.manager = manager; this.cards = cards; this.world = world; this.effects = effects; this.handlers = handlers;
            manager.Changed += OnChanged;
            manager.Restored += OnRestore;
            foreach (var instance in manager.Instances) OnChanged(instance);
        }

        private void OnChanged(EventInstance instance)
        {
            if (instance.status == EventStatus.Ready) queue.Add(instance.instanceId);
            else queue.Remove(instance.instanceId);
        }

        private void OnRestore()
        {
            generation++;
            Current = null;
            currentDefinition = null;
            operationPending = false;
            autoApply = false;
            queue.Clear();
            Closed?.Invoke();
        }

        // Called once per frame by the root, not by a timer/coroutine or recursively from completion.
        public void Pump()
        {
            if (Current != null || queue.Count == 0) return;
            var next = queue.Select(id => manager.TryGet(id, out var instance) ? instance : null)
                .Where(i => i != null && i.status == EventStatus.Ready)
                .OrderByDescending(i => manager.Catalog.TryGetById(i.definitionId, out var d) ? d.priority : 0)
                .ThenBy(i => i.reservedPhase).ThenBy(i => i.instanceId, StringComparer.Ordinal).FirstOrDefault();
            if (next == null) { queue.Clear(); return; }
            queue.Remove(next.instanceId);
            if (!manager.TryGetDefinition(next.instanceId, out _, out var definition, out var error)) { Report(next, error); return; }
            if (definition.execution == null || definition.reservation == null) { Report(next, "Event execution/reservation definition is missing."); return; }
            var handler = definition.execution.executionType == EventExecutionType.Narrative ? null : handlers?.Invoke(definition.execution.executionType);
            if (definition.execution.executionType == EventExecutionType.Narrative && !CanPresentNarrative && next.resolvedOutcomeCode != "EXPIRED")
            { Report(next, "ExecutionView is not connected. Select the event again after connecting it."); return; }
            if (definition.execution.executionType != EventExecutionType.Narrative && handler == null)
            { Report(next, $"{definition.execution.executionType} handler is not connected. Event remains Ready."); return; }
            Current = next;
            currentDefinition = definition;
            next.status = EventStatus.Running;
            next.lastError = null;
            manager.Notify(next);
            if (next.resolvedOutcomeCode == "EXPIRED") { ApplyNext(); return; }
            if (definition.execution.executionType == EventExecutionType.Narrative)
            {
                var outcome = GetStoredOutcome();
                PresentationRequested?.Invoke(next, definition, next.hasJudgement && outcome != null ? outcome.presentation : definition.execution.beforeJudge);
                return;
            }
            if (!string.IsNullOrEmpty(next.resolvedOutcomeCode)) { ApplyNext(); return; }
            operationPending = true;
            int token = generation;
            bool completed = false;
            bool started = handler.TryBegin(definition, next.Copy(), definition.execution.externalKey, next.instanceId + ":execution", (success, outcomeCode) =>
            {
                if (completed || token != generation || Current != next) return;
                completed = true;
                operationPending = false;
                if (!success)
                {
                    next.status = EventStatus.Ready;
                    Current = null;
                    currentDefinition = null;
                    Report(next, outcomeCode ?? "External event was not completed. Select it to retry.");
                    Closed?.Invoke();
                    return;
                }
                var outcome = definition.EffectiveOutcomes?.FirstOrDefault(o => o != null && o.outcomeCode == outcomeCode)
                    ?? definition.EffectiveOutcomes?.FirstOrDefault(o => o != null && o.isDefault);
                if (outcome == null) { Report(next, "External execution has no valid default outcome."); return; }
                next.resolvedOutcomeCode = outcome.outcomeCode;
                next.hasJudgement = true;
                ApplyNext();
            }, out error);
            if (!started && !completed)
            {
                operationPending = false;
                next.status = EventStatus.Ready;
                Current = null;
                currentDefinition = null;
                Report(next, error ?? "External handler could not start.");
                Closed?.Invoke();
            }
        }

        public void EnqueueReady(string id) { if (manager.TryGet(id, out var instance) && instance.status == EventStatus.Ready) queue.Add(id); }

        public void Continue()
        {
            if (Current == null || operationPending) return;
            if (!Current.hasJudgement && currentDefinition.execution.executionType == EventExecutionType.Narrative)
            {
                var judge = currentDefinition.execution.judge;
                if (judge == null || judge.diceCount < 1 || judge.diceCount > 100 || judge.diceSides < 2 || judge.diceSides > 10000)
                { Report(Current, "Invalid dice definition."); return; }
                int value = 0;
                if (Current.assignedCardIds.Any(id => !string.IsNullOrEmpty(id)) && (cards == null || !cards.TryGetParameter(Current.assignedCardIds, judge.parameter, out value)))
                { Report(Current, "Assigned cards could not be restored; judgement was not rolled."); return; }
                // Fixed algorithm and seed: independent of UnityEngine.Random and framework changes.
                uint state = unchecked((uint)Current.randomSeed);
                var rolls = new List<int>();
                if (judge.useDice) for (int index = 0; index < judge.diceCount; index++)
                {
                    state = unchecked(state * 1664525u + 1013904223u);
                    rolls.Add((int)(state % (uint)judge.diceSides) + 1);
                }
                Current.diceResults = rolls;
                Current.judgeTotal = value + judge.modifier + rolls.Sum();
                Current.judgeSucceeded = Current.judgeTotal >= judge.target;
                Current.hasJudgement = true;
                var context = new EventContext { instance = Current, cards = cards, world = world };
                var outcome = currentDefinition.useExtendedLifecycle ? currentDefinition.EffectiveOutcomes.FirstOrDefault(o => o != null && o.outcomeCode == (Current.judgeSucceeded ? "SUCCESS" : "FAILURE")) : currentDefinition.EffectiveOutcomes?.FirstOrDefault(o => o != null && !o.isDefault && context.Matches(o.conditions))
                    ?? currentDefinition.EffectiveOutcomes?.FirstOrDefault(o => o != null && o.isDefault);
                if (outcome == null)
                {
                    Current.hasJudgement = false;
                    Report(Current, "No matching or default outcome. Result was not applied.");
                    return;
                }
                Current.resolvedOutcomeCode = outcome.outcomeCode;
                manager.Notify(Current);
                PresentationRequested?.Invoke(Current, currentDefinition, outcome.presentation);
                return;
            }
            ApplyNext();
        }

        private EventOutcomeDefinition GetStoredOutcome() => currentDefinition?.EffectiveOutcomes?.FirstOrDefault(o => o != null && o.outcomeCode == Current?.resolvedOutcomeCode);

        private void ApplyNext()
        {
            var instance = Current;
            var outcome = GetStoredOutcome();
            manager.CalculateHintLevel(instance, currentDefinition);
            if (outcome == null || outcome.effects == null || outcome.followUps == null) { Report(instance, "Saved outcome or result lists are missing from the definition."); return; }
            if (instance.appliedEffectCount > outcome.EffectCount || instance.appliedFollowUpCount > outcome.followUps.Count)
            { Report(instance, "Result definition changed since save; result cursor cannot be resumed."); return; }
            if (instance.appliedEffectCount < outcome.EffectCount)
            {
                if (effects == null) { Report(instance, "EventEffectExecutor is not connected."); return; }
                operationPending = true;
                int token = generation;
                bool completed = false;
                bool started = effects.TryApplyOutcome(currentDefinition, instance, outcome,
                    instance.instanceId + ":effect:" + instance.appliedEffectCount, (success, message) =>
                    {
                        if (completed || token != generation || Current != instance) return;
                        completed = true;
                        operationPending = false;
                        if (!success) { Report(instance, message ?? "Effect failed."); return; }
                        instance.appliedEffectCount++;
                        manager.Notify(instance);
                        // Do not recursively drain effects: the next frame or Continue handles the next step.
                        autoApply = true;
                    }, out var error);
                if (!started && !completed) { operationPending = false; Report(instance, error); }
                return;
            }
            if (currentDefinition.useExtendedLifecycle) manager.RecordHistory(instance);
            while (instance.appliedFollowUpCount < outcome.followUps.Count)
            {
                var followUp = outcome.followUps[instance.appliedFollowUpCount];
                if (followUp == null) { Report(instance, "Null follow-up."); return; }
                string error;
                if (!(currentDefinition.useExtendedLifecycle ? manager.ScheduleFollowUp(instance, followUp, out error) : manager.StartFollowUp(instance, followUp.definition, followUp.startMode, out error))) { Report(instance, error); return; }
                instance.appliedFollowUpCount++;
                manager.Notify(instance);
            }
            manager.Resolve(instance);
            manager.ProcessFollowUps();
            Current = null;
            currentDefinition = null;
            Closed?.Invoke();
        }

        private bool autoApply;
        public void Tick()
        {
            if (autoApply && !operationPending) { autoApply = false; if (Current != null) ApplyNext(); }
            Pump();
        }

        private void Report(EventInstance instance, string error)
        {
            if (instance != null) instance.lastError = error;
            UnityEngine.Debug.LogError($"[EventRunner:{instance?.instanceId}] {error}");
            Failed?.Invoke(error);
        }

        public void Dispose() { generation++; manager.Changed -= OnChanged; manager.Restored -= OnRestore; }
    }
}
