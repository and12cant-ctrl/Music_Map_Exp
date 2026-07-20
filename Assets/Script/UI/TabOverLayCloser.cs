using UnityEngine;
using UnityEngine.UI;

public class TabOverLayCloser : MonoBehaviour
{
    [SerializeField] private Button overlayButton;

    private GameObject currentTab;
    private bool comonTab = false;
    void Awake()
    {
        gameObject.SetActive(false);

        if (overlayButton != null)
        {
            overlayButton.onClick.RemoveAllListeners();
            overlayButton.onClick.AddListener(CloseCurrentTab);
        }
    }

    public void OpenTab(GameObject tabRoot)
    {
        if (tabRoot == null) return;
        if(comonTab)currentTab.SetActive(false);
        currentTab = tabRoot;

        // タブ本体を開く
        currentTab.SetActive(true);
        // 背景オーバーレイを表示
        gameObject.SetActive(true);
        comonTab = true;
    }

    public void CloseCurrentTab()
    {
        if (currentTab != null)
        {
            currentTab.SetActive(false);
            currentTab = null;
        }

        gameObject.SetActive(false);
        comonTab = false;
    }
}
