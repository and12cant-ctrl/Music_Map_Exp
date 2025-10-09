using UnityEngine;
using UnityEngine.UI;
public class MinimapCellView : MonoBehaviour
{
    [SerializeField] private Image fill;   // 中塗り
    [SerializeField] private Image frame;  // 白枠（常時表示、white/alpha=1）

    public void SetColor(Color c) => fill.color = c;

    public void SetFrame(Color color, int thicknessPx = 1)
    {
        if (frame == null) return;
        frame.color = color;
        // 厚みは 9-slice やスプライト側で調整するのが綺麗
    }
}
