using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class Moniter : MonoBehaviour
{
    [SerializeField] private TMP_Text Score;
    [SerializeField] private TMP_Text MinEmo;
    [SerializeField] private TMP_Text MaxEmo;
    [SerializeField] private MusicXmlEditorCaller Happyou;
    [SerializeField] private AnswerSet Ans;
    [SerializeField] private GameObject AnsBut;
    [SerializeField] private GameObject Toggle;
    [SerializeField] private Button Close;
    [SerializeField] public TabOverLayCloser TabClose;
    void Start()
    {
        Button AnsButton =AnsBut.GetComponent<Button>();
        AnsButton.onClick.AddListener(() =>
        {
            Toggle.SetActive(true);
            AnsBut.SetActive(false);
            TabClose.CloseCurrentTab();
        });
        Close.onClick.AddListener(() =>
        {
            Toggle.SetActive(false);

            AnsBut.SetActive(true );
        });
        MaxEmo.text = "-";
        MinEmo.text = "-";
        Score.text = "-";
    }

    public void EmoTextChenge(string maxEmo,string minEmo,int a,int b)
    {
        MaxEmo.text = maxEmo;
        MinEmo.text = minEmo;
        Debug.Log($"{a}_{b}");
        Score.text = (a * 15 + b * 20).ToString();
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