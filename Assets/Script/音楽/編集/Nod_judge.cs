using Unity.VisualScripting;
using UnityEngine;
using System.Linq;

public class Nod_judge : MonoBehaviour
{
    public RectTransform NodePitchRect;
    public RectTransform NodeTimeRect;

    private double[] Nodes= Enumerable.Range(0,(int)(90/4.5)+1).Select(i => i*4.5).ToArray();

    //Pitch‚Ì58‚ÅE
    //Pitch‚Ì67.2‚ÅG

    void Judge(RectTransform Node)
    {
        double Ans = Node.anchoredPosition.y / 4.5;
        Debug.Log(Ans);
        double closest = Nodes.OrderBy(s => Mathf.Abs((float)(s - Ans))).First();
        
        
    }



}
