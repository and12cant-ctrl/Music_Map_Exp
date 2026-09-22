using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicMap.Events
{
    public sealed class ReservationView : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text requiredTime;
        public TMP_Text requirements;
        public TMP_Text message;
        public Image background;
        public Transform slotContainer;
        public RectTransform dragLayer;
        public CardSlot slotPrefab;
        public Button confirmButton;
        public Button cancelButton;
        private readonly List<CardSlot> slots = new List<CardSlot>();
        public IReadOnlyList<CardSlot> Slots => slots;
        public event Action ConfirmRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            Hide();
        }
        private void Confirm() => ConfirmRequested?.Invoke();
        private void Close() => CloseRequested?.Invoke();
        private void OnDisable() { if (slots.Count > 0) CloseRequested?.Invoke(); }

        public bool Show(EventDefinitionSO definition, out string error)
        {
            error = null;
            if (definition?.reservation?.cardSlots == null || definition.reservation.requirements == null || definition.reservation.reservationPresentation == null
                || definition.reservation.cardSlots.Any(s => s == null) || definition.reservation.requirements.Any(r => r == null))
                return EventManager.Fail("Reservation definition has missing sections.", out error);
            if (panel == null || (definition.reservation.cardSlots.Count > 0 && (slotPrefab == null || slotContainer == null)))
                return EventManager.Fail("ReservationView panel, slot container, or CardSlot prefab is missing.", out error);
            ClearSlots();
            if (title != null) title.text = definition.displayName;
            if (description != null) description.text = definition.reservation.reservationPresentation.text;
            if (background != null) { background.sprite = definition.reservation.reservationPresentation.image; background.enabled = background.sprite != null; }
            if (requiredTime != null) requiredTime.text = $"実行まで {definition.reservation.requiredPhaseCount} 時間帯";
            if (requirements != null) requirements.text = string.Join("\n", definition.reservation.requirements.Select(r => $"{r.parameter}: {r.minimum} 以上"));
            if (message != null) message.text = "";
            foreach (var slotDefinition in definition.reservation.cardSlots)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.gameObject.SetActive(true);
                slot.name = string.IsNullOrEmpty(slotDefinition.label) ? slotDefinition.acceptedType.ToString() : slotDefinition.label;
                slot.ConfigureForEvent(slotDefinition.acceptedType, slotDefinition.amount);
                var label = new GameObject("EventSlotLabel", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(slot.transform, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = new Vector2(1, .32f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.fontSize = 12;
                label.alignment = TextAlignmentOptions.Center;
                if (title != null) label.font = title.font;
                label.raycastTarget = false;
                label.text = $"{slot.name}\n×{slotDefinition.amount} {(slotDefinition.required ? "必須" : "任意")}";
                slots.Add(slot);
            }
            panel.SetActive(true);
            return true;
        }

        public void ShowMessage(string text) { if (message != null) message.text = text; }
        public void Hide() { if (panel != null) panel.SetActive(false); }
        public void ClearSlots()
        {
            foreach (var slot in slots) if (slot != null) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
            slots.Clear();
        }
    }
}
