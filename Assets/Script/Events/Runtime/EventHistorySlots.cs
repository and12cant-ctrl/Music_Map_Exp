using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicMap.Events
{
    public enum ParticipantHistorySource { FollowUpParent, LatestMatching }
    [Serializable] public sealed class ParticipantSlotReference
    {
        public bool enabled;
        public EventDefinitionSO definition;
        public int slotNumber = 1;
        public ParticipantHistorySource source;
        public bool filterOutcome;
        public OutcomeType outcome;
    }
    [Serializable] public sealed class EventSlotAssignment
    {
        public int slotNumber;
        public string characterId;
        public EventSlotAssignment Copy() => (EventSlotAssignment)MemberwiseClone();
        public static List<EventSlotAssignment> CopyList(List<EventSlotAssignment> source)
            => source?.Where(s => s != null).Select(s => s.Copy()).ToList() ?? new List<EventSlotAssignment>();
        public static bool IsValid(List<EventSlotAssignment> source) => source == null ||
            source.All(s => s != null && s.slotNumber > 0 && !string.IsNullOrEmpty(s.characterId)) &&
            source.Select(s => s.slotNumber).Distinct().Count() == source.Count;
    }
    // Optional capability: other participant providers can still accept a manually supplied matching card.
    public interface IEventHistoryCardSource { string FindCharacterCard(string characterId); }

    public sealed partial class EventManager
    {
        private List<EventSlotAssignment> CaptureSlots(EventDefinitionSO definition, IReadOnlyList<string> ids)
        {
            var result = new List<EventSlotAssignment>();
            var actors = cards as IEventParticipantSource;
            if (actors == null) return result;
            for (int i = 0; i < ids.Count; i++)
            {
                if (definition.reservation.cardSlots[i].acceptedType != CardType.Chara || string.IsNullOrEmpty(ids[i])) continue;
                var participants = actors.Participants(new[] { ids[i] }).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
                if (participants.Length == 1) result.Add(new EventSlotAssignment { slotNumber = i + 1, characterId = participants[0] });
            }
            return result;
        }

        public bool TryResolveHistoryParticipant(EventInstance instance, ParticipantSlotReference reference, out string characterId, out string error)
        {
            characterId = null;
            if (reference == null || !reference.enabled || reference.definition == null || reference.slotNumber < 1)
                return Fail("過去参加者: 参照イベントとSlot番号を設定してください。", out error);
            var matches = Progress.history.Where(h => h != null && h.resolved && h.definitionId == reference.definition.PersistentId);
            if (reference.filterOutcome) matches = matches.Where(h => h.outcome == reference.outcome);
            EventHistoryRecord record;
            if (reference.source == ParticipantHistorySource.FollowUpParent)
            {
                if (string.IsNullOrEmpty(instance?.sourceEventInstanceId)) return Fail("過去参加者: FollowUpの発生元が記録されていません。", out error);
                record = matches.LastOrDefault(h => h.instanceId == instance.sourceEventInstanceId);
            }
            else if (reference.source == ParticipantHistorySource.LatestMatching) record = matches.LastOrDefault();
            else return Fail("過去参加者: 履歴の選択方式が不正です。", out error);
            if (record == null) return Fail("過去参加者: 条件に一致する解決済み履歴がありません。", out error);
            characterId = record.slotAssignments?.FirstOrDefault(s => s != null && s.slotNumber == reference.slotNumber)?.characterId;
            if (string.IsNullOrEmpty(characterId)) return Fail("過去参加者: 参照Slotの参加者が未記録です（旧履歴・空Slot・EXPIRED等）。", out error);
            error = null; return true;
        }

        private bool AssignHistorySlot(EventInstance instance, CardSlotDefinition slot, List<string> result, int index, out string error)
        {
            if (slot.acceptedType != CardType.Chara || slot.amount != 1 || slot.slotType == SlotType.Involved)
                return Fail("過去参加者: 人物1人の枠を指定し、固定関係者との併用を解除してください。", out error);
            if (!TryResolveHistoryParticipant(instance, slot.historyParticipant, out var character, out error)) return false;
            var actors = cards as IEventParticipantSource;
            if (actors == null || !actors.TryCharacter(character, EventParameter.Appeal, out _))
                return Fail("過去参加者: 同じ人物が現在利用できません。代替はできません。", out error);
            var id = result[index];
            if (string.IsNullOrEmpty(id)) id = (cards as IEventHistoryCardSource)?.FindCharacterCard(character);
            var participants = string.IsNullOrEmpty(id) ? Array.Empty<string>() : actors.Participants(new[] { id }).Distinct().ToArray();
            if (participants.Length != 1 || participants[0] != character || !cards.TryGetCard(id, out var type, out _) || type != CardType.Chara || IsCardAssigned(id))
                return Fail("過去参加者: 同じ人物の使用可能なカードが必要です。別の人物では開始できません。", out error);
            result[index] = id; error = null; return true;
        }
    }
}
