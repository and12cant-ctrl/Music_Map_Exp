using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/CharacterGenerator")]
public class CharacterGenerator : ScriptableObject
{
    [SerializeField] private List<CharaData> charaTemplates;

    private Dictionary<string, int> nameCountDict = new();

    private readonly string[] kanjiNumbers = { "壱", "弐", "参", "肆"};

    public CharacterRuntimeData GenerateRandomCharacter()
    {

        if (charaTemplates.Count == 0)
        {
            Debug.LogWarning("キャラテンプレートがありません！");
            return null;
        }

        // 1. テンプレートをランダム選択
        var baseData = charaTemplates[Random.Range(0, charaTemplates.Count)];
        // 2. ランタイムデータを生成
        var runtime = new CharacterRuntimeData(baseData);

        // 2. 名前使用回数を確認
        if (!nameCountDict.ContainsKey(baseData._jobName))
        {
            nameCountDict[baseData._jobName] = 0;
        }
        int count = nameCountDict[baseData._jobName];
        nameCountDict[baseData._jobName]++;

        // 3. 漢数字付きの名前を生成
        string suffix = count < kanjiNumbers.Length ? kanjiNumbers[count] : $"({count + 1})";
        string newName = $"{baseData._jobName}-{suffix}";

        #region 4.ステータス補正
        int hpOffset = Random.Range(-10, 11);
        int newMaxHP = Mathf.Max(1, baseData._maxHP + hpOffset);
        #endregion

        runtime.charaName = newName;
        runtime.currentHP = newMaxHP;

        return runtime;
    }

    public void ResetNameCounts()
    {
        nameCountDict.Clear();
    }
}
