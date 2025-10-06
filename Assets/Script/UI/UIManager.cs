using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private List<CharacterStatusUI> charaUIList;
    public void AssignCharacters(List<Character> characters)
    {
        for (int i = 0; i < charaUIList.Count; i++)
        {
            if (i < characters.Count)
            {
                charaUIList[i].Setup(characters[i]);
                charaUIList[i].gameObject.SetActive(true);
            }
            else
            {
                charaUIList[i].gameObject.SetActive(false); // ‹ó‚«‚Í”ñ•\Ž¦
            }
        }
    }
}
