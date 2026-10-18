using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicMap.Events
{
    [Serializable]
    public sealed class EventInstance
    {
        public string instanceId;
        public string definitionId;
        public string sourceEventInstanceId;
        public List<EventSlotAssignment> slotAssignments = new List<EventSlotAssignment>();
        public EventStatus status;
        public Vector2Int cellPosition;
        public bool occupiesCell = true;
        public int reservedPhase = -1;
        public int executePhase = -1;
        public List<string> assignedCardIds = new List<string>();
        public int appearedPhase;
        public int deadlinePhase = -1;
        public List<string> participantIds = new List<string>();
        public int hintLevel = 1;
        public int hintPoints;
        public bool hintLevelCalculated;
        public int randomSeed;
        public string resolvedOutcomeCode;
        public bool hasJudgement;
        public List<int> diceResults = new List<int>();
        public int judgeTotal;
        public bool judgeSucceeded;
        public int appliedEffectCount;
        public int appliedFollowUpCount;
        public List<string> immediateAncestors = new List<string>();
        public string lastError;

        public EventInstance Copy()
        {
            var copy = (EventInstance)MemberwiseClone();
            copy.slotAssignments = EventSlotAssignment.CopyList(slotAssignments);
            copy.participantIds = new List<string>(participantIds ?? new List<string>());
            copy.assignedCardIds = new List<string>(assignedCardIds ?? new List<string>());
            copy.diceResults = new List<int>(diceResults ?? new List<int>());
            copy.immediateAncestors = new List<string>(immediateAncestors ?? new List<string>());
            return copy;
        }
    }

    [Serializable]
    public sealed class EventSaveData
    {
        public int version = 1;
        public EventProgress progress = new EventProgress();
        public int currentPhase;
        public List<EventInstance> instances = new List<EventInstance>();
        public List<string> unlockedDefinitionIds = new List<string>();
    }
}
