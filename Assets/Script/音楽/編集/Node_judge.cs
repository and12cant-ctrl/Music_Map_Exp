using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;


public class Node_judge : MonoBehaviour,IPointerClickHandler
{
    [SerializeField] private RectTransform staffRect; // クリック対象のエリア（五線譜など）
    [SerializeField] private Canvas canvas;           // 所属Canvas
    [SerializeField] private Camera uiCamera;         // Screen Space - Camera / World Space のときに使用
    [SerializeField] public RectTransform NodePrehab;
    [SerializeField] private Node_Place NodeCheak;
    [SerializeField] public EditMode editMode = EditMode.Place;
    public float beat = 4.0f;           //小節の拍数
    private int scale = 20;             //音階の下から上までの数
    private string nameNumber = "1";    //音符の名付け
    public Part part = Part.A;         //パート名
    public float nodeLength = 0.5f;        //選択した音符の拍数（現在八分音符）
    private int measure = 1;            //現在小節数
    public float bar = 4;               //最大小節数
    public harfNode hafeN =harfNode.None;
    private string[] Gcode = { "G", "A", "B", "C", "D", "E", "F" };

    public void OnPointerClick(PointerEventData eventData)
    {
        double[] Nodes = Enumerable.Range(0,(int)((2*beat))).Select(i =>(double)i*nodeLength*(1/beat)).ToArray();
        double[] Scales = Enumerable.Range(0, scale).Select(i => (double)i / scale).ToArray();
        // Canvasの種類に応じてカメラを選ぶ
        Camera cam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            cam = uiCamera != null ? uiCamera : canvas.worldCamera;
        Vector2 NodesFin = new Vector2();
        // 画面座標（スクリーン）→ RectTransformローカル座標 に変換
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                staffRect, eventData.position, cam, out Vector2 localPoint))
        {

            Vector2 half = staffRect.rect.size * 0.5f;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(localPoint.x, -half.x, half.x),
                Mathf.Clamp(localPoint.y, -half.y, half.y)
            );

            #region     正規化
            Vector2 anchoredPos = clamped; // staffRect直下に子を置くならこのまま使える
            // 正規化座標（0～1）も作っておくと便利（ビート/音高スナップに使う）
            float nx = Mathf.InverseLerp(-half.x, half.x, anchoredPos.x); // 横方向（時間）
            float ny = Mathf.InverseLerp(-half.y, half.y, anchoredPos.y); // 縦方向（音高）

            float snappedX = Normalize(Nodes, nx);
            float snappedY = Normalize(Scales, ny);
            if (snappedX >= 1) return;

            #region 変換
            int ScaleX = Mathf.RoundToInt(snappedX * 100);
            int BeatY= Mathf.RoundToInt(snappedY * 100);
            //Debug.Log($"ScaleX{ScaleX} ,BeatY{BeatY}");
            string hafeNodes =
                hafeN == harfNode.Shape ? "#" :
                hafeN == harfNode.Flat ? "♭" : "";

            string num = $"{Gcode[(BeatY / 5 ) % Gcode.Length]}{hafeNodes}{(((BeatY / 5) + 6) / Gcode.Length) + 3}";
            float start =(snappedX * Mathf.Clamp(measure - 1,1,measure-1) * bar*2) % bar;
            int truebars =(int)(measure+ (snappedX * Mathf.Clamp(measure - 1, 1, measure - 1) * bar * 2) / bar);
//            Debug.Log($"num [{num}]& start[{start}] &[{truebars}]");

            #endregion
            if (!NodeCheak.PlaceNode(nodeLength,part,num,start,truebars)) return;

            //Debug.Log(staffRect.rect.height);
            float ansX = Mathf.Lerp(0, staffRect.rect.width, snappedX); //割合で×して返す
            float ansY = Mathf.Lerp(0, staffRect.rect.height, snappedY);

            NodesFin = new Vector2(ansX, ansY);
            #endregion
        }
        switch (editMode)
        {
            case EditMode.Place:
                PlaceNodeat(NodesFin, nameNumber);
                break;
        }
    }
    public void PlaceNodeat (Vector2 anchore,string name)
    {
        RectTransform note = Instantiate(NodePrehab, staffRect);
        note.anchoredPosition = anchore;
        note.name = name;
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
    public enum EditMode
    {
        Place,Delete,Change
    }
    public enum harfNode
    {
        Shape,Flat,None
    }
    public enum LengthNode
    {
        Harf,One,Two,all
    }

    public void CloseTab(GameObject Tab,RectTransform Area)
    {

    }
}
