using System;
using UnityEngine;
using Sirenix.OdinInspector;
namespace MusicMap.Events {
public abstract class EventIdentitySO : ScriptableObject {
[SerializeField, ReadOnly] private string persistentId = Guid.NewGuid().ToString("N");
public string PersistentId => persistentId;
public string displayName;
[TextArea] public string description;
protected virtual void OnValidate() { if (string.IsNullOrEmpty(persistentId)) persistentId=Guid.NewGuid().ToString("N"); }
}
}
