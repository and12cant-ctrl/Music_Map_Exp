using UnityEngine;
using UnityEngine.UI;

public class ScoreButton : MonoBehaviour
{
    [SerializeField] private GameObject NodeButton;
    [SerializeField] private Button PartButton;
    [SerializeField] private GameObject TabPrehab;
    public Node_judge nodeJudge;
    
    void Start()
    {
        nodeJudge = GetComponent<Node_judge>();

    }

    void Update()
    {
        
    }
    private void et()
    {

    }
}
