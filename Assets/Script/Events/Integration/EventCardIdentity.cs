using UnityEngine;

namespace MusicMap.Events
{
    // Facilities and equipment stay independent of CharacterRuntimeData.
    [RequireComponent(typeof(CardDrag))]
    public sealed class EventCardIdentity : MonoBehaviour
    {
        public Object definition;
        [SerializeField] private string instanceId;
        public string InstanceId => instanceId;
        public void RestoreIdentity(string savedId)
        {
            if (string.IsNullOrEmpty(savedId)) throw new System.ArgumentException("Card instance ID is required.", nameof(savedId));
            instanceId = savedId; GetComponent<CardDrag>().BindEventCard(instanceId);
        }
        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = System.Guid.NewGuid().ToString("N");
            var card = GetComponent<CardDrag>();
            card.BindEventCard(instanceId);
            if (definition is FacilityDefinitionSO) card.Type = card.TrueType = CardType.Facility;
            else if (definition is EquipData) card.Type = card.TrueType = CardType.Equip;
        }
    }
}
