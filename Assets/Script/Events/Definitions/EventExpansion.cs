using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicMap.Events
{
    public enum SlotType { Required, Optional, Choice, Involved }
    public enum OutcomeType { SUCCESS, FAILURE, EXPIRED }
    public enum ConditionSource { Character, Facility, Flag, Hint, World, EventHistory, EventParticipant }
    public enum HistoryMode { Latest, Any, All }
    public enum Comparison { Equal, NotEqual, GreaterOrEqual, Greater, LessOrEqual, Less }
    public enum WorldPhase { Day, Night }
    public enum FlagScope { Global, Case }

    [Serializable]
    public sealed class EventQuery
    {
        [EnumToggleButtons] public ConditionSource source;
        [ShowIf("@source == ConditionSource.Character")] public CharaData character;
        [ShowIf("@source == ConditionSource.Character"), ValueDropdown(nameof(CharacterInstances)), LabelText("Character instance (optional)")] public string characterInstanceId;
        [ShowIf("@source == ConditionSource.Facility")] public FacilityDefinitionSO facility;
        [ShowIf("@source == ConditionSource.Flag")] public FlagDefinitionSO flag;
        [ShowIf("@source == ConditionSource.Hint")] public HintData hint;
        [ShowIf("@source == ConditionSource.EventHistory || source == ConditionSource.EventParticipant")] public EventDefinitionSO eventDefinition;
        [ShowIf("@source == ConditionSource.EventHistory || source == ConditionSource.EventParticipant")] public HistoryMode historyMode;
        [ShowIf("@source == ConditionSource.Character || source == ConditionSource.EventParticipant"), LabelText("Property (Appeal = Charm)")] public EventParameter parameter = EventParameter.Appeal;
        [ShowInInspector, ReadOnly] public string Property => source == ConditionSource.Facility ? "Relation" : source == ConditionSource.Flag ? "Value" : source == ConditionSource.Hint ? "Owned" : source == ConditionSource.World ? "CurrentPhase" : source == ConditionSource.EventHistory ? "Outcome" : parameter.ToString();
        [ValueDropdown(nameof(Operators))] public Comparison comparison;
        [ShowIf("@source == ConditionSource.Character || source == ConditionSource.Facility || source == ConditionSource.EventParticipant")] public int value;
        [ShowIf("@source == ConditionSource.Flag || source == ConditionSource.Hint")] public bool boolValue = true;
        [ShowIf("@source == ConditionSource.World")] public WorldPhase phase;
        [ShowIf("@source == ConditionSource.EventHistory")] public OutcomeType outcome;
        private IEnumerable<Comparison> Operators => source == ConditionSource.Character || source == ConditionSource.Facility || source == ConditionSource.EventParticipant ? (Comparison[])Enum.GetValues(typeof(Comparison)) : new[] { Comparison.Equal, Comparison.NotEqual };
        private IEnumerable<ValueDropdownItem<string>> CharacterInstances
        {
            get
            {
                yield return new ValueDropdownItem<string>("Any matching character", "");
                foreach (var party in UnityEngine.Object.FindObjectsByType<PartyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (party.Runtime != null) foreach (var member in party.Runtime.Members.Where(m => m != null && m.baseData == character))
                        yield return new ValueDropdownItem<string>(member.charaName + " / " + member.runtimeId, member.runtimeId);
            }
        }
        public bool HasTarget => source == ConditionSource.World || source == ConditionSource.Character && character != null || source == ConditionSource.Facility && facility != null || source == ConditionSource.Flag && flag != null || source == ConditionSource.Hint && hint != null || (source == ConditionSource.EventHistory || source == ConditionSource.EventParticipant) && eventDefinition != null;
    }
    [Serializable] public sealed class OccurrenceCondition { [HideLabel, InlineProperty] public EventQuery query = new EventQuery(); }
    [Serializable] public sealed class PointCondition { [HideLabel, InlineProperty] public EventQuery query = new EventQuery(); public int addPoint = 1; }

    [Serializable]
    public sealed class EventExpansion
    {
        [FoldoutGroup("Conditions"), GUIColor(.65f,.8f,1)] public List<OccurrenceCondition> conditions = new List<OccurrenceCondition>();
        [FoldoutGroup("Time"), GUIColor(1,.95f,.6f), Min(0)] public int deadlinePhaseCount = 2;
        [FoldoutGroup("Time"), Min(0)] public int durationPhaseCount = 1;
        [FoldoutGroup("Hint Level"), GUIColor(.85f,.65f,1)] public List<PointCondition> pointConditions = new List<PointCondition>();
        [FoldoutGroup("Hint Level")] public int level2Points = 2;
        [FoldoutGroup("Hint Level")] public int level3Points = 4;
        [FoldoutGroup("Hint Level")] public HintPoolSO randomHintPool;
        [TabGroup("Outcomes", "SUCCESS"), GUIColor(1,.7f,.7f)] public EventOutcomeDefinition success = new EventOutcomeDefinition { outcomeCode = "SUCCESS" };
        [TabGroup("Outcomes", "FAILURE")] public EventOutcomeDefinition failure = new EventOutcomeDefinition { outcomeCode = "FAILURE" };
        [TabGroup("Outcomes", "EXPIRED")] public EventOutcomeDefinition expired = new EventOutcomeDefinition { outcomeCode = "EXPIRED" };
        public IEnumerable<EventOutcomeDefinition> Outcomes => new[] { success, failure, expired };
    }

    [Serializable] public abstract class OutcomeEffect { public abstract bool Apply(EventEffectContext context, out string error); }
    [Serializable] public sealed class GoldEffect : OutcomeEffect
    {
        public int amount = 1;
        public override bool Apply(EventEffectContext c, out string error) => c.Legacy(new EventEffect { kind = amount < 0 ? EventEffectKind.ConsumeGold : EventEffectKind.GrantGold, amount = amount < 0 ? (amount == int.MinValue ? -1 : -amount) : amount }, out error);
    }
    [Serializable] public sealed class ItemEffect : OutcomeEffect
    {
        public EquipData item; [Min(1)] public int amount = 1;
        public override bool Apply(EventEffectContext c, out string error) => c.Legacy(new EventEffect { kind = EventEffectKind.GrantEquip, equip = item, amount = amount }, out error);
    }
    [Serializable] public sealed class FlagEffect : OutcomeEffect
    {
        public FlagDefinitionSO flag; public bool value = true;
        public override bool Apply(EventEffectContext c, out string error)
        { error = null; if (flag == null) return EventManager.Fail("Flag is missing.", out error); c.manager.Progress.SetFlag(flag.PersistentId, value); return true; }
    }
    [Serializable] public sealed class RelationEffect : OutcomeEffect
    {
        public FacilityDefinitionSO facility; public int amount = 1;
        public override bool Apply(EventEffectContext c, out string error)
        { error = null; if (facility == null) return EventManager.Fail("Facility is missing.", out error); return c.manager.Progress.AddRelation(facility.PersistentId, amount, out error); }
    }
    [Serializable] public sealed class HintEffect : OutcomeEffect
    {
        public bool isFixedHint = true;
        [ShowIf(nameof(isFixedHint))] public HintData fixedHint;
        public override bool Apply(EventEffectContext c, out string error) => c.GrantHint(this, out error);
    }
}
