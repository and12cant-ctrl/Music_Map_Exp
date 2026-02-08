using UnityEngine;
using UnityEngine.UI;

public class ScoreButton : MonoBehaviour
{
    [SerializeField] private GameObject NodeButton;
    [SerializeField] private Button PartButton;
    [SerializeField] private GameObject TabPrehab;
    public Node_judge_re nodeJudge;
    
    void Start()
    {
        nodeJudge = GetComponent<Node_judge_re>();

    }

    void Update()
    {
        
    }
    private void et()
    {

    }
}
