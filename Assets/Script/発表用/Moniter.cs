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

    public void EmoTextChenge(string maxEmo,string minEmo,int a,int b)
    {
        MaxEmo.text = maxEmo;
        MinEmo.text = minEmo;
        Debug.Log($"{a}_{b}");
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