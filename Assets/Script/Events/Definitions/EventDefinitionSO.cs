using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicMap.Events
{
    [CreateAssetMenu(menuName = "Events/Event Definition")]
    public sealed class EventDefinitionSO : ScriptableObject
    {
        [SerializeField, ReadOnly, TabGroup("基本")] private string persistentId;
        public string PersistentId => persistentId;
        [TabGroup("基本")] public string eventCode;
        [TabGroup("基本")] public string displayName;
        [TabGroup("基本")] public bool enabled = true;
        // Kept during migration. Never use this value as the new category identity.
        [HideInInspector] public string category;
        [SerializeField, HideInInspector] private string categoryId;
        [SerializeField, HideInInspector] private EventCategoryMigrationState categoryMigrationState;
        [HideInInspector] public string CategoryId => categoryId;
        [HideInInspector] public string LegacyCategory => category;
        [HideInInspector] public EventCategoryMigrationState CategoryMigrationState => categoryMigrationState;
        [TabGroup("基本")] public List<string> tags = new List<string>();
        [TabGroup("基本"), TextArea] public string developerMemo;
        [TabGroup("基本")] public int sortOrder;
        [TabGroup("基本")] public int priority;
        [TabGroup("出現")] public EventAppearanceDefinition appearance = new EventAppearanceDefinition();
        [TabGroup("予約")] public EventReservationDefinition reservation = new EventReservationDefinition();
        [TabGroup("実行")] public EventExecutionDefinition execution = new EventExecutionDefinition();
        [TabGroup("結果")] public List<EventOutcomeDefinition> outcomes = new List<EventOutcomeDefinition>
        {
            new EventOutcomeDefinition { outcomeCode = "SUCCESS", conditions = new List<EventCondition>
                { new EventCondition { kind = EventConditionKind.JudgeSucceeded } } },
            new EventOutcomeDefinition { outcomeCode = "FAILURE", isDefault = true }
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Never repair duplicates here: saved games may already use this identity.
            if (string.IsNullOrEmpty(persistentId)) persistentId = Guid.NewGuid().ToString("N");
        }
#endif
    }
}
