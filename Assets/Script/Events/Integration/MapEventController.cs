using UnityEngine;

namespace MusicMap.Events
{
    public sealed class MapEventController
    {
        private readonly EventManager manager;
        private readonly EventRunner runner;
        private readonly ReservationController reservation;
        public MapEventController(EventManager manager, EventRunner runner, ReservationController reservation)
        { this.manager = manager; this.runner = runner; this.reservation = reservation; }

        public bool Select(MapCell cell)
        {
            if (cell == null || string.IsNullOrEmpty(cell.eventInstanceId)) return Error("Selected cell has no eventInstanceId.");
            if (!manager.TryGetDefinition(cell.eventInstanceId, out var instance, out var definition, out var error)) return Error(error);
            if (definition.reservation == null) return Error("Reservation definition is missing.");
            if (runner.Current != null) return Error("Another event is currently running.");
            if (instance.status == EventStatus.Ready) { reservation.Close(); runner.EnqueueReady(instance.instanceId); return true; }
            if (instance.status != EventStatus.Available) return Error($"Event '{instance.instanceId}' is {instance.status}.");
            if (definition.useExtendedLifecycle || definition.reservation.triggerMode == EventTriggerMode.Scheduled) return reservation.Open(instance, definition);
            reservation.Close();
            return manager.RequestImmediate(instance.instanceId, out error) || Error(error);
        }
        private static bool Error(string message) { Debug.LogWarning($"[MapEventController] {message}"); return false; }
    }
}
