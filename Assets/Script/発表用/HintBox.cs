using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintBox : MonoBehaviour
{
    [SerializeField] private Button move;
    [SerializeField] private Button density;
    [SerializeField] private Button order;
    [SerializeField] private Button rhythm;
    [SerializeField] private GameObject hintBox;

    private void TextChenge(Hint title)
    {
        TMP_Text text = hintBox.transform.Find("ヒントタイトル").GetComponentInChildren<TMP_Text>();
        text.text = title.ToString();
        hintBox .SetActive(true);

        Button High = hintBox.transform.Find("大").GetComponentInChildren<Button>();
        Button Middle = hintBox.transform.Find("中").GetComponentInChildren<Button>();
        Button Low = hintBox.transform.Find("小").GetComponentInChildren<Button>();
        High.onClick.RemoveAllListeners();
        Middle.onClick.RemoveAllListeners();
        Low.onClick.RemoveAllListeners();

    }





}
