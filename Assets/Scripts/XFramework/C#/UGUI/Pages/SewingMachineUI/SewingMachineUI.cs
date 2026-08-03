using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class SewingMachineUI : UIBase
{
    private Sprite CursorTexture;
    private SewingMachineGameData Setting;
    private SweingMachinePanel currentPanel;
    private GameObject runtimeIron;
    private RectTransform runtimeIronRect;
    private Canvas rootCanvas;
    private bool isIronFollowing;

    public CharacterBag CurrentBag { get; private set; }
    public ClothingBag ClothingBag { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        Setting = LoadAsset<SewingMachineGameData>(AssetKeys.SewingMachineGameDataPath);
        CursorTexture = LoadAsset<Sprite>(AssetKeys.CursorHandPath);
        rootCanvas = GetComponentInParent<Canvas>();
        ClearIronTriggers();
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
        StopIronFollow();
        ClearCurrentPanel();
        base.Close();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        CurrentBag = characterBag;
        ClothingBag = clothingBag;
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
        currentPanel.SetIronScratchReadyCallback(StartIronFollow);
        currentPanel.SetData(Setting);
    }

    private void ClearCurrentPanel()
    {
        if (currentPanel == null)
        {
            return;
        }

        StopIronFollow();
        currentPanel.Release();
        AssetsManager.Instance.FreeGameObject(currentPanel.gameObject);
        currentPanel = null;
    }

    /// <summary>
    /// 熨斗改成拼图完成后自动跟随鼠标，Iron 上残留的拖拽事件要清掉，
    /// 否则预制体里配的旧监听还会再生成一把熨斗。
    /// </summary>
    private void ClearIronTriggers()
    {
        if (iron == null)
        {
            return;
        }

        iron.triggers.Clear();
    }

    /// <summary>
    /// 进入刮刮乐阶段：熨斗直接挂到鼠标上，不再需要玩家按住拖动
    /// </summary>
    private void StartIronFollow()
    {
        if (isIronFollowing || currentPanel == null || !currentPanel.IsIronScratchReady)
        {
            return;
        }

        isIronFollowing = true;
        CreateRuntimeIron();
        UpdateIronFollow();
    }

    private void StopIronFollow()
    {
        isIronFollowing = false;
        currentPanel?.StopIronScratch();
        ClearRuntimeIron();
    }

    private void Update()
    {
        if (!isIronFollowing)
        {
            return;
        }

        // 全部刮完后面板会把 IsIronScratchReady 置回 false，这时收起熨斗
        if (currentPanel == null || !currentPanel.IsIronScratchReady)
        {
            StopIronFollow();
            return;
        }

        UpdateIronFollow();
    }

    private void UpdateIronFollow()
    {
        Vector2 screenPosition = Input.mousePosition;
        // 熨斗预制体加载失败时只是没有表现，刮擦逻辑照常走，不要把玩法卡死
        if (runtimeIronRect != null)
        {
            MoveRuntimeIron(screenPosition, GetUICamera());
        }

        currentPanel.UpdateIronScratch(screenPosition, GetUICamera());
    }

    private void CreateRuntimeIron()
    {
        if (runtimeIron != null)
        {
            return;
        }

        runtimeIron = AssetsManager.Instance.Instantiate(AssetKeys.IronMonoPath);
        if (runtimeIron == null)
        {
            Debug.LogWarning($"SewingMachineUI 生成熨斗失败：{AssetKeys.IronMonoPath}");
            return;
        }

        runtimeIronRect = runtimeIron.transform as RectTransform;
        RectTransform parent = transform as RectTransform;
        runtimeIronRect.SetParent(parent, false);
        runtimeIronRect.localScale = Vector3.one;
        runtimeIronRect.localRotation = Quaternion.identity;
        runtimeIronRect.SetAsLastSibling();

        Graphic[] graphics = runtimeIron.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private void MoveRuntimeIron(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform parent = runtimeIronRect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera camera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 localPoint))
        {
            runtimeIronRect.anchoredPosition = localPoint;
        }
    }

    private void ClearRuntimeIron()
    {
        if (runtimeIron == null)
        {
            return;
        }

        AssetsManager.Instance.FreeGameObject(runtimeIron);
        runtimeIron = null;
        runtimeIronRect = null;
    }

    private Camera GetUICamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }


    public void Complete()
    {
        StopIronFollow();
        UIUtility.PopClothingMinGameComplete(CurrentBag, ClothingBag, ClothingMinGameType.SewingMachine, Close);

    }

}
