using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class Moniter : MonoBehaviour
{
    [SerializeField] private TMP_Text Score;
    [SerializeField] private TMP_Text ScoreText;
    [SerializeField] private TMP_Text Emo;
    [SerializeField] private MusicXmlEditorCaller Happyou;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var (maxKey, maxVal, minKey, minVal) = EmotionPick.GetMaxMin(Happyou.Json);
        Emo.text = maxKey;
        ScoreText.text = minKey;
        
    }
    [Serializable]
    public class EmotionResult
    {
        public string label;
        public List<string> classes;
        public Dictionary<string, float> proba_by_class;
        public List<float> proba_vec;
    }

    public static class EmotionPick
    {
        public static (string maxKey, float maxVal, string minKey, float minVal) GetMaxMin(string json)
        {
            var obj = JsonConvert.DeserializeObject<EmotionResult>(json);
            if (obj?.proba_by_class == null || obj.proba_by_class.Count == 0)
                throw new Exception("proba_by_class is missing or empty.");

            // Å‘å
            var maxPair = obj.proba_by_class.Aggregate((a, b) => a.Value >= b.Value ? a : b);
            // Å¬
            var minPair = obj.proba_by_class.Aggregate((a, b) => a.Value <= b.Value ? a : b);

            return (maxPair.Key, maxPair.Value, minPair.Key, minPair.Value);
        }
    }
}
