using JetBrains.Annotations;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class Nod_judge : MonoBehaviour,IPointerClickHandler
{

    private double[] Nodes= Enumerable.Range(0,(int)(90/4.5)+1).Select(i => i*4.5).ToArray();

    [SerializeField] private RectTransform staffRect; // クリック対象のエリア（五線譜など）
    [SerializeField] private Canvas canvas;           // 所属Canvas
    [SerializeField] private Camera uiCamera;         // Screen Space - Camera / World Space のときに使用
    [SerializeField] private RectTransform NodePrehab;
    public int beats = 8;
    private int scale = 20;
    private string number = "1";
    // クリック位置（アンカー基準のローカル座標）を取得
    public void OnPointerClick(PointerEventData eventData)
    {
        // Canvasの種類に応じてカメラを選ぶ
        Camera cam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            cam = uiCamera != null ? uiCamera : canvas.worldCamera;

        // 画面座標（スクリーン）→ RectTransformローカル座標 に変換
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                staffRect, eventData.position, cam, out Vector2 localPoint))
        {
            // 必要なら Rect 内にクランプ
            Vector2 half = staffRect.rect.size * 0.5f;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(localPoint.x, -half.x, half.x),
                Mathf.Clamp(localPoint.y, -half.y, half.y)
            );

            // アンカーポジションとして使える
            Vector2 anchoredPos = clamped; // staffRect直下に子を置くならこのまま使える

            // 正規化座標（0～1）も作っておくと便利（ビート/音高スナップに使う）
            float nx = Mathf.InverseLerp(-half.x, half.x, anchoredPos.x); // 横方向（時間）
            float ny = Mathf.InverseLerp(-half.y, half.y, anchoredPos.y); // 縦方向（音高）
            Debug.Log($"Normalized: ({nx:F3}, {ny:F3})");

            // ここで「スナップ処理」や「音符生成」を呼ぶ
            #region MyRegion
            float snappedX = Mathf.Round(nx * (beats)) / (beats);
            float snappedY = Mathf.Round(ny * (scale)) / (scale);
            if (snappedX >= 1) return;

            xml(snappedX, snappedY);

            Debug.Log($"X:{snappedX},Y: {snappedY}");
            float ansX = Mathf.Lerp(0, staffRect.rect.width, snappedX);
            float ansY = Mathf.Lerp(0, staffRect.rect.height, snappedY);

            Vector2 Nodesfgs = new Vector2(ansX, ansY);
            #endregion

            PlaceNodeat(Nodesfgs,number);
            
        } 
    }
    public void PlaceNodeat (Vector2 anchore,string name)
    {
        RectTransform note = Instantiate(NodePrehab, staffRect);
        note.anchoredPosition = anchore;
        note.name = name;
        Debug.Log($"note.anchore{note.anchoredPosition}");
    }

    public void xml(float x,float y)
    {
        float xmlbrats = x/(float)(1.0/beats);
        float xmlscale = y/(float)(1.0 / scale);
        Debug.Log($"XML!!{xmlbrats},{xmlscale}");
    }
}
