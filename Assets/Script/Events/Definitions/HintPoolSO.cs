using System.Collections.Generic;
using UnityEngine;
namespace MusicMap.Events {
[CreateAssetMenu(menuName="Events/Case Hint Pool")]
public sealed class HintPoolSO : EventIdentitySO { public List<HintData> hints = new List<HintData>(); }
}