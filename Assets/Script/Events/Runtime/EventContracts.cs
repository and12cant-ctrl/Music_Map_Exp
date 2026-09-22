using System;
using System.Collections.Generic;

namespace MusicMap.Events
{
    public interface IEventCardSource
    {
        bool TryGetCard(string cardId, out CardType type, out int amount);
        bool TryGetParameter(IReadOnlyList<string> cardIds, EventParameter parameter, out int value);
    }

    // Implement on a component and assign to EventSystemRoot. No invented flag/inventory stores.
    public interface IEventWorldState
    {
        bool TryGetFlag(string key, out bool value);
        bool TryGetResource(string key, out int value);
        bool TrySetFlag(string key, bool value, string operationId, out string error);
    }

    public interface IEventExecutionHandler
    {
        EventExecutionType ExecutionType { get; }
        // Return false without side effects if unavailable. Invoke completion once after closing the dedicated UI.
        // operationId must be idempotent across save/load (also used for battle effects).
        bool TryBegin(EventDefinitionSO definition, EventInstance instance, string externalKey, string operationId,
            Action<bool, string> completion, out string error);
    }

    public sealed class EventContext
    {
        public EventInstance instance;
        public IEventCardSource cards;
        public IEventWorldState world;

        public bool Matches(IEnumerable<EventCondition> conditions)
        {
            if (conditions == null) return false;
            foreach (var condition in conditions) if (condition == null || !Matches(condition)) return false;
            return true;
        }

        private bool Matches(EventCondition condition)
        {
            switch (condition.kind)
            {
                case EventConditionKind.Always: return true;
                case EventConditionKind.JudgeSucceeded: return instance != null && instance.hasJudgement && instance.judgeSucceeded;
                case EventConditionKind.JudgeFailed: return instance != null && instance.hasJudgement && !instance.judgeSucceeded;
                case EventConditionKind.TotalAtLeast: return instance != null && instance.hasJudgement && instance.judgeTotal >= condition.value;
                case EventConditionKind.TotalBelow: return instance != null && instance.hasJudgement && instance.judgeTotal < condition.value;
                case EventConditionKind.FlagEquals: return world != null && world.TryGetFlag(condition.key, out var flag) && flag == condition.flagValue;
                case EventConditionKind.ResourceAtLeast: return world != null && world.TryGetResource(condition.key, out var resource) && resource >= condition.value;
                case EventConditionKind.ParameterAtLeast: return cards != null && cards.TryGetParameter(instance?.assignedCardIds ?? new List<string>(), condition.parameter, out var value) && value >= condition.value;
                default: return false;
            }
        }
    }
}
