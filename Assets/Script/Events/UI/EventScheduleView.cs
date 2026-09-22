using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicMap.Events
{
    public sealed class EventScheduleView : MonoBehaviour
    {
        public Transform container;
        public Button rowPrefab;
        private readonly List<Button> rows = new List<Button>();
        public event Action<string> CancelRequested;

        public void Show(IEnumerable<(string id, string label, bool canCancel)> items)
        {
            if (container == null || rowPrefab == null) return;
            foreach (var row in rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            rows.Clear();
            foreach (var item in items)
            {
                var row = Instantiate(rowPrefab, container);
                row.gameObject.SetActive(true);
                var text = row.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = item.label;
                row.interactable = item.canCancel;
                string id = item.id;
                row.onClick.AddListener(() => CancelRequested?.Invoke(id));
                rows.Add(row);
            }
        }
    }
}
