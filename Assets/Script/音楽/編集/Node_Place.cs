using System.Collections.Generic;
using UnityEngine;

public class Node_Place : MonoBehaviour
{
    // 今配置されている Node の一覧
    public List<Node> _nodes = new List<Node>();

    // 同じY(_scale)に置ける最大数
    private const int MaxPerY = 3;

    public bool CanPlace(Part part, string pitch, float start, int bars,float time)
    {
        //Debug.Log($"{scale},{beats}");
        // 1) すでに同じ (X,Y) が存在していないか？
        foreach (var node in _nodes)
        {
            if (node._Part == part && node._pitch == pitch && node._startBar == start && node._bar == bars &&
                (bars >= node._startBar && bars <= node._startBar + node._duration))
                
            {
                return false;
            }
        }

        // 2) 同じ Y(_scale) の Node が何個あるか数える
        int countSameY = 0;
        foreach (var node in _nodes)
        {
            if (node._startBar == start && node._bar == bars)
            {
                countSameY++;
            }
        }
        if (countSameY >= MaxPerY)
        {
            return false;
        }

        // ここまで来たら置いてOK
        return true;
    }

    /// <summary>
    /// 置けるなら Node を作って登録する
    /// （※ ScriptableObject を動的生成する例）
    /// </summary>
    public bool PlaceNode(float time, Part part,string pitch,float start,int bars)
    {
        if (!CanPlace(part, pitch, start, bars,time))
        {
            Debug.LogWarning($"この位置には置けません: {bars}小節、{start+1}拍、{pitch}");
            return false;
        }

        var node = new Node();
        node._duration = time;
        node._Part = part;
        node._pitch = pitch;
        node._startBar = start;
        node._bar = bars;

        _nodes.Add(node);
        return true;
    }
}
