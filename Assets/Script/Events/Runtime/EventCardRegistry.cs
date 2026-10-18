using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public sealed class EventCardRegistry : IEventCardSource, IEventParticipantSource, IEventHistoryCardSource
    {
        private readonly PartyManager party;
        public EventCardRegistry(PartyManager party) { this.party = party; }
        private static CardDrag[] Cards() => UnityEngine.Object.FindObjectsByType<CardDrag>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        public bool TryGetCard(string cardId, out CardType type, out int amount)
        {
            var card = Cards().FirstOrDefault(c => c.EventCardId == cardId);
            type = card != null ? card.Type : CardType.Chara;
            // Clones share a source identity. Only one event can reserve that source at a time.
            amount = card == null ? 0 : card.Type == CardType.Chara || card.Type == CardType.Facility ? 1 : Cards().Where(c => c.EventCardId == cardId).Sum(c => c._cardNum);
            return card != null;
        }

        public IEnumerable<CharacterRuntimeData> AssignedCharacters(IReadOnlyList<string> ids)
        {
            if (party?.Runtime == null || ids == null) yield break;
            foreach (var member in party.Runtime.Members)
                if (member != null && ids.Any(id => !string.IsNullOrEmpty(id) && id.StartsWith(member.runtimeId + "/", StringComparison.Ordinal))) yield return member;
        }

        public bool TryGetParameter(IReadOnlyList<string> ids, EventParameter parameter, out int value)
        {
            value = 0;
            var characters = AssignedCharacters(ids).ToArray();
            if (ids != null && ids.Any(id => !string.IsNullOrEmpty(id) && !TryGetCard(id, out _, out _))) return false;
            foreach (var character in characters) value += GetParameter(character, parameter);
            return true;
        }

        public IEnumerable<string> Participants(IReadOnlyList<string> ids) => AssignedCharacters(ids).Select(c => c.runtimeId);
        public IEnumerable<string> Characters(CharaData definition) => party?.Runtime?.Members.Where(c => c != null && c.baseData == definition).Select(c => c.runtimeId) ?? Enumerable.Empty<string>();
        public bool TryCharacter(string id, EventParameter parameter, out int value)
        {
            var member = party?.Runtime?.Members.FirstOrDefault(c => c != null && c.runtimeId == id);
            value = member == null ? 0 : GetParameter(member, parameter); return member != null;
        }
        public string FindCard(UnityEngine.Object target)
        {
            if (target == null) return null;
            if (target is CharaData character)
            {
                var ids = Characters(character).ToArray();
                return Cards().Where(c => !c.EventLocked && ids.Any(id => c.EventCardId.StartsWith(id + "/", StringComparison.Ordinal))).OrderBy(c => c.EventCardId, StringComparer.Ordinal).FirstOrDefault()?.EventCardId;
            }
            return Cards().Where(c => !c.EventLocked && c.GetComponent<EventCardIdentity>()?.definition == target).OrderBy(c => c.EventCardId, StringComparer.Ordinal).FirstOrDefault()?.EventCardId;
        }

        public void RefreshLocks(EventManager manager)
        {
            foreach (var card in Cards()) card.EventLocked = manager?.IsCardAssigned(card.EventCardId) == true;
        }

        public string FindCharacterCard(string characterId) => Cards()
            .Where(c => c.Type == CardType.Chara && !c.EventLocked && !string.IsNullOrEmpty(characterId) && c.EventCardId.StartsWith(characterId + "/", StringComparison.Ordinal))
            .OrderBy(c => c.EventCardId, StringComparer.Ordinal).FirstOrDefault()?.EventCardId;

        public static int GetParameter(CharacterRuntimeData character, EventParameter parameter)
        {
            switch (parameter)
            {
                case EventParameter.Strength: return character.curStr;
                case EventParameter.Intelligence: return character.curInt;
                case EventParameter.Appeal: return character.curApi;
                case EventParameter.Social: return character.curSoc;
                case EventParameter.Survival: return character.curSur;
                case EventParameter.Mystery: return character.curMys;
                case EventParameter.HP: return character.currentHP;
                case EventParameter.MP: return character.currentMP;
                case EventParameter.SP: return character.currentSP;
                default: return 0;
            }
        }

        public static void ChangeParameter(CharacterRuntimeData character, EventParameter parameter, int amount)
        {
            switch (parameter)
            {
                case EventParameter.Strength: character.curStr += amount; break;
                case EventParameter.Intelligence: character.curInt += amount; break;
                case EventParameter.Appeal: character.curApi += amount; break;
                case EventParameter.Social: character.curSoc += amount; break;
                case EventParameter.Survival: character.curSur += amount; break;
                case EventParameter.Mystery: character.curMys += amount; break;
                case EventParameter.HP: character.currentHP = Mathf.Clamp(character.currentHP + amount, 0, character.currentMaxHP); break;
                case EventParameter.MP: character.currentMP = Mathf.Clamp(character.currentMP + amount, 0, character.currentMaxMP); break;
                case EventParameter.SP: character.currentSP = Mathf.Clamp(character.currentSP + amount, 0, character.currentMaxSP); break;
            }
        }
    }
}
