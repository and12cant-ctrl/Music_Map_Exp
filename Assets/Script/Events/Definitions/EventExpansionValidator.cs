using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicMap.Events
{
    public static class EventExpansionValidator
    {
        public static IEnumerable<EventValidationIssue> Validate(EventDefinitionSO definition, EventCatalogSO catalog)
        {
            var issues = new List<EventValidationIssue>();
            void Add(string message, EventIssueSeverity severity = EventIssueSeverity.Error) => issues.Add(new EventValidationIssue { asset = definition, message = message, severity = severity });
            var config = definition.extension;
            if (config == null) { Add("Extension is missing."); return issues; }
            if (config.deadlinePhaseCount < 0 || config.durationPhaseCount < 0) Add("Phase counts must be nonnegative.");
            if (config.deadlinePhaseCount == 0) Add("Deadline 0 resolves EXPIRED without Start or participants.", EventIssueSeverity.Info);
            if (config.level3Points < config.level2Points) Add("Level 3 threshold must not be below Level 2.");
            void Query(EventQuery q, string label)
            {
                if (q == null || !q.HasTarget) { Add(label + ": required Target/Value is missing."); return; }
                if (!Enum.IsDefined(typeof(ConditionSource), q.source) || !Enum.IsDefined(typeof(Comparison), q.comparison) || !Enum.IsDefined(typeof(HistoryMode), q.historyMode)) Add(label + ": invalid selector.");
                if (q.source == ConditionSource.World && !Enum.IsDefined(typeof(WorldPhase), q.phase) || q.source == ConditionSource.EventHistory && !Enum.IsDefined(typeof(OutcomeType), q.outcome)) Add(label + ": 比較値の種類が不正です。");
                if ((q.source == ConditionSource.Character || q.source == ConditionSource.EventParticipant) && !Enum.IsDefined(typeof(EventParameter), q.parameter)) Add(label + ": 能力値の種類が不正です。");
                if ((q.source == ConditionSource.Flag || q.source == ConditionSource.Hint || q.source == ConditionSource.World || q.source == ConditionSource.EventHistory) && q.comparison != Comparison.Equal && q.comparison != Comparison.NotEqual) Add(label + ": only Equal/NotEqual are valid for this property.");
                if (q.eventDefinition != null && (catalog?.definitions == null || !catalog.definitions.Contains(q.eventDefinition))) Add(label + ": target event is not registered.");
            }
            if (config.conditions == null) Add("Conditions list is missing."); else foreach (var condition in config.conditions) Query(condition?.query, "Condition");
            if (config.pointConditions == null) Add("PointConditions list is missing."); else foreach (var condition in config.pointConditions) Query(condition?.query, "PointCondition");
            var slots = definition.reservation?.cardSlots;
            if (slots == null || slots.Count > 32) Add("Slots must exist and contain at most 32 entries.");
            else foreach (var slot in slots)
            {
                if (slot == null || slot.amount < 1 || !Enum.IsDefined(typeof(SlotType), slot.slotType) || !Enum.IsDefined(typeof(CardType), slot.acceptedType)) { Add("Invalid slot."); continue; }
                if ((slot.acceptedType == CardType.Chara || slot.acceptedType == CardType.Facility) && slot.amount != 1) Add("Character/Facility slots require amount 1.");
                var reference = slot.historyParticipant;
                if (reference?.enabled == true)
                {
                    if (slot.acceptedType != CardType.Chara || slot.amount != 1 || slot.slotType == SlotType.Involved) Add("Slot: 過去参加者は人物1人の枠のみ。固定関係者との併用はできません。");
                    if (reference.definition == null || catalog?.definitions == null || !catalog.definitions.Contains(reference.definition)) Add("Slot: 過去参加者の参照イベントが未設定／未登録です。");
                    else if (reference.slotNumber < 1 || reference.slotNumber > (reference.definition.reservation?.cardSlots?.Count ?? 0)) Add("Slot: 参照先のSlot番号が存在しません。");
                    else if (reference.definition.reservation.cardSlots[reference.slotNumber - 1]?.acceptedType != CardType.Chara) Add("Slot: 参照先が人物Slotではありません。");
                    if (!Enum.IsDefined(typeof(ParticipantHistorySource), reference.source) || !Enum.IsDefined(typeof(OutcomeType), reference.outcome)) Add("Slot: 過去参加者の参照方式／結果が不正です。");
                    if (reference.filterOutcome && reference.outcome == OutcomeType.EXPIRED) Add("Slot: EXPIREDの参加者は空のため参照を満たせません。", EventIssueSeverity.Warning);
                    Add("Slot: 履歴・同一人物の利用可否は開始時に検査します。旧履歴のParticipants順からSlotは推測しません。", EventIssueSeverity.Info);
                }
                if (slot.slotType == SlotType.Involved && (slot.involvedTarget == null || !(slot.involvedTarget is CharaData || slot.involvedTarget is FacilityDefinitionSO || slot.involvedTarget is EquipData))) Add("Involved requires a Character, Facility or Equipment target.");
                if (slot.slotType == SlotType.Involved && slot.involvedTarget != null && (slot.involvedTarget is CharaData && slot.acceptedType != CardType.Chara || slot.involvedTarget is FacilityDefinitionSO && slot.acceptedType != CardType.Facility || slot.involvedTarget is EquipData && slot.acceptedType != CardType.Equip)) Add("Involved target and card type differ.");
            }
            if (definition.execution?.executionType != EventExecutionType.Narrative) Add("Extended lifecycle currently requires Narrative execution.");
            var judge = definition.execution?.judge;
            if (judge == null || judge.diceCount < 1 || judge.diceCount > 100 || judge.diceSides < 2 || judge.diceSides > 10000) Add("Invalid judgement settings.");
            var outcomes = config.Outcomes.ToArray();
            for (int i = 0; i < outcomes.Length; i++)
            {
                var outcome = outcomes[i];
                if (outcome == null || outcome.outcomeCode != ((OutcomeType)i).ToString()) { Add("SUCCESS/FAILURE/EXPIRED outcome is missing or renamed."); continue; }
                if (outcome.presentation == null) Add("Outcome presentation is missing.");
                else if (string.IsNullOrWhiteSpace(outcome.presentation.text)) Add(outcome.outcomeCode + ": presentation text is empty.", EventIssueSeverity.Warning);
                if (outcome.effects == null || outcome.typedEffects == null || outcome.followUps == null) { Add("Outcome lists must not be null."); continue; }
                if (outcome.effects.Count > 0) Add("Legacy effects remain and execute before typed Effects.", EventIssueSeverity.Info);
                foreach (var effect in outcome.typedEffects)
                {
                    if (effect == null) Add("Missing Effect type.");
                    if (effect is FlagEffect flag && flag.flag == null) Add("FlagEffect requires a Flag.");
                    if (effect is RelationEffect relation && relation.facility == null) Add("RelationEffect requires a Facility.");
                    if (effect is ItemEffect item && (item.item == null || item.amount < 1 || item.amount > 10000)) Add("ItemEffect requires Equipment and amount 1..10000.");
                    if (effect is HintEffect hint)
                    {
                        if (hint.isFixedHint && hint.fixedHint == null) Add("Fixed Hint is missing.");
                        if (!hint.isFixedHint && (config.randomHintPool?.hints == null || !config.randomHintPool.hints.Any(h => h != null))) Add("Random Hint Pool is missing/empty.");
                    }
                }
                foreach (var followUp in outcome.followUps)
                {
                    if (followUp?.definition == null || catalog?.definitions == null || !catalog.definitions.Contains(followUp.definition)) Add("FollowUp EventDefinition is missing/unregistered.");
                    if (followUp != null && followUp.delayPhaseCount < 0) Add("FollowUp delay must be nonnegative.");
                    if (followUp?.definition == definition && followUp.delayPhaseCount == 0 && config.deadlinePhaseCount == 0) Add("Immediate self-expiration FollowUp would repeat indefinitely.");
                }
            }
            if (definition.appearance?.mapIcon == null) Add("Map icon is missing.", EventIssueSeverity.Warning);
            return issues;
        }
    }
}
