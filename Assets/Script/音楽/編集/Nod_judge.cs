using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;


public class Nod_judge : MonoBehaviour,IPointerClickHandler
{
    [SerializeField] private RectTransform staffRect; // クリック対象のエリア（五線譜など）
    [SerializeField] private Canvas canvas;           // 所属Canvas
    [SerializeField] private Camera uiCamera;         // Screen Space - Camera / World Space のときに使用
    [SerializeField] private RectTransform NodePrehab;
    [SerializeField] private Node_Place NodeCheak;
    public int beat = 8;
    private int scale = 20;
    private string number = "1";
    private Part part = Part.A;
    private float time = 0.5f;

    public void OnPointerClick(PointerEventData eventData)
    {
        double[] Nodes = Enumerable.Range(0,beat+1).Select(i =>(double)i*1/beat).ToArray();
        double[] Scales = Enumerable.Range(0, scale+1).Select(i => (double)i * 1 / scale).ToArray();
        // Canvasの種類に応じてカメラを選ぶ
        Camera cam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            cam = uiCamera != null ? uiCamera : canvas.worldCamera;

        // 画面座標（スクリーン）→ RectTransformローカル座標 に変換
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                staffRect, eventData.position, cam, out Vector2 localPoint))
        {

            Vector2 half = staffRect.rect.size * 0.5f;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(localPoint.x, -half.x, half.x),
                Mathf.Clamp(localPoint.y, -half.y, half.y)
            );


            Vector2 anchoredPos = clamped; // staffRect直下に子を置くならこのまま使える
            #region     正規化
            // 正規化座標（0～1）も作っておくと便利（ビート/音高スナップに使う）
            float nx = Mathf.InverseLerp(-half.x, half.x, anchoredPos.x); // 横方向（時間）
            float ny = Mathf.InverseLerp(-half.y, half.y, anchoredPos.y); // 縦方向（音高）
            //Debug.Log($"Normalized: ({nx:F3}, {ny:F3})");

            // ここで「スナップ処理」や「音符生成」を呼ぶ

            float snappedX = Normalize(Nodes, nx);
            float snappedY = Normalize(Scales, ny);
            //Debug.Log($"snappedX,Y{snappedX} {snappedY}");
            if (snappedX >= 1) return;

            #region 変換
            int ScaleX = (int)(snappedX * 100);
            int BeatY= (int)(snappedY * 100);
            Debug.Log($"ScaleX{ScaleX} ,BratY{BeatY}");
            #endregion
            if (!NodeCheak.PlaceNode(ScaleX, BeatY,time, part)) return;

            Debug.Log(staffRect.rect.height);
            float ansX = Mathf.Lerp(0, staffRect.rect.width, snappedX); //割合で×して返す
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
        float xmlbrats = x/(float)(1.0/beat);
        float xmlscale = y/(float)(1.0 / scale);
        Debug.Log($"XML!!{xmlbrats},{xmlscale}");
    }

    public float Normalize(double[] separator, float ans)
    {
        if (separator == null || separator.Length == 0)
        {
            throw new ArgumentException("separator が空です");
        }
        float closest = (float)separator[0];
        float minDiff = Mathf.Abs(ans - closest);
        for (int i = 1; i < separator.Length; i++)
        {
            float value = (float)separator[i];
            float diff = Mathf.Abs(ans - value);

            if (diff < minDiff)
            {
                minDiff = diff;
                closest = value;
            }
        }
        return closest;
    }
}
