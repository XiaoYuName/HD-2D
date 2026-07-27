using UnityEngine;
using XFramework;

public partial class SewingMachineUI : UIBase
{
    private Sprite CursorTexture;
    private SewingMachineGameData Setting;
    private SweingMachinePanel currentPanel;
    
    public override void Init()
    {
        InitAutoBind();

        Setting = LoadAsset<SewingMachineGameData>(AssetKeys.SewingMachineGameDataPath);
        CursorTexture = LoadAsset<Sprite>(AssetKeys.ShouPath);
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Cursor.SetCursor(CursorTexture.texture, Vector2.zero, CursorMode.Auto);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        ClearCurrentPanel();
        base.Close();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        GenerateRandomPanel();
    }

    private void GenerateRandomPanel()
    {
        ClearCurrentPanel();

        if (Setting == null || Setting.Parents == null || Setting.Parents.Count <= 0)
        {
            Debug.LogWarning("SewingMachineGameData 未配置 Parents，无法生成缝纫机面板。");
            return;
        }

        int index = Random.Range(0, Setting.Parents.Count);
        string panelPath = Setting.Parents[index];
        if (string.IsNullOrEmpty(panelPath))
        {
            Debug.LogWarning("SewingMachineGameData Parents 中存在空路径，无法生成缝纫机面板。");
            return;
        }

        GameObject obj = AssetsManager.Instance.Instantiate(panelPath);
        RectTransform rectTransform = (RectTransform)obj.transform;
        rectTransform.SetParent(panelGroup, false);
        rectTransform.localScale = Vector3.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;

        currentPanel = obj.GetComponent<SweingMachinePanel>();
        if (currentPanel == null)
        {
            Debug.LogWarning($"生成的缝纫机面板缺少 SweingMachinePanel 组件：{panelPath}");
            AssetsManager.Instance.FreeGameObject(obj);
            return;
        }

        currentPanel.Init();
    }

    private void ClearCurrentPanel()
    {
        if (currentPanel == null)
        {
            return;
        }

        currentPanel.Release();
        AssetsManager.Instance.FreeGameObject(currentPanel.gameObject);
        currentPanel = null;
    }
    
}
