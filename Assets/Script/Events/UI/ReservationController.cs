using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public sealed class ReservationController : IDisposable
    {
        private readonly EventManager manager;
        private readonly ReservationView view;
        private EventInstance current;
        private readonly Dictionary<string, RectTransform> dragOrigins = new Dictionary<string, RectTransform>();
        public bool IsOpen => current != null;
        public ReservationController(EventManager manager, ReservationView view)
        {
            this.manager = manager; this.view = view;
            if (view != null) { view.ConfirmRequested += Confirm; view.CloseRequested += Close; }
            manager.Restored += Close;
        }

        public bool Open(EventInstance instance, EventDefinitionSO definition)
        {
            Close();
            if (instance == null || definition == null || instance.status != EventStatus.Available || instance.definitionId != definition.PersistentId) return false;
            if (view == null) { Debug.LogError("[ReservationController] ReservationView is not connected."); return false; }
            if (!view.Show(definition, out var error)) { Debug.LogError($"[ReservationController] {error}"); return false; }
            if (view.dragLayer != null)
                foreach (var card in UnityEngine.Object.FindObjectsByType<CardDrag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!dragOrigins.ContainsKey(card.EventCardId)) dragOrigins.Add(card.EventCardId, card.DragLayer);
                    card.SetDragArea(view.dragLayer);
                }
            current = instance;
            return true;
        }

        public void Confirm()
        {
            if (current == null || view == null) return;
            foreach (var slot in view.Slots)
                if (slot.SlotInCard != null && !slot.CanAccept(slot.SlotInCard))
                { ReturnCards(); view.ShowMessage("カードの条件を満たしていません。"); return; }
            var ids = view.Slots.Select(slot => slot.SlotInCard != null ? slot.SlotInCard.EventCardId : "").ToArray();
            if (!manager.Reserve(current.instanceId, ids, manager.CurrentPhase, out var error))
            { ReturnCards(); view.ShowMessage(error); return; }
            // The manager owns the committed IDs. Cards return to their source and remain locked until completion/cancel.
            Close();
        }

        private void ReturnCards()
        {
            if (view == null) return;
            foreach (var slot in view.Slots) if (slot != null && slot.SlotInCard != null) slot.ReturnCard(slot.SlotInCard);
        }

        public void Close()
        {
            current = null;
            if (view == null) return;
            ReturnCards();
            foreach (var card in UnityEngine.Object.FindObjectsByType<CardDrag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (dragOrigins.TryGetValue(card.EventCardId, out var origin)) card.SetDragArea(origin);
            dragOrigins.Clear();
            view.ClearSlots();
            view.Hide();
        }

        public void Dispose()
        {
            Close();
            if (view != null) { view.ConfirmRequested -= Confirm; view.CloseRequested -= Close; }
            manager.Restored -= Close;
        }
    }
}
