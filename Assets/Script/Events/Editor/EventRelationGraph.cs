using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicMap.Events.Editor
{
    internal sealed class EventRelationGraph : VisualElement
    {
        private sealed class Edge { public EventDefinitionSO from, to; public string label; public bool history; }
        public EventRelationGraph(EventDefinitionSO[] definitions, Action<EventDefinitionSO> select)
        {
            // One row per relationship keeps all outcome and query labels readable, including cycles.
            var edges = new List<Edge>();
            foreach (var d in definitions)
            {
                foreach (var outcome in d.EffectiveOutcomes ?? Enumerable.Empty<EventOutcomeDefinition>())
                    foreach (var follow in outcome?.followUps ?? new List<EventFollowUp>())
                        if (follow?.definition != null) edges.Add(new Edge { from = d, to = follow.definition, label = outcome.outcomeCode + " / FollowUp " + follow.delayPhaseCount + "回後" });
                if (!d.useExtendedLifecycle || d.extension == null) continue;
                var queries = (d.extension.conditions ?? new List<OccurrenceCondition>()).Select(c => c?.query)
                    .Concat((d.extension.pointConditions ?? new List<PointCondition>()).Select(c => c?.query));
                foreach (var q in queries)
                    if (q?.eventDefinition != null && (q.source == ConditionSource.EventHistory || q.source == ConditionSource.EventParticipant))
                        edges.Add(new Edge { from = d, to = q.eventDefinition, history = true, label = "履歴参照 / " + q.historyMode + (q.source == ConditionSource.EventHistory ? " / " + q.outcome : " / 参加者能力") });
                foreach (var slot in d.reservation?.cardSlots ?? new List<CardSlotDefinition>())
                    if (slot?.historyParticipant?.enabled == true && slot.historyParticipant.definition != null)
                        edges.Add(new Edge { from = d, to = slot.historyParticipant.definition, history = true, label = "参加者参照 / Slot " + slot.historyParticipant.slotNumber + " / " + slot.historyParticipant.source });
            }
            foreach (var edge in edges) AddEdge(edge, select);
            var unused = definitions.Where(d => !edges.Any(e => e.from == d || e.to == d));
            foreach (var d in unused) Add(new Button(() => select(d)) { text = d.displayName + " / " + d.eventCode + "（関連なし）" });
            if (definitions.Length == 0) Add(new Label("イベントがありません。"));
        }
        private void AddEdge(Edge edge, Action<EventDefinitionSO> select)
        {
            var row = new VisualElement(); row.AddToClassList("event-graph-row");
            var source = new Button(() => select(edge.from)) { text = edge.from.displayName + "\n" + edge.from.eventCode };
            source.AddToClassList("event-graph-node"); row.Add(source);
            var connector = new VisualElement(); connector.AddToClassList("event-graph-connector");
            row.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool narrow = evt.newRect.width < 650;
                if (row.ClassListContains("event-graph-row--narrow") != narrow) row.EnableInClassList("event-graph-row--narrow", narrow);
                connector.MarkDirtyRepaint();
            });
            var label = new Label(edge.label); label.style.whiteSpace = WhiteSpace.Normal; label.style.unityTextAlign = TextAnchor.UpperCenter; connector.Add(label);
            connector.generateVisualContent += context =>
            {
                var painter = context.painter2D; painter.strokeColor = edge.history ? new Color(1, .65f, .25f) : new Color(.3f, .7f, 1); painter.lineWidth = 2;
                bool narrow = row.ClassListContains("event-graph-row--narrow");
                Vector2 start = narrow ? new Vector2(12, 8) : new Vector2(8, connector.contentRect.height - 16);
                Vector2 end = narrow ? new Vector2(12, connector.contentRect.height - 8) : new Vector2(connector.contentRect.width - 8, start.y);
                painter.BeginPath();
                var direction = (end - start).normalized; float length = Vector2.Distance(start, end);
                if (edge.history) for (float distance = 0; distance < length; distance += 16) { painter.MoveTo(start + direction * distance); painter.LineTo(start + direction * Mathf.Min(distance + 9, length)); }
                else { painter.MoveTo(start); painter.LineTo(end); }
                var side = new Vector2(-direction.y, direction.x) * 6;
                painter.MoveTo(end - direction * 8 + side); painter.LineTo(end); painter.LineTo(end - direction * 8 - side); painter.Stroke();
            };
            row.Add(connector);
            var target = new Button(() => select(edge.to)) { text = edge.to.displayName + "\n" + edge.to.eventCode }; target.AddToClassList("event-graph-node"); row.Add(target); Add(row);
        }
    }
}
