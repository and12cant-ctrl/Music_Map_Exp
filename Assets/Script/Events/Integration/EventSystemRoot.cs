using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameManager))]
    public sealed class EventSystemRoot : MonoBehaviour
    {
        public EventCatalogSO catalog;
        [Tooltip("Reserve GameManager.BaseCell during initial placement when no base event definition exists.")]
        public bool reserveBaseCell = true;
        public GameObject uiPrefab;
        public ReservationView reservationView;
        public ExecutionView executionView;
        public EventScheduleView scheduleView;
        [Tooltip("Optional component implementing IEventWorldState.")]
        public MonoBehaviour worldState;
        [Tooltip("Components implementing IEventExecutionHandler, one per execution type.")]
        public MonoBehaviour[] executionHandlers = new MonoBehaviour[0];
        public EventManager Manager { get; private set; }
        public EventRunner Runner { get; private set; }
        public MapEventController MapController { get; private set; }
        public ReservationController Reservation { get; private set; }
        public IEventWorldState World { get; private set; }
        public bool BlocksMovement => Reservation?.IsOpen == true || Runner?.Current != null;
        private GameManager game;
        private EventCardRegistry cards;
        private MapDataSO initializedMap;

        public bool Initialize(MapDataSO map)
        {
            if (map == null) return false;
            if (Manager != null) return initializedMap == map;
            if (catalog == null) return false;
            initializedMap = map;
            game = GetComponent<GameManager>();
            if (uiPrefab != null && (reservationView == null || executionView == null))
            {
                var ui = Instantiate(uiPrefab, transform);
                reservationView = ui.GetComponentInChildren<ReservationView>(true);
                executionView = ui.GetComponentInChildren<ExecutionView>(true);
                scheduleView = ui.GetComponentInChildren<EventScheduleView>(true);
            }
            cards = new EventCardRegistry(game.PartyManager);
            Manager = new EventManager(catalog, map, cards, CurrentPhase());
            var world = World = new PartyEventWorldState(game.PartyManager, worldState as IEventWorldState);
            var executor = new EventEffectExecutor(Manager, game.PartyManager, cards, world, FindHandler);
            Runner = new EventRunner(Manager, cards, world, executor, FindHandler) { CanPresentNarrative = executionView != null && executionView.panel != null };
            Reservation = new ReservationController(Manager, reservationView);
            MapController = new MapEventController(Manager, Runner, Reservation);
            Manager.Changed += OnChanged;
            if (scheduleView != null) scheduleView.CancelRequested += CancelReservation;
            if (executionView != null)
            {
                Runner.PresentationRequested += executionView.Show;
                Runner.Failed += executionView.ShowError;
                Runner.Closed += executionView.Hide;
                executionView.ContinueRequested += Runner.Continue;
            }
            if (game.TimeManager != null) game.TimeManager.OnPeriodStarted += OnPeriodStarted;
            game.OnPlayerIconSet += OnPlayerMoved;
            return true;
        }

        private void Start() { if (Manager == null && catalog != null) Initialize(GetComponent<GameManager>().questMap); }
        private void Update()
        {
            if (Runner == null) return;
            if (Runner.HasQueuedEvents && Reservation.IsOpen) Reservation.Close();
            Runner.Tick();
        }
        private int CurrentPhase() => game?.TimeManager?.IsInitialized == true
            ? (game.TimeManager.Runtime.CurrentDate - 1) * 2 + (game.TimeManager.Runtime.Period == TimePeriod.Night ? 1 : 0) : 0;
        private void OnPeriodStarted(TimePeriod period) { Manager.AdvancePhase(CurrentPhase()); RefreshSchedule(); }
        private void OnPlayerMoved(Vector2Int position) => Reservation.Close();
        private IEventExecutionHandler FindHandler(EventExecutionType type) => executionHandlers?.OfType<IEventExecutionHandler>().FirstOrDefault(h => h.ExecutionType == type);

        private void OnChanged(EventInstance instance)
        {
            cards.RefreshLocks(Manager);
            RefreshSchedule();
            var icons = GetComponent<MapIconPresenter>();
            if (icons == null) return;
            if (!instance.occupiesCell || instance.status == EventStatus.Resolved || instance.status == EventStatus.Cancelled) icons.RemoveIcon(instance.instanceId);
            // Initial icons are built by MiniMapBuilder. Later follow-ups are added here.
            else if (mapBuilt && Manager.Catalog.TryGetById(instance.definitionId, out var definition) && definition.appearance.mapIcon != null)
                icons.CreateIcon(instance.instanceId, definition.appearance.mapIcon, instance.cellPosition.x, instance.cellPosition.y);
        }
        private bool mapBuilt;
        private void LateUpdate() { if (Manager != null) mapBuilt = true; }

        public EventSaveData CreateSaveData() => Manager?.CreateSaveData();
        public bool PrepareNewMap(MapDataSO map)
        {
            if (!Initialize(map)) return false;
            if (Runner.Current != null || Runner.IsOperationPending)
            { Debug.LogError("[EventSystemRoot] Finish the active event before generating a new map."); return false; }
            // Restore owns both the registry and cell IDs and notifies runner/reservation.
            Restore(new EventSaveData { currentPhase = CurrentPhase() });
            cards.RefreshLocks(Manager);
            RefreshSchedule();
            map.EnsureGrid();
            foreach (var cell in map.mapGrid)
            {
                cell.EvData = null;
                cell.eventId = null;
            }
            mapBuilt = false;
            GetComponent<MiniMapBuilder>()?.ResetMapView();
            return true;
        }
        private void RefreshSchedule()
        {
            if (scheduleView == null) return;
            scheduleView.Show(Manager.Instances.Where(i => i.reservedPhase >= 0 && (i.status == EventStatus.Reserved || i.status == EventStatus.Ready || i.status == EventStatus.Running))
                .OrderBy(i => i.executePhase).ThenBy(i => i.instanceId)
                .Select(i => (i.instanceId, $"{(Manager.Catalog.TryGetById(i.definitionId, out var d) ? d.displayName : i.definitionId)} / {i.status} / 残り {Mathf.Max(0, i.executePhase - Manager.CurrentPhase)} 時間帯", i.status != EventStatus.Running)));
        }
        private void CancelReservation(string id)
        {
            if (!Manager.CancelReservation(id, out var error)) Debug.LogWarning($"[EventSystemRoot] {error}");
        }
        public void Restore(EventSaveData data)
        {
            if (Manager == null || data == null) { Debug.LogError("[EventSystemRoot] Initialize before restoring events."); return; }
            if (Runner.IsOperationPending) { Debug.LogError("[EventSystemRoot] Close the pending external operation before loading another save."); return; }
            if (data.currentPhase != CurrentPhase()) { Debug.LogError("[EventSystemRoot] Restore the matching TimeRuntimeData before events; phase mismatch."); return; }
            var oldIds = Manager.Instances.Select(i => i.instanceId).ToArray();
            Manager.Restore(data);
            var icons = GetComponent<MapIconPresenter>();
            foreach (var id in oldIds) if (!Manager.TryGet(id, out _) && icons != null) icons.RemoveIcon(id);
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.OnPlayerIconSet -= OnPlayerMoved;
                if (game.TimeManager != null) game.TimeManager.OnPeriodStarted -= OnPeriodStarted;
            }
            if (Manager != null) Manager.Changed -= OnChanged;
            if (scheduleView != null) scheduleView.CancelRequested -= CancelReservation;
            Reservation?.Dispose();
            Runner?.Dispose();
            cards?.RefreshLocks(null);
            if (executionView != null && Runner != null) executionView.ContinueRequested -= Runner.Continue;
        }
    }
}
