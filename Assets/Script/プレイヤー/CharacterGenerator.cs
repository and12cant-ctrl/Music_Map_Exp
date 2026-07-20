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

        CharacterRuntimeData charaRuntime = new CharacterRuntimeData(charaTemplates[Random.Range(0, charaTemplates.Count)]);

        // 1. テンプレートをランダム選択
        // 2. ランタイムデータを生成


        // 2. 名前使用回数を確認
        if (!nameCountDict.ContainsKey(charaRuntime.baseData._jobName))
        {
            nameCountDict[charaRuntime.baseData._jobName] = 0;
        }
        int count = nameCountDict[charaRuntime.baseData._jobName];
        nameCountDict[charaRuntime.baseData._jobName]++;

        // 3. 漢数字付きの名前を生成
        string suffix = count < kanjiNumbers.Length ? kanjiNumbers[count] : $"({count + 1})";
        string newName = $"{charaRuntime.baseData._jobName}-{suffix}";

        #region 4.ステータス補正
        int hpOffset = Random.Range(-10, 11);
        int newMaxHP = Mathf.Max(1, charaRuntime.baseData._defoMaxHP + hpOffset);
        int newST = Random.Range(-2, 2);
        int newMaxAPI = Mathf.Max(1,charaRuntime.baseData ._defApi+newST);
        charaRuntime.currentMaxHP = newMaxHP;
        charaRuntime.currentHP = charaRuntime.currentMaxHP;
        charaRuntime.curApi = newMaxAPI;
        #endregion      

        charaRuntime.charaName = newName;
        charaRuntime.currentHP = newMaxHP;

        return charaRuntime;
    }

    public void ResetNameCounts()
    {
        nameCountDict.Clear();
    }
}
