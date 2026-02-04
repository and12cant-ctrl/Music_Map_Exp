using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class Node_judge : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform staffRect; // クリック対象のエリア（五線譜など）
    [SerializeField] private RectTransform exBox;
    [SerializeField] private Canvas canvas;           // 所属Canvas
    [SerializeField] private Camera uiCamera;         // Screen Space - Camera / World Space のときに使用
    [SerializeField] public RectTransform NodePrehab;
    [SerializeField] private Node_Place NodeCheak;
    private EditMode _editMode;
    private float scoreWidth = 330;
    public EditMode editMode
    {
        get => _editMode;
        set
        {
            if (_editMode == value) return;
            Debug.Log($"[editMode SET] {_editMode} -> {value} (this={GetInstanceID()})\n{Environment.StackTrace}");
            _editMode = value;
        }
    }
    public float beat = 4.0f;           //小節の拍数
    private int scale = 20;             //音階の下から上までの数
    private string nameNumber = "1";    //音符の名付け
    public Part part = Part.A;         //パート名
    [HideInInspector] public float nodeLength = 0.5f;        //選択した音符の拍数（現在八分音符）
    [HideInInspector] public int currentBar = 1;            //現在小節数
    [HideInInspector] public float maxBar = 4;               //最大小節数
    [HideInInspector] public harfNode hafeN = harfNode.None;
    [HideInInspector] public LengthNode Leng = LengthNode.One;
    private string[] Gcode = { "A", "B", "C", "D", "E", "F", "G", };

    public void OnPointerClick(PointerEventData eventData)
    {

        double[] Nodes = Enumerable.Range(0, (int)(2 * beat)).Select(i => i * nodeLength * (1.0 / beat)).ToArray();
        double[] Scales = Enumerable.Range(0, scale).Select(i => (double)i / scale).ToArray();
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
                Mathf.Clamp(localPoint.y, -half.y, half.y));

            #region     正規化
            float nx = Mathf.InverseLerp(-half.x, half.x, clamped.x); // 横方向（時間）
            float ny = Mathf.InverseLerp(-half.y, half.y, clamped.y); // 縦方向（音高）

            //Debug.Log(nx);
            float snappedX = Normalize(Nodes, nx);
            float snappedY = Normalize(Scales, ny);
            if (snappedX >= 1) return;

            #region 変換
            //int ScaleX = Mathf.RoundToInt(snappedX * 100);
            int BeatY = Mathf.RoundToInt(snappedY * 100);

            string hafeNodes =
                hafeN == harfNode.Shape ? "#" :
                hafeN == harfNode.Flat ? "♭" : "";

            string num = $"{Gcode[(BeatY / 5) % Gcode.Length]}{hafeNodes}{(((BeatY / 5) + 6) / Gcode.Length) + 3}";
            float start = (snappedX * Mathf.Clamp(currentBar - 1, 1, currentBar - 1) * 2);
            int truebars = (int)(currentBar + (snappedX * Mathf.Clamp(currentBar - 1, 1, currentBar - 1) * maxBar * 2) / maxBar);
            Debug.Log($"num [{num}]& start[{start}] &[{truebars}]");

            #endregion


            //Debug.Log(staffRect.rect.height);
            float ansX = Mathf.Lerp(0, staffRect.rect.width, snappedX); //割合で×して返す
            float ansY = Mathf.Lerp(0, staffRect.rect.height, snappedY);

            NodesFin = new Vector2(ansX + ((currentBar - 1) * scoreWidth), ansY);
            #endregion
            Debug.Log($"[Switch] this={GetInstanceID()} editMode={editMode}");
            switch (editMode)
            {
                case EditMode.Place:
                    if (!NodeCheak.PlaceNode(nodeLength, part, num, start, truebars)) return;
                    Debug.Log("Place");
                    PutONNodeat(NodesFin, nameNumber);
                    break;
                case EditMode.Delete:
                    Debug.Log("Delete");
                    NodeCheak.DeleteNode(nodeLength, part, num, start, truebars);
                    DelNode(NodesFin, nameNumber);
                    break;
            }
        }
    }
    public void PutONNodeat (Vector2 anchore,string name)
    {
        RectTransform note = Instantiate(NodePrehab, exBox);
        note.anchoredPosition = anchore;
        note.name = name;
    }
    private void DelNode(Vector2 anchore, string name)
    {
        float eps = 0.1f; // UIなので少し余裕を持たせる

        foreach (RectTransform child in exBox)
        {
            if (Vector2.Distance(child.anchoredPosition, anchore) < eps)
            {
                Destroy(child.gameObject);
                return; // 1個消したら終了
            }
        }
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

    //private Vector2 XmltoNode(string Code,float sta,int bar)
    //{
    //    Vector2 a = new Vector2(0,0);
    //    return a;
    //}

    //public void xml(float x,float y)
    //{
    //    float xmlbrats = x/(float)(1.0/beat);
    //    float xmlscale = y/(float)(1.0 / scale);
    //    Debug.Log($"XML!!{xmlbrats},{xmlscale}");
    //}

}
