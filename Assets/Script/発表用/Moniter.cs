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
    void Update()
    {
        if (EmotionPick.TryGetMaxMin(Happyou.Json, out var r))
        {
            MaxEmo.text = r.maxKey;
            MinEmo.text = r.minKey;
        }
        else
        {
            MaxEmo.text = "-";
            MinEmo.text = "-";
        }        
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
        public static bool TryGetMaxMin(
            string json,
            out (string maxKey, float maxVal, string minKey, float minVal) result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            EmotionResult obj;
            try
            {
                obj = JsonConvert.DeserializeObject<EmotionResult>(json);
            }
            catch
            {
                return false; // JSON壊れなどもスキップ
            }

            if (obj?.proba_by_class == null || obj.proba_by_class.Count == 0)
                return false;
            //Debug.Log(json);
            var maxPair = obj.proba_by_class.Aggregate((a, b) => a.Value >= b.Value ? a : b);
            var minPair = obj.proba_by_class.Aggregate((a, b) => a.Value <= b.Value ? a : b);

            result = (maxPair.Key, maxPair.Value, minPair.Key, minPair.Value);
            return true;
        }
    }

}
