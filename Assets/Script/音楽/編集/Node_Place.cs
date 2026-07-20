using System.Collections.Generic;
using UnityEngine;

public class Node_Place : MonoBehaviour
{
    // 今配置されている Node の一覧
    public List<NodeMapView> _nodes = new List<NodeMapView>();

    // 同じY(_scale)に置ける最大数
    private const int MaxPerY = 3;
    private voice voic;
    public bool CanPlace(float time, Part part, string pitch, float start, int bars)
    {
        //  同一パート・同一小節で時間が重なるなら置けない（voiceは問わない）
        foreach (var node in _nodes)
        {
            if (node._Part == part && node._bar == bars && node._pitch == pitch &&
                Mathf.Max(node._startBar, start) < Mathf.Min(node._startBar + node._duration, start + time))
            {
                return false;
            }
        }

        //  同じ(start, bar)に置ける数を制限、かつ空いてるvoiceを決める
        int countSameSlot = 0;
        foreach (var node in _nodes)
        {
            if (node._bar == bars && Mathf.Approximately(node._startBar, start))
            {
                countSameSlot++;
            }
        }

        if (countSameSlot >= MaxPerY)
            return false;

        voic = ChooseVoice(start, bars);
        return true;
    }

    /// <summary>
    /// 置けるなら Node を作って登録する
    /// duration=拍子,pitch=音階,bar=小節
    /// </summary>
    public bool PlaceNode(float duration, Part part, string pitch, float start, int bars)
    {
        if (!CanPlace(duration, part, pitch, start, bars))
        {
            Debug.LogWarning($"この位置には置けません: {bars}小節、{start * 4}拍、{pitch}");
            return false;
        }

        var node = new NodeMapView();
        node._duration = duration;
        node._Part = part;
        node._pitch = pitch;
        node._startBar = start;
        node._bar = bars;
        node._voice = voic;

        _nodes.Add(node);
        return true;
    }


    public bool PlaceXMLNode(float duration, Part part, string pitch, float start, int bars)
    {
        if (!CanPlace(duration, part, pitch, start, bars))
        {
            Debug.LogWarning($"この位置には置けません: {bars}小節、{start * 4}拍、{pitch}");
            return false;
        }

        var node = new NodeMapView();
        node._duration = duration;
        node._Part = part;
        node._pitch = pitch;
        node._startBar = start;
        node._bar = bars;
        node._voice = voic;

        _nodes.Add(node);
        return true;
    }
    public void DeleteNode(float duration, Part part, string pitch, float start, int bars)
    {

        _nodes.RemoveAll(n =>
            n._Part == part &&
            n._pitch == pitch &&
            n._bar == bars &&
            Mathf.Approximately(n._startBar, start) 
        );

    }
    private voice ChooseVoice(float start, int bars)
    {
        bool usedUp = false, usedMid = false, usedDown = false;

        foreach (var n in _nodes)
        {
            if (Mathf.Approximately(n._startBar, start) && n._bar == bars)
            {
                if (n._voice == voice.Up) usedUp = true;
                else if (n._voice == voice.Middle) usedMid = true;
                else if (n._voice == voice.Down) usedDown = true;
            }
        }

        if (!usedUp) return voice.Up;
        if (!usedMid) return voice.Middle;
        return voice.Down;
    }

}
