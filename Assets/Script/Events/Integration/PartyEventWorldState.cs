namespace MusicMap.Events
{
    public sealed class PartyEventWorldState : IEventWorldState
    {
        private readonly PartyManager party;
        private readonly IEventWorldState external;
        public PartyEventWorldState(PartyManager party, IEventWorldState external) { this.party = party; this.external = external; }
        public bool TryGetResource(string key, out int value)
        {
            if (string.Equals(key, "Gold", System.StringComparison.OrdinalIgnoreCase) && party?.Runtime != null)
            { value = party.Runtime.Gold; return true; }
            value = 0;
            return external != null && external.TryGetResource(key, out value);
        }
        public bool TryGetFlag(string key, out bool value)
        { value = false; return external != null && external.TryGetFlag(key, out value); }
        public bool TrySetFlag(string key, bool value, string operationId, out string error)
        {
            if (external == null) return EventManager.Fail("Flag system is not connected.", out error);
            return external.TrySetFlag(key, value, operationId, out error);
        }
    }
}
