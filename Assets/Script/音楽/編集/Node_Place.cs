using System.Collections.Generic;
using UnityEngine;

public class Node_Place : MonoBehaviour
{
    // 今配置されている Node の一覧
    public List<Node> _nodes = new List<Node>();

    // 同じY(_scale)に置ける最大数
    private const int MaxPerY = 3;
    private voice voic;
    public bool CanPlace(float time, Part part, string pitch, float start, int bars)
    {
        //Debug.Log($"{scale},{beats}");
        // 1) すでに同じ (X,Y) が存在していないか？
        foreach (var node in _nodes)
        {
            if (node._Part == part && node._pitch == pitch && 
               Mathf.Max(node._startBar,start)<Mathf.Min(node._startBar+node._duration,start+time))
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
        switch (countSameY)
        {
            case 0:
               voic = voice.Up;
                break;
            case 1:
                voic= voice.Middle;
                break;
            case 2:
                voic = voice.Down;
                break;
            default:
                break;
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
    /// duration=拍子,pitch=音階,bar=小節
    /// </summary>
    public bool PlaceNode(float duration, Part part,string pitch,float start,int bars)
    {
        if (!CanPlace( duration,part, pitch, start, bars))
        {
            Debug.LogWarning($"この位置には置けません: {bars}小節、{start*4}拍、{pitch}");
            return false;
        }

        var node = new Node();
        node._duration = duration;
        node._Part = part;
        node._pitch = pitch;
        node._startBar = start;
        node._bar = bars;
        node._voice = voic;

        _nodes.Add(node);
        return true;
    }
}
