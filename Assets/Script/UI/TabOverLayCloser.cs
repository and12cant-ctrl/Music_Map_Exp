using UnityEngine;
using UnityEngine.UI;

public class TabOverLayCloser : MonoBehaviour
{
    [SerializeField] private Button overlayButton;

    // 今、オーバーレイの下で開いているタブ
    private GameObject currentTab;

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

        currentTab = tabRoot;

        // タブ本体を開く
        currentTab.SetActive(true);
        // 背景オーバーレイを表示
        gameObject.SetActive(true);
    }

    public void CloseCurrentTab()
    {
        if (currentTab != null)
        {
            currentTab.SetActive(false);
            currentTab = null;
        }

        gameObject.SetActive(false);
    }
}
