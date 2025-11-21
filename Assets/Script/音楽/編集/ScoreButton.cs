using UnityEngine;
using UnityEngine.UI;

public class ScoreButton : MonoBehaviour
{
    [SerializeField] private GameObject NodeButton;
    [SerializeField] private Button PartButton;
    [SerializeField] private GameObject TabPrehab;
    public Nod_judge nodeJudge;
    
    void Start()
    {
        nodeJudge = GetComponent<Nod_judge>();

    }

    void Update()
    {
        
    }
    private void et()
    {

    }
}
