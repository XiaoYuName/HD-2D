using UnityEngine;
using XFramework;

public class TestManager : MonoBehaviour
{
    public static TestManager St => st != null ? st : st = FindAnyObjectByType<TestManager>();

    static TestManager st;

    void Awake()
    {
        st = this;
    }

    void OnDestroy()
    {
        if (st == this)
            st = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            TogglePanel();
    }

    /// <summary>开/关测试面板。</summary>
    public void TogglePanel()
    {
        // GetUI 会在首次调用时按 UIPage 表加载并缓存预制体（不会自动打开）
        TestPanel panel = UISystem.Instance.GetUI<TestPanel>(UIPanelIdSet.TestPanel);

        if (panel.isOpen)
            UISystem.Instance.CloseUI(UIPanelIdSet.TestPanel);
        else
            UISystem.Instance.OpenUI(UIPanelIdSet.TestPanel);
    }

    #region 便捷 GM 方法（可被其它脚本 / 按钮调用）
    public void AddItem(long id, int count = 1) => InventoryManager.Instance.AddItem(id, count);

    public void AddMoney(int value = 1000) => GameDataManager.Instance.AddProperty(PropertyType.Coin, value);

    public void AddGameCoin(int value = 1000) => GameDataManager.Instance.AddProperty(PropertyType.GameCoin, value);
    #endregion
}
