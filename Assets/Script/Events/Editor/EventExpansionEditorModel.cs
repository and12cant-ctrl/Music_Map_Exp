using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    // A view onto existing serialized fields, not a second copy of the event data.
    internal sealed class EventExpansionEditorModel
    {
        private readonly EventDefinitionSO definition;
        private readonly EventCatalogSO catalog;
        private readonly OutcomeEditor success, failure, expired;
        private EventExpansion Config => definition.extension;
        public EventExpansionEditorModel(EventDefinitionSO definition, EventCatalogSO catalog)
        { this.definition = definition; this.catalog = catalog; success = new OutcomeEditor(Config.success); failure = new OutcomeEditor(Config.failure); expired = new OutcomeEditor(Config.expired); }
        [ShowInInspector, FoldoutGroup("基本情報"), GUIColor(.85f,.85f,.85f)] public string DisplayName { get => definition.displayName; set => definition.displayName = value; }
        [ShowInInspector, FoldoutGroup("基本情報")] public string EventCode { get => definition.eventCode; set => definition.eventCode = value; }
        [ShowInInspector, FoldoutGroup("基本情報"), ReadOnly] public string DefinitionId => definition.PersistentId;
        [ShowInInspector, FoldoutGroup("基本情報")] public bool Enabled { get => definition.enabled; set => definition.enabled = value; }
        [ShowInInspector, FoldoutGroup("基本情報")] public EventAppearanceDefinition MapPlacement => definition.appearance;
        [ShowInInspector, FoldoutGroup("基本情報")] public EventPresentation StartPresentation => definition.reservation.reservationPresentation;
        [ShowInInspector, FoldoutGroup("基本情報")] public EventExecutionDefinition Execution => definition.execution;
        [ShowInInspector, FoldoutGroup("基本情報")] public int Priority { get => definition.priority; set => definition.priority = value; }
        [ShowInInspector, FoldoutGroup("発生条件"), GUIColor(.65f,.8f,1)] public List<OccurrenceCondition> Conditions { get => Config.conditions; set => Config.conditions = value; }
        [ShowInInspector, FoldoutGroup("Slot設定"), GUIColor(.7f,1,.7f)] public List<CardSlotDefinition> Slots { get => definition.reservation.cardSlots; set => definition.reservation.cardSlots = value; }
        [ShowInInspector, FoldoutGroup("Slot設定")] public List<EventRequirement> Requirements => definition.reservation.requirements;
        [ShowInInspector, FoldoutGroup("時間設定"), GUIColor(1,.95f,.6f), MinValue(0)] public int DeadlinePhaseCount { get => Config.deadlinePhaseCount; set => Config.deadlinePhaseCount = value; }
        [ShowInInspector, FoldoutGroup("時間設定"), MinValue(0)] public int DurationPhaseCount { get => Config.durationPhaseCount; set => Config.durationPhaseCount = value; }
        [ShowInInspector, FoldoutGroup("HintLevel設定"), GUIColor(.85f,.65f,1)] public List<PointCondition> PointConditions { get => Config.pointConditions; set => Config.pointConditions = value; }
        [ShowInInspector, FoldoutGroup("HintLevel設定")] public int Level2Points { get => Config.level2Points; set => Config.level2Points = value; }
        [ShowInInspector, FoldoutGroup("HintLevel設定")] public int Level3Points { get => Config.level3Points; set => Config.level3Points = value; }
        [ShowInInspector, FoldoutGroup("HintLevel設定")] public HintPoolSO CaseHintPool { get => Config.randomHintPool; set => Config.randomHintPool = value; }
        [ShowInInspector, FoldoutGroup("Outcome設定"), TabGroup("Outcome設定/Tabs", "SUCCESS"), HideLabel, GUIColor(1,.75f,.75f)] public OutcomeEditor Success => success;
        [ShowInInspector, TabGroup("Outcome設定/Tabs", "FAILURE"), HideLabel] public OutcomeEditor Failure => failure;
        [ShowInInspector, TabGroup("Outcome設定/Tabs", "EXPIRED"), HideLabel] public OutcomeEditor Expired => expired;
        [OnInspectorGUI, FoldoutGroup("Validator"), GUIColor(1,.8f,.5f)] private void Validation()
        {
            var selectedCatalog = catalog != null ? catalog : AssetDatabase.FindAssets("t:EventCatalogSO").Select(g => AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c.definitions != null && c.definitions.Contains(definition));
            foreach (var issue in EventExpansionValidator.Validate(definition, selectedCatalog)) EditorGUILayout.HelpBox(issue.message, issue.severity == EventIssueSeverity.Error ? MessageType.Error : issue.severity == EventIssueSeverity.Warning ? MessageType.Warning : MessageType.Info);
        }
        [Serializable] internal sealed class OutcomeEditor
        {
            private readonly EventOutcomeDefinition outcome;
            public OutcomeEditor(EventOutcomeDefinition outcome) { this.outcome = outcome; }
            [ShowInInspector] public EventPresentation Presentation => outcome?.presentation;
            [ShowInInspector, ListDrawerSettings(ShowFoldout = true), LabelText("Effects (+ Effect追加)")] public List<OutcomeEffect> Effects { get => outcome?.typedEffects; set { if (outcome != null) outcome.typedEffects = value; } }
            [ShowInInspector] public List<EventFollowUp> FollowUps { get => outcome?.followUps; set { if (outcome != null) outcome.followUps = value; } }
        }
    }
}
