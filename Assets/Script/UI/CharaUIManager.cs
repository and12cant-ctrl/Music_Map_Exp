using System.Collections.Generic;
using UnityEngine;

public class CharaUIManager : MonoBehaviour
{
    [SerializeField] private List<CharacterStatusUI> charaUIList;
    [SerializeField] private CardDrag CardPrehab;
    public void AssignCharacters(List<CharacterRuntimeData> characters)
    {
        for (int i = 0; i < charaUIList.Count; i++)
        {
            if (i < characters.Count)
            {
                charaUIList[i].Setup(characters[i]);
                charaUIList[i].gameObject.SetActive(true);
                charaUIList[i].CharaMan.Spawn(CardPrehab, characters[i]);
                Debug.Log($"{characters[i].charaName}");
            }
            else
            {
                charaUIList[i].gameObject.SetActive(false);     // ‹ó‚«‚Í”ñ•\Ž¦
            }
        }
    }
}
