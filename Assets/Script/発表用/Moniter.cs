using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class Moniter : MonoBehaviour
{
    [SerializeField] private TMP_Text Score;
    [SerializeField] private TMP_Text MinEmo;
    [SerializeField] private TMP_Text MaxEmo;
    [SerializeField] private MusicXmlEditorCaller Happyou;
    [SerializeField] private AnswerSet Ans;
    void Start()
    {

        MaxEmo.text = "-";
        MinEmo.text = "-";
        Score.text = "-";
    }

    public void EmoTextChenge(string maxEmo,string minEmo,
        string Move,string Dens,string order,string rhythm)
    {
        int a =0;
        int b =0;
        MaxEmo.text = maxEmo;
        MinEmo.text = minEmo;
        if (Ans.move.ToString() == Move) a++;
        if (Ans.density.ToString() == Dens) a++;
        if (Ans.order.ToString() == order) a++;
        if (Ans.rhythm.ToString() == rhythm) a++;

        if (Ans.maxEmo.ToString() == maxEmo) b++;
        if (Ans.minEmo.ToString() == minEmo) b++;
        Score.text = (a * 10 + b * 15).ToString();
    }

    [Serializable]

    public class EmotionResult
    {
        public string label;
        public List<string> classes;
        public Dictionary<string, float> proba_by_class;
        public List<float> proba_vec;
    }

}