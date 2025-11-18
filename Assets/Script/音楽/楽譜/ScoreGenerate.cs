using System.Linq;
using UnityEngine;

public class ScoreGenerate : MonoBehaviour
{

    [SerializeField]private Node_Place NodeList;
    public string[] Gcode = {"G","A", "B", "C", "D", "E", "F"};
    void LinQOrder()
    {
        NodeList._nodes = NodeList._nodes.
            OrderBy(n => n._Part).
            ThenBy(n => n._scale).
            ThenBy(n => n._duration).
            ToList();
    }

    void NodeChenge()
    {
        foreach (Node node in NodeList._nodes)
        {
            string num =$"{(node._beats / Gcode.Length)+4}";
            string arufa = Gcode[node._beats % Gcode.Length];
            node.pitch = $"{arufa}{num}";

        }
    }
    
}
