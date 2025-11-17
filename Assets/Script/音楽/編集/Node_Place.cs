using System.Collections.Generic;
using UnityEngine;

public class Node_Place : MonoBehaviour
{
    // 今配置されている Node の一覧
    private readonly List<Node> _nodes = new List<Node>();

    // 同じY(_scale)に置ける最大数
    private const int MaxPerY = 3;

    public bool CanPlace(int scale, int beats,int time,Part part)
    {
        Debug.Log($"{scale},{beats}");
        // 1) すでに同じ (X,Y) が存在していないか？
        foreach (var node in _nodes)
        {
            if (node._scale == scale && node._beats == beats)
            {
                return false;
            }
        }

        // 2) 同じ Y(_scale) の Node が何個あるか数える
        int countSameY = 0;
        foreach (var node in _nodes)
        {
            if (node._scale == scale)
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
    public bool PlaceNode(int scaleN, int beatsN, int time, Part part)
    {
        if (!CanPlace(scaleN, beatsN,time,part))
        {
            Debug.LogWarning($"この位置には置けません: scale={scaleN}, beats={beatsN}");
            return false;
        }

        var node = new Node();
        node._scale = scaleN;
        node._beats = beatsN;
        node._time = time;
        node._Part = part;

        _nodes.Add(node);
        return true;
    }
}
