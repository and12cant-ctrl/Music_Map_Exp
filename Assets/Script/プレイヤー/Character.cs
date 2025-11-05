using UnityEngine;

public class Character : MonoBehaviour
{
    public CharaData chara;
    public CharacterRuntimeData runtime;

    public void Start()
    {
        Setup(chara);
    }
    public void Setup(CharaData baseData)
    {
        runtime = new CharacterRuntimeData(baseData);
    }
}
