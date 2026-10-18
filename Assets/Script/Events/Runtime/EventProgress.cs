using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public interface IEventParticipantSource
    {
        IEnumerable<string> Participants(IReadOnlyList<string> cardIds);
        bool TryCharacter(string instanceId, EventParameter parameter, out int value);
        IEnumerable<string> Characters(CharaData definition);
        string FindCard(UnityEngine.Object target);
    }
    [Serializable] public sealed class EventHistoryRecord
    {
        public string definitionId, instanceId;
        public string sourceEventInstanceId;
        public List<EventSlotAssignment> slotAssignments = new List<EventSlotAssignment>();
        public bool resolved;
        public OutcomeType outcome;
        public List<string> participantIds = new List<string>();
    }
    [Serializable] public sealed class FlagState { public string definitionId; public bool value; }
    [Serializable] public sealed class HintOwnership { public string hintId; public int level; }
    [Serializable] public sealed class HintDrawHistory { public string poolId; public List<string> drawn = new List<string>(); }
    [Serializable] public sealed class FacilityState { public string definitionId; public int relation; }
    [Serializable] public sealed class ScheduledFollowUp
    {
        public string id, definitionId, parentId;
        public int duePhase;
        public int scheduledPhase;
        public bool allowConcurrent;
        public Vector2Int preferredCell;
    }
    [Serializable] public sealed class EventProgress
    {
        public List<EventHistoryRecord> history = new List<EventHistoryRecord>();
        public List<FlagState> flags = new List<FlagState>();
        public List<HintOwnership> hints = new List<HintOwnership>();
        public List<HintDrawHistory> draws = new List<HintDrawHistory>();
        public List<FacilityState> facilities = new List<FacilityState>();
        public List<ScheduledFollowUp> followUps = new List<ScheduledFollowUp>();
        public EventProgress Copy() => JsonUtility.FromJson<EventProgress>(JsonUtility.ToJson(this));
        public bool IsValid()
        {
            if (history == null || flags == null || hints == null || draws == null || facilities == null || followUps == null) return false;
            if (history.Any(h => h != null && !EventSlotAssignment.IsValid(h.slotAssignments))) return false;
            return history.All(h => h != null && !string.IsNullOrEmpty(h.definitionId) && !string.IsNullOrEmpty(h.instanceId) && h.participantIds != null && h.participantIds.All(id => !string.IsNullOrEmpty(id)) && Enum.IsDefined(typeof(OutcomeType), h.outcome))
                && history.Select(h => h.instanceId).Distinct().Count() == history.Count
                && flags.All(f => f != null && !string.IsNullOrEmpty(f.definitionId)) && flags.Select(f => f.definitionId).Distinct().Count() == flags.Count
                && hints.All(h => h != null && !string.IsNullOrEmpty(h.hintId) && h.level >= 1 && h.level <= 3) && hints.Select(h => h.hintId).Distinct().Count() == hints.Count
                && draws.All(d => d != null && !string.IsNullOrEmpty(d.poolId) && d.drawn != null) && draws.Select(d => d.poolId).Distinct().Count() == draws.Count
                && facilities.All(f => f != null && !string.IsNullOrEmpty(f.definitionId)) && facilities.Select(f => f.definitionId).Distinct().Count() == facilities.Count
                && followUps.All(f => f != null && !string.IsNullOrEmpty(f.id) && !string.IsNullOrEmpty(f.definitionId) && f.duePhase >= 0) && followUps.Select(f => f.id).Distinct().Count() == followUps.Count;
        }
        public bool Flag(string id) => flags.Any(f => f.definitionId == id && f.value);
        public void SetFlag(string id, bool value)
        { var state = flags.FirstOrDefault(f => f.definitionId == id); if (state == null) flags.Add(state = new FlagState { definitionId = id }); state.value = value; }
        public int Relation(string id) => facilities.FirstOrDefault(f => f.definitionId == id)?.relation ?? 0;
        public bool AddRelation(string id, int amount, out string error)
        {
            long total = (long)Relation(id) + amount;
            if (total < int.MinValue || total > int.MaxValue) return EventManager.Fail("Relation overflow.", out error);
            var state = facilities.FirstOrDefault(f => f.definitionId == id);
            if (state == null) facilities.Add(state = new FacilityState { definitionId = id });
            state.relation = (int)total; error = null; return true;
        }
        public int HintLevel(string id) => hints.FirstOrDefault(h => h.hintId == id)?.level ?? 0;
        public void GrantHint(string id, int level)
        {
            var state = hints.FirstOrDefault(h => h.hintId == id);
            if (state == null) hints.Add(state = new HintOwnership { hintId = id });
            state.level = Math.Max(state.level, Mathf.Clamp(level, 1, 3));
        }
        public HintData Draw(HintPoolSO pool, int seed)
        {
            if (pool == null) return null;
            var candidates = pool.hints?.Where(h => h != null && !string.IsNullOrEmpty(h.PersistentId)).GroupBy(h => h.PersistentId).Select(g => g.First()).ToArray();
            if (candidates == null || candidates.Length == 0) return null;
            var state = draws.FirstOrDefault(d => d.poolId == pool.PersistentId);
            if (state == null) draws.Add(state = new HintDrawHistory { poolId = pool.PersistentId });
            var remaining = candidates.Where(h => !state.drawn.Contains(h.PersistentId)).ToArray();
            if (remaining.Length == 0) { state.drawn.Clear(); remaining = candidates; }
            var chosen = remaining[(int)(unchecked((uint)seed) % (uint)remaining.Length)];
            state.drawn.Add(chosen.PersistentId); return chosen;
        }
    }
    public sealed class EventEffectContext
    {
        public EventManager manager;
        public EventDefinitionSO definition;
        public EventInstance instance;
        public EventEffectExecutor executor;
        public PartyManager party;
        public string operationId;
        public bool Legacy(EventEffect effect, out string error) => executor.TryApply(definition, instance, effect, operationId, (_, __) => { }, out error);
        public bool GrantHint(HintEffect effect, out string error)
        {
            var hint = effect.isFixedHint ? effect.fixedHint : manager.Progress.Draw(definition.extension.randomHintPool, unchecked(instance.randomSeed + instance.appliedEffectCount));
            if (hint == null || string.IsNullOrEmpty(hint.PersistentId)) return EventManager.Fail("Hint or case hint pool is missing/empty.", out error);
            manager.Progress.GrantHint(hint.PersistentId, instance.hintLevel);
            if (party?.Runtime != null && !party.Runtime.Hints.Contains(hint)) party.AddHint(hint);
            error = null; return true;
        }
    }
}
