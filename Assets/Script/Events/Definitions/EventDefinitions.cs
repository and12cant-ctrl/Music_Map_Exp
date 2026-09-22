using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicMap.Events
{
    public enum EventTriggerMode { Scheduled, Immediate }
    public enum EventExecutionType { Narrative, Shop, Offering, Encounter }
    public enum EventStatus { Available, Reserved, Ready, Running, Resolved, Cancelled }
    public enum EventFollowUpMode { Unlock, SpawnAvailable, Reserve }
    public enum EventConditionKind { Always, JudgeSucceeded, JudgeFailed, TotalAtLeast, TotalBelow, FlagEquals, ParameterAtLeast, ResourceAtLeast }
    public enum EventEffectKind { GrantGold, ConsumeGold, GrantAbility, GrantEquip, GrantHint, ChangeParameter, SetFlag, UnlockEvent, ReserveEvent, StartBattle }
    public enum EventParameter { Strength, Intelligence, Appeal, Social, Survival, Mystery, HP, MP, SP }

    [Serializable]
    public sealed class EventPresentation
    {
        [TextArea(3, 12)] public string text;
        public Sprite image;
    }

    [Serializable]
    public sealed class EventAppearanceDefinition
    {
        public Sprite mapIcon;
        public string mapColorKey;
        public bool generateOnMap = true;
        public bool requiresUnlock;
        [Min(0)] public int requiredPlacements;
        [Min(0)] public float weight = 1;
        public List<string> backgroundCategories = new List<string>();
        public List<EventCondition> conditions = new List<EventCondition>();
    }

    [Serializable]
    public sealed class EventReservationDefinition
    {
        public EventTriggerMode triggerMode = EventTriggerMode.Scheduled;
        [Min(0)] public int requiredPhaseCount = 1;
        public EventPresentation reservationPresentation = new EventPresentation();
        public List<CardSlotDefinition> cardSlots = new List<CardSlotDefinition>();
        public List<EventRequirement> requirements = new List<EventRequirement>();
    }

    [Serializable]
    public sealed class CardSlotDefinition
    {
        public string label;
        public CardType acceptedType = CardType.Chara;
        public bool required = true;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public sealed class EventRequirement
    {
        public EventParameter parameter;
        [Min(0)] public int minimum;
    }

    [Serializable]
    public sealed class EventExecutionDefinition
    {
        public EventExecutionType executionType = EventExecutionType.Narrative;
        public EventPresentation beforeJudge = new EventPresentation();
        public EventJudgeDefinition judge = new EventJudgeDefinition();
        [Tooltip("Identifier understood by the connected shop/offering/battle system.")]
        public string externalKey;
    }

    [Serializable]
    public sealed class EventJudgeDefinition
    {
        public bool useDice = true;
        public EventParameter parameter = EventParameter.Appeal;
        [Min(1)] public int diceCount = 1;
        [Min(2)] public int diceSides = 6;
        public int target = 4;
        public int modifier;
    }

    [Serializable]
    public sealed class EventCondition
    {
        public EventConditionKind kind;
        public EventParameter parameter;
        public string key;
        public int value;
        public bool flagValue = true;
    }

    [Serializable]
    public sealed class EventOutcomeDefinition
    {
        public string outcomeCode;
        public bool isDefault;
        public List<EventCondition> conditions = new List<EventCondition>();
        public EventPresentation presentation = new EventPresentation();
        public List<EventEffect> effects = new List<EventEffect>();
        public List<EventFollowUp> followUps = new List<EventFollowUp>();
    }

    [Serializable]
    public sealed class EventFollowUp
    {
        public EventDefinitionSO definition;
        public EventFollowUpMode startMode;
    }

    [Serializable]
    public sealed class EventEffect
    {
        public EventEffectKind kind;
        public int amount = 1;
        public EventParameter parameter;
        public string key;
        public bool flagValue;
        public AbilityData ability;
        public EquipData equip;
        public HintData hint;
        public EventDefinitionSO eventDefinition;
    }
}
